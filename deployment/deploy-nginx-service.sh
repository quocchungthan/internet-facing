#!/usr/bin/env bash
set -Eeuo pipefail

: "${SERVICE_NAME:?SERVICE_NAME must identify the service}"
: "${SERVICE_DOMAIN:?SERVICE_DOMAIN must identify the public domain}"
: "${SERVICE_ALIASES:=}"
: "${SERVICE_UPSTREAM_PORT:?SERVICE_UPSTREAM_PORT must identify the container port}"
: "${SERVICE_CONTAINER:?SERVICE_CONTAINER must identify the container}"
: "${SERVICE_IMAGE:?SERVICE_IMAGE must name the image loaded on the VPS}"
: "${SERVICE_ENV_FILE:?SERVICE_ENV_FILE must identify the Docker environment file}"
: "${SERVICE_VOLUME:=}"
: "${NGINX_SITES_AVAILABLE:=/etc/nginx/sites-available}"
: "${NGINX_SITES_ENABLED:=/etc/nginx/sites-enabled}"
: "${CERTBOT_WEBROOT:=/var/www/certbot}"
: "${CERTBOT_BIN:=certbot}"
: "${CERTBOT_EMAIL:=}"
: "${NGINX_SERVICE:=nginx}"
: "${DEPLOY_LOCK_FILE:=/var/lock/deploy-${SERVICE_NAME}.lock}"
: "${DOCKER_BIN:=docker}"
: "${NGINX_BIN:=nginx}"
: "${OPENSSL_BIN:=openssl}"
: "${CERTBOT_CONFIG_DIR:=/etc/letsencrypt}"

