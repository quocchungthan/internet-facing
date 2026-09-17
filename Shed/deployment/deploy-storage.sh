#!/usr/bin/env bash
set -Eeuo pipefail

: "${SERVICE_NAME:=storage}"
: "${SERVICE_DOMAIN:=storage.eldervibe.dev}"
: "${SERVICE_UPSTREAM_PORT:=8080}"
: "${STORAGE_DEPLOY_DIR:=/srv/storage}"
: "${CADDY_SITES_DIR:=/etc/caddy/sites}"
: "${CADDY_CONFIG:=/etc/caddy/Caddyfile}"
: "${CADDY_BIN:=caddy}"
: "${CADDY_SERVICE:=caddy}"
: "${DOCKER_BIN:=docker}"
: "${CADDY_LOCK_FILE:=${DEPLOY_LOCK_FILE:-/var/lock/deploy-caddy.lock}}"

echo "==> Deploying $SERVICE_NAME to $SERVICE_DOMAIN..."

# Prepare directories. Caddy must be able to traverse and read its site fragments.
mkdir -p "$STORAGE_DEPLOY_DIR/mysql" "$STORAGE_DEPLOY_DIR/data"
install -d -m 0755 "$CADDY_SITES_DIR"

# Copy compose configuration if provided
if [[ -n "${STORAGE_SOURCE_DIR:-}" ]] && [[ -f "$STORAGE_SOURCE_DIR/docker-compose.yml" ]]; then
    cp "$STORAGE_SOURCE_DIR/docker-compose.yml" "$STORAGE_DEPLOY_DIR/docker-compose.yml"
fi
if [[ -n "${STORAGE_SOURCE_DIR:-}" ]] && [[ -f "$STORAGE_SOURCE_DIR/conf/seahub_settings_template.py" ]]; then
    seahub_config_dir="$STORAGE_DEPLOY_DIR/data/seafile/conf"
    seahub_config="$seahub_config_dir/seahub_settings.py"
    install -d -m 0750 "$seahub_config_dir"
    if [[ ! -e "$seahub_config" ]]; then
        install -m 0640 "$STORAGE_SOURCE_DIR/conf/seahub_settings_template.py" "$seahub_config"
    fi
    sed -i '/^# BEGIN MANAGED STORAGE PROXY SETTINGS$/,/^# END MANAGED STORAGE PROXY SETTINGS$/d' "$seahub_config"
    cat >> "$seahub_config" <<PYTHON

# BEGIN MANAGED STORAGE PROXY SETTINGS
SERVICE_URL = 'https://${SERVICE_DOMAIN}'
FILE_SERVER_ROOT = 'https://${SERVICE_DOMAIN}/seafhttp'
CSRF_TRUSTED_ORIGINS = [SERVICE_URL]
SECURE_PROXY_SSL_HEADER = ('HTTP_X_FORWARDED_PROTO', 'https')
USE_X_FORWARDED_HOST = True
CSRF_COOKIE_SECURE = True
SESSION_COOKIE_SECURE = True
# END MANAGED STORAGE PROXY SETTINGS
PYTHON
    chmod 0640 "$seahub_config"
fi

# 1. Update Caddy site configuration
fragment="$CADDY_SITES_DIR/$SERVICE_DOMAIN.caddy"
fragment_tmp="$CADDY_SITES_DIR/.$SERVICE_DOMAIN.caddy.$$"
fragment_backup="$CADDY_SITES_DIR/.$SERVICE_DOMAIN.caddy.previous.$$"
state=absent

umask 077
mkdir -p "$(dirname "$CADDY_LOCK_FILE")"
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
$SERVICE_DOMAIN {
    # Forward all requests to seafile container (Nginx in container handles web + seafhttp)
    reverse_proxy 127.0.0.1:$SERVICE_UPSTREAM_PORT {
        header_up Host {host}
        header_up X-Real-IP {remote_host}
        header_up X-Forwarded-For {remote_host}
        header_up X-Forwarded-Host {host}
        header_up X-Forwarded-Proto https
    }
}
EOF
chmod 0644 "$fragment_tmp"
mv -f -- "$fragment_tmp" "$fragment"

if ! "$CADDY_BIN" validate --config "$CADDY_CONFIG" --adapter caddyfile; then
    if [[ "$state" == "file" ]]; then
        mv -f -- "$fragment_backup" "$fragment"
        chmod 0644 "$fragment"
    else
        rm -f -- "$fragment"
    fi
    echo "Caddy configuration validation failed; restored previous state" >&2
    exit 1
fi

if ! systemctl reload "$CADDY_SERVICE"; then
    echo "Caddy reload failed; service diagnostics:" >&2
    systemctl --no-pager --full status "$CADDY_SERVICE" >&2 || true
    journalctl --no-pager -u "$CADDY_SERVICE" -n 80 >&2 || true
    if [[ "$state" == "file" ]]; then
        mv -f -- "$fragment_backup" "$fragment"
        chmod 0644 "$fragment"
    else
        rm -f -- "$fragment"
    fi
    exit 1
fi

# 2. Run Seafile stack with Docker Compose
cd "$STORAGE_DEPLOY_DIR"
export SERVICE_DOMAIN
export SERVICE_UPSTREAM_PORT
export STORAGE_BASE_DIR="$STORAGE_DEPLOY_DIR"

if command -v docker-compose &>/dev/null; then
    COMPOSE_CMD="docker-compose"
else
    COMPOSE_CMD="$DOCKER_BIN compose"
fi

$COMPOSE_CMD pull || true
$COMPOSE_CMD up -d --remove-orphans

echo "==> Seafile Storage deployed successfully at https://$SERVICE_DOMAIN"
