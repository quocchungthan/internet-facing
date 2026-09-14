#!/usr/bin/env bash
set -Eeuo pipefail

: "${AFFINE_DEPLOY_DIR:=/opt/affine-note}"
: "${AFFINE_SOURCE_DIR:=}"
: "${AFFINE_DOMAIN:=note.eldervibe.dev}"
: "${AFFINE_UPSTREAM_PORT:=3010}"
: "${CADDY_SITES_DIR:=/etc/caddy/sites}"
: "${CADDY_CONFIG:=/etc/caddy/Caddyfile}"
: "${CADDY_BIN:=caddy}"
: "${CADDY_SERVICE:=caddy}"
: "${CADDY_LOCK_FILE:=/var/lock/deploy-caddy.lock}"
: "${DOCKER_COMPOSE_BIN:=docker compose}"

[[ "$AFFINE_DOMAIN" =~ ^[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?(\.[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?)+$ ]] || {
	echo "AFFINE_DOMAIN must be a lowercase DNS name: $AFFINE_DOMAIN" >&2
	exit 2
}

repo_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
AFFINE_SOURCE_DIR="${AFFINE_SOURCE_DIR:-$repo_root/Streams/DaHang/.docker/selfhost}"
mkdir -p "$AFFINE_DEPLOY_DIR" "$CADDY_SITES_DIR" "$(dirname -- "$CADDY_LOCK_FILE")"
cp -R "$AFFINE_SOURCE_DIR/." "$AFFINE_DEPLOY_DIR/"
sed -i -E "s#- '3010:3010'#- '127.0.0.1:${AFFINE_UPSTREAM_PORT}:3010'#" "$AFFINE_DEPLOY_DIR/compose.yml"

cd "$AFFINE_DEPLOY_DIR"
$DOCKER_COMPOSE_BIN -f compose.yml up -d

fragment="$CADDY_SITES_DIR/$AFFINE_DOMAIN.caddy"
fragment_tmp="$CADDY_SITES_DIR/.$AFFINE_DOMAIN.caddy.$$"
fragment_backup="$CADDY_SITES_DIR/.$AFFINE_DOMAIN.caddy.previous.$$"
state=absent

exec 9>"$CADDY_LOCK_FILE"
flock -n 9 || { echo "Another Caddy deployment is already running" >&2; exit 1; }

cleanup() {
	rm -f "$fragment_tmp" "$fragment_backup"
}
trap cleanup EXIT

if [[ -f "$fragment" ]]; then
	state=file
	cp -- "$fragment" "$fragment_backup"
fi

cat > "$fragment_tmp" <<EOF
$AFFINE_DOMAIN {
	reverse_proxy 127.0.0.1:$AFFINE_UPSTREAM_PORT
}
EOF
mv -f -- "$fragment_tmp" "$fragment"

if ! "$CADDY_BIN" validate --config "$CADDY_CONFIG" --adapter caddyfile; then
	if [[ "$state" == file ]]; then mv -f -- "$fragment_backup" "$fragment"; else rm -f "$fragment"; fi
	"$CADDY_BIN" validate --config "$CADDY_CONFIG" --adapter caddyfile >/dev/null
	exit 1
fi

if ! systemctl reload "$CADDY_SERVICE"; then
	if [[ "$state" == file ]]; then mv -f -- "$fragment_backup" "$fragment"; else rm -f "$fragment"; fi
	"$CADDY_BIN" validate --config "$CADDY_CONFIG" --adapter caddyfile >/dev/null
	systemctl reload "$CADDY_SERVICE" || true
	exit 1
fi

echo "AFFiNE deployed at https://$AFFINE_DOMAIN"