[[ "$SERVICE_DOMAIN" != */* ]] || { echo "SERVICE_DOMAIN must not contain a slash" >&2; exit 2; }

server_names="$SERVICE_DOMAIN${SERVICE_ALIASES:+ $SERVICE_ALIASES}"
certbot_domains=(--domain "$SERVICE_DOMAIN")
for alias in $SERVICE_ALIASES; do
    certbot_domains+=(--domain "$alias")
done

renewal_dir="$CERTBOT_CONFIG_DIR/renewal"
live_dir="$CERTBOT_CONFIG_DIR/live"
managed_link="$NGINX_SITES_ENABLED/$SERVICE_DOMAIN.conf"
http_config="$NGINX_SITES_AVAILABLE/$SERVICE_DOMAIN.http.conf"
https_config="$NGINX_SITES_AVAILABLE/$SERVICE_DOMAIN.https.conf"
state_backup="$NGINX_SITES_AVAILABLE/.$SERVICE_DOMAIN.previous.$$.conf"
state_kind=absent
state_target=
state_restored=false
state_captured=false

umask 077
mkdir -p "$(dirname "$DEPLOY_LOCK_FILE")" "$NGINX_SITES_AVAILABLE" "$NGINX_SITES_ENABLED" "$CERTBOT_WEBROOT/.well-known/acme-challenge"
exec 9>"$DEPLOY_LOCK_FILE"
flock -n 9 || { echo "Another $SERVICE_NAME deployment is already running" >&2; exit 1; }

restore_previous_config() {
    [[ "$state_captured" == true ]] || return 0
    [[ "$state_restored" == true ]] && return 0
    rm -f "$managed_link"
    case "$state_kind" in
        symlink) ln -s "$state_target" "$managed_link" ;;
        file) mv "$state_backup" "$managed_link" ;;
    esac
    state_restored=true
}

cleanup() {
    local exit_code=$?
    if (( exit_code != 0 )); then
        restore_previous_config || true
        "$NGINX_BIN" -t >/dev/null 2>&1 && systemctl reload "$NGINX_SERVICE" || true
    fi
    rm -f "$http_config" "$https_config"
    if [[ -f "$state_backup" ]]; then
        mv "$state_backup" "$NGINX_SITES_AVAILABLE/$SERVICE_DOMAIN.previous.$(date +%s).conf"
    fi
    exit "$exit_code"
}
trap cleanup EXIT

stage_config() {
    local source_config="$1"
    if [[ "$state_captured" == false ]]; then
        if [[ -L "$managed_link" ]]; then
            state_kind=symlink
            state_target=$(readlink "$managed_link")
            rm "$managed_link"
        elif [[ -f "$managed_link" ]]; then
            state_kind=file
            mv "$managed_link" "$state_backup"
        fi
        state_captured=true
    else
        rm -f "$managed_link"
    fi
    ln -s "$source_config" "$managed_link"
    "$NGINX_BIN" -t
}

certificate_covers_domain() {
    local certificate="$1"
    local san_names domain
    san_names=$("$OPENSSL_BIN" x509 -in "$certificate" -noout -ext subjectAltName 2>/dev/null) || return 1
    for domain in $SERVICE_DOMAIN $SERVICE_ALIASES; do
        "$OPENSSL_BIN" x509 -in "$certificate" -noout -checkhost "$domain" >/dev/null 2>&1 || return 1
        printf '%s\n' "$san_names" | grep -Eq "DNS:${domain}([,[:space:]]|$)" || return 1
    done
}

discover_lineage() {
    local renewal_file lineage certificate
    [[ -d "$renewal_dir" ]] || return 0
    for renewal_file in "$renewal_dir"/*.conf; do
        [[ -f "$renewal_file" ]] || continue
        lineage=$(basename "$renewal_file" .conf)
        certificate="$live_dir/$lineage/cert.pem"
        [[ -s "$certificate" ]] || continue
        if certificate_covers_domain "$certificate"; then
            printf '%s\n' "$lineage"
            return 0
        fi
    done
}

lineage=$(discover_lineage)
if [[ -z "$lineage" ]]; then
    lineage="$SERVICE_DOMAIN"
    [[ -n "$CERTBOT_EMAIL" ]] || { echo "CERTBOT_EMAIL is required for a new certificate lineage" >&2; exit 2; }
fi

cat > "$http_config" <<EOF
server {
    listen 80;
    listen [::]:80;
    server_name $server_names;

    location ^~ /.well-known/acme-challenge/ {
        root $CERTBOT_WEBROOT;
        try_files \$uri =404;
    }

    location / {
        proxy_pass http://127.0.0.1:$SERVICE_UPSTREAM_PORT;
        proxy_set_header Host \$host;
        proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto \$scheme;
    }
}
EOF
stage_config "$http_config"
systemctl reload "$NGINX_SERVICE"

if [[ -f "$renewal_dir/$lineage.conf" ]]; then
    certificate="$live_dir/$lineage/cert.pem"
    if certificate_covers_domain "$certificate"; then
        "$CERTBOT_BIN" renew --config-dir "$CERTBOT_CONFIG_DIR" --cert-name "$lineage" --webroot-path "$CERTBOT_WEBROOT" --non-interactive
    else
        "$CERTBOT_BIN" certonly --config-dir "$CERTBOT_CONFIG_DIR" --webroot --webroot-path "$CERTBOT_WEBROOT" \
            --cert-name "$lineage" "${certbot_domains[@]}" --expand --non-interactive
    fi
else
    "$CERTBOT_BIN" certonly --config-dir "$CERTBOT_CONFIG_DIR" --webroot --webroot-path "$CERTBOT_WEBROOT" \
        --cert-name "$lineage" "${certbot_domains[@]}" \
        --email "$CERTBOT_EMAIL" --agree-tos --non-interactive
fi

lineage=$(discover_lineage)
[[ -n "$lineage" ]] || { echo "No Certbot lineage covers $SERVICE_DOMAIN" >&2; exit 1; }
certificate="$live_dir/$lineage/cert.pem"
private_key="$live_dir/$lineage/privkey.pem"
[[ -s "$certificate" && -s "$private_key" ]] || { echo "Certificate files are missing for lineage $lineage" >&2; exit 1; }
certificate_covers_domain "$certificate"

cat > "$https_config" <<EOF
server {
    listen 80;
    listen [::]:80;
    server_name $server_names;

    location ^~ /.well-known/acme-challenge/ {
        root $CERTBOT_WEBROOT;
        try_files \$uri =404;
    }

    location / { return 308 https://\$host\$request_uri; }
}

server {
    listen 443 ssl;
    listen [::]:443 ssl;
    server_name $server_names;
    ssl_certificate $certificate;
    ssl_certificate_key $private_key;

    location / {
        proxy_pass http://127.0.0.1:$SERVICE_UPSTREAM_PORT;
        proxy_set_header Host \$host;
        proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto https;
    }
}
EOF
stage_config "$https_config"
systemctl reload "$NGINX_SERVICE"

"$DOCKER_BIN" rm --force "$SERVICE_CONTAINER" >/dev/null 2>&1 || true
docker_volume_args=()
if [[ -n "$SERVICE_VOLUME" ]]; then
    docker_volume_args+=(--volume "$SERVICE_VOLUME")
fi
"$DOCKER_BIN" run --detach --name "$SERVICE_CONTAINER" --restart unless-stopped \
    --publish "127.0.0.1:$SERVICE_UPSTREAM_PORT:80" \
    "${docker_volume_args[@]}" \
    --env-file "$SERVICE_ENV_FILE" "$SERVICE_IMAGE" >/dev/null
echo "$SERVICE_NAME deployed at https://$SERVICE_DOMAIN using Certbot lineage $lineage"