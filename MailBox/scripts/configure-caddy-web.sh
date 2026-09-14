#!/usr/bin/env bash
set -Eeuo pipefail

: "${MAIL_HOSTNAME:?MAIL_HOSTNAME is required}"
: "${MAIL_DEPLOY_DIR:?MAIL_DEPLOY_DIR is required}"
: "${CADDY_SITES_DIR:=/etc/caddy/sites}"
: "${CADDY_CONFIG:=/etc/caddy/Caddyfile}"
: "${CADDY_BIN:=caddy}"
: "${CADDY_SERVICE:=caddy}"
: "${CADDY_LOCK_FILE:=/var/lock/deploy-caddy.lock}"

cert_dir="$MAIL_DEPLOY_DIR/runtime/letsencrypt/live/$MAIL_HOSTNAME"
[[ -s "$cert_dir/fullchain.pem" && -s "$cert_dir/privkey.pem" ]] || {
	echo "Mail certificate is missing: $cert_dir" >&2
	exit 1
}

mkdir -p "$CADDY_SITES_DIR" "$(dirname -- "$CADDY_LOCK_FILE")"
exec 9>"$CADDY_LOCK_FILE"
flock -n 9 || { echo "Another Caddy deployment is already running" >&2; exit 1; }

fragment="$CADDY_SITES_DIR/$MAIL_HOSTNAME.caddy"
backup="$CADDY_SITES_DIR/.$MAIL_HOSTNAME.caddy.previous.$$"
tmp="$CADDY_SITES_DIR/.$MAIL_HOSTNAME.caddy.$$"
had_previous=false
if [[ -f "$fragment" ]]; then
	cp -- "$fragment" "$backup"
	had_previous=true
fi
cleanup() { rm -f "$tmp" "$backup"; }
trap cleanup EXIT

cat > "$tmp" <<EOF
$MAIL_HOSTNAME {
	tls $cert_dir/fullchain.pem $cert_dir/privkey.pem
	reverse_proxy 127.0.0.1:8000
}
EOF
mv -f -- "$tmp" "$fragment"

rollback() {
	if [[ "$had_previous" == true ]]; then mv -f -- "$backup" "$fragment"; else rm -f "$fragment"; fi
	"$CADDY_BIN" validate --config "$CADDY_CONFIG" --adapter caddyfile >/dev/null
	systemctl reload "$CADDY_SERVICE" || true
}

if ! "$CADDY_BIN" validate --config "$CADDY_CONFIG" --adapter caddyfile; then
	rollback
	exit 1
fi
if ! systemctl reload "$CADDY_SERVICE"; then
	rollback
	exit 1
fi

echo "Caddy web route configured for $MAIL_HOSTNAME"