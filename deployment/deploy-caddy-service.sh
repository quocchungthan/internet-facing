#!/usr/bin/env bash
set -Eeuo pipefail

: "${SERVICE_NAME:?SERVICE_NAME must identify the service}"
: "${SERVICE_DOMAIN:?SERVICE_DOMAIN must identify the public domain}"
: "${SERVICE_UPSTREAM_PORT:?SERVICE_UPSTREAM_PORT must identify the container port}"
: "${SERVICE_CONTAINER:?SERVICE_CONTAINER must identify the container}"
: "${SERVICE_IMAGE:?SERVICE_IMAGE must name the image loaded on the VPS}"
: "${SERVICE_ENV_FILE:?SERVICE_ENV_FILE must identify the Docker environment file}"
: "${SERVICE_VOLUME:=}"
: "${CADDY_SITES_DIR:=/etc/caddy/sites}"
: "${CADDY_CONFIG:=/etc/caddy/Caddyfile}"
: "${CADDY_BIN:=caddy}"
: "${CADDY_SERVICE:=caddy}"
: "${DOCKER_BIN:=docker}"
: "${CADDY_LOCK_FILE:=${DEPLOY_LOCK_FILE:-/var/lock/deploy-caddy.lock}}"

if [[ ! "$SERVICE_DOMAIN" =~ ^[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?(\.[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?)+$ ]]; then
	echo "SERVICE_DOMAIN must be a lowercase DNS name: $SERVICE_DOMAIN" >&2
	exit 2
fi

fragment="$CADDY_SITES_DIR/$SERVICE_DOMAIN.caddy"
fragment_tmp="$CADDY_SITES_DIR/.$SERVICE_DOMAIN.caddy.$$"
fragment_backup="$CADDY_SITES_DIR/.$SERVICE_DOMAIN.caddy.previous.$$"
state=absent

umask 077
mkdir -p "$(dirname "$CADDY_LOCK_FILE")" "$CADDY_SITES_DIR"
exec 9>"$CADDY_LOCK_FILE"
flock -n 9 || { echo "Another Caddy deployment is already running" >&2; exit 1; }

cleanup() {
	rm -f "$fragment_tmp" "$fragment_backup"
}
trap cleanup EXIT

capture_fragment() {
	if [[ -f "$fragment" ]]; then
		state=file
		cp -- "$fragment" "$fragment_backup"
	fi
}

restore_fragment() {
	case "$state" in
		file) mv -f -- "$fragment_backup" "$fragment" ;;
		absent) rm -f -- "$fragment" ;;
	esac
}

reload_caddy() {
	systemctl reload "$CADDY_SERVICE"
}

rollback_caddy() {
	restore_fragment
	if ! "$CADDY_BIN" validate --config "$CADDY_CONFIG" --adapter caddyfile >/dev/null; then
		echo "Restored Caddy configuration is invalid" >&2
		return 1
	fi
	reload_caddy
}

capture_fragment
cat > "$fragment_tmp" <<EOF
$SERVICE_DOMAIN {
	reverse_proxy 127.0.0.1:$SERVICE_UPSTREAM_PORT
}
EOF
mv -f -- "$fragment_tmp" "$fragment"

if ! "$CADDY_BIN" validate --config "$CADDY_CONFIG" --adapter caddyfile; then
	rollback_caddy
	echo "Caddy configuration validation failed; restored $fragment" >&2
	exit 1
fi

if ! reload_caddy; then
	rollback_caddy
	echo "Caddy reload failed; restored $fragment" >&2
	exit 1
fi

"$DOCKER_BIN" rm --force "$SERVICE_CONTAINER" >/dev/null 2>&1 || true
docker_volume_args=()
if [[ -n "$SERVICE_VOLUME" ]]; then
	docker_volume_args+=(--volume "$SERVICE_VOLUME")
fi
"$DOCKER_BIN" run --detach --name "$SERVICE_CONTAINER" --restart unless-stopped \
	--publish "127.0.0.1:$SERVICE_UPSTREAM_PORT:80" \
	"${docker_volume_args[@]}" \
	--env-file "$SERVICE_ENV_FILE" "$SERVICE_IMAGE" >/dev/null

echo "$SERVICE_NAME deployed at https://$SERVICE_DOMAIN using Caddy automatic HTTPS"