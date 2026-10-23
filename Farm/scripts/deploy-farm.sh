#!/usr/bin/env bash
set -Eeuo pipefail

: "${DEPLOY_COMMON_SCRIPT:?DEPLOY_COMMON_SCRIPT must identify the shared deployment script}"
: "${FARM_IMAGE:?FARM_IMAGE must name the Farm image loaded on the VPS}"

# ── 1. Ensure Docker Network & AuraFarming PostgreSQL on Port 4554 ─────────────
FARM_NETWORK="${FARM_NETWORK:-farm-network}"
FARM_PG_CONTAINER="${FARM_PG_CONTAINER:-eldervibe-farm-postgres}"
FARM_PG_PORT="${FARM_PG_PORT:-4554}"
FARM_PG_DB="${FARM_PG_DB:-aurafarming}"
FARM_PG_USER="${FARM_PG_USER:-postgres}"
FARM_PG_VOLUME="${FARM_PG_VOLUME:-eldervibe-farm-postgres-data}"
FARM_ENV_DIR="${FARM_ENV_DIR:-/etc/farm}"
FARM_PG_PASS_FILE="$FARM_ENV_DIR/postgres.password"

docker_bin="docker"
if ! command -v docker >/dev/null 2>&1 || ! docker info >/dev/null 2>&1; then
    if command -v sudo >/dev/null 2>&1; then
        docker_bin="sudo -n docker"
    fi
fi

if ! $docker_bin network inspect "$FARM_NETWORK" >/dev/null 2>&1; then
    echo "Creating user-defined Docker network: $FARM_NETWORK..."
    $docker_bin network create "$FARM_NETWORK" >/dev/null 2>&1 || true
fi

as_root() {
    if [[ "$EUID" -eq 0 ]]; then "$@"; else sudo -n -- "$@"; fi
}

# The database password lives only on the VPS (/etc/farm/postgres.password, root-only). It is generated on the first
# deploy and then reused, so no GitHub secret is needed. POSTGRES_PASSWORD in the environment still wins when set.
as_root install -d -m 0755 "$FARM_ENV_DIR"
password_is_new=false
if [[ -n "${POSTGRES_PASSWORD:-}" ]]; then
    FARM_PG_PASS="$POSTGRES_PASSWORD"
    if ! as_root test -s "$FARM_PG_PASS_FILE" || [[ "$(as_root cat "$FARM_PG_PASS_FILE")" != "$FARM_PG_PASS" ]]; then
        password_is_new=true
    fi
elif as_root test -s "$FARM_PG_PASS_FILE"; then
    FARM_PG_PASS="$(as_root cat "$FARM_PG_PASS_FILE")"
else
    FARM_PG_PASS="$(head -c 24 /dev/urandom | od -An -tx1 | tr -d ' \n')"
    password_is_new=true
fi

env_key_pw="POSTGRES_PASSWORD"
if ! $docker_bin ps --format '{{.Names}}' | grep -q "^${FARM_PG_CONTAINER}\$"; then
    if $docker_bin ps -a --format '{{.Names}}' | grep -q "^${FARM_PG_CONTAINER}\$"; then
        echo "Starting existing Farm Postgres container ($FARM_PG_CONTAINER)..."
        $docker_bin start "$FARM_PG_CONTAINER"
    else
        echo "Provisioning Farm Postgres container ($FARM_PG_CONTAINER) on port $FARM_PG_PORT..."
        $docker_bin run -d --name "$FARM_PG_CONTAINER" \
            --network "$FARM_NETWORK" \
            --restart unless-stopped \
            -p "127.0.0.1:${FARM_PG_PORT}:4554" \
            -e POSTGRES_DB="$FARM_PG_DB" \
            -e POSTGRES_USER="$FARM_PG_USER" \
            -e "$env_key_pw=$FARM_PG_PASS" \
            -v "$FARM_PG_VOLUME:/var/lib/postgresql/data" \
            postgres:17-alpine -c port=4554
    fi
fi

# The container only reads POSTGRES_PASSWORD when it is first created, so an existing database (created with the old
# default) must be changed explicitly. The local socket inside the image is trusted, so no old password is needed.
if [[ "$password_is_new" == true ]]; then
    for attempt in {1..30}; do
        $docker_bin exec "$FARM_PG_CONTAINER" pg_isready -p "$FARM_PG_PORT" -U "$FARM_PG_USER" >/dev/null 2>&1 && break
        sleep 1
    done
    sql_password="${FARM_PG_PASS//\'/\'\'}"
    printf "ALTER USER \"%s\" WITH PASSWORD '%s';\n" "$FARM_PG_USER" "$sql_password" \
        | $docker_bin exec -i "$FARM_PG_CONTAINER" psql -v ON_ERROR_STOP=1 -p "$FARM_PG_PORT" -U "$FARM_PG_USER" -d "$FARM_PG_DB" >/dev/null
    printf '%s\n' "$FARM_PG_PASS" | as_root tee "$FARM_PG_PASS_FILE" >/dev/null
    as_root chmod 0600 "$FARM_PG_PASS_FILE"
fi

# ── 2. Configure Environment File for Farm Candidate ─────────────────────────
FARM_ENV_FILE="$FARM_ENV_DIR/farm.env"

# Sign-in through Fences. The key ring volume is shared with Farm.Mind so the session cookie works in both.
FARM_AUTH_AUTHORITY="${FARM_AUTH_AUTHORITY:-https://identity.eldervibe.dev}"
FARM_AUTH_CLIENT_ID="${FARM_AUTH_CLIENT_ID:-farm}"
FARM_AUTH_PUBLIC_BASE_URL="${FARM_AUTH_PUBLIC_BASE_URL:-https://eldervibe.dev}"
FARM_KEYS_VOLUME="${FARM_KEYS_VOLUME:-eldervibe-farm-keys}"
FARM_ROUTES_DIR="${FARM_ROUTES_DIR:-/etc/caddy/farm-routes}"

write_env_content() {
    printf '%s=%s\n' "POSTGRES_HOST" "${FARM_PG_CONTAINER}"
    printf '%s=%s\n' "POSTGRES_PORT" "${FARM_PG_PORT}"
    printf '%s=%s\n' "POSTGRES_DB" "${FARM_PG_DB}"
    printf '%s=%s\n' "POSTGRES_USER" "${FARM_PG_USER}"
    printf '%s=%s\n' "$env_key_pw" "$FARM_PG_PASS"
    printf '%s=%s\n' "FarmAuth__Authority" "$FARM_AUTH_AUTHORITY"
    printf '%s=%s\n' "FarmAuth__ClientId" "$FARM_AUTH_CLIENT_ID"
    printf '%s=%s\n' "FarmAuth__PublicBaseUrl" "$FARM_AUTH_PUBLIC_BASE_URL"
    printf '%s=%s\n' "FarmAuth__DataProtectionKeysPath" "/keys"
    printf '%s=Host=%s;Port=%s;Database=%s;Username=%s;Password=%s;\n' \
        "ConnectionStrings__AuraFarming" "${FARM_PG_CONTAINER}" "${FARM_PG_PORT}" "${FARM_PG_DB}" "${FARM_PG_USER}" "$FARM_PG_PASS"
}

if [[ "$EUID" -eq 0 ]]; then
    install -d -m 0755 "$FARM_ENV_DIR"
    write_env_content > "$FARM_ENV_FILE"
    chmod 0600 "$FARM_ENV_FILE"
else
    if sudo -n -l >/dev/null 2>&1; then
        sudo -n install -d -m 0755 "$FARM_ENV_DIR"
        write_env_content | sudo -n tee "$FARM_ENV_FILE" >/dev/null
        sudo -n chmod 0600 "$FARM_ENV_FILE"
    else
        FARM_ENV_FILE=$(mktemp)
        write_env_content > "$FARM_ENV_FILE"
        chmod 0600 "$FARM_ENV_FILE"
    fi
fi

# ── 3. Export Service Parameters and Dispatch to Common Deployer ──────────────
export SERVICE_NAME=farm
export SERVICE_DOMAIN=eldervibe.dev
# 8080/8081 are already bound by an unrelated Seafile instance on this VPS; use 8180/8181 instead.
export SERVICE_UPSTREAM_PORT=8180
export SERVICE_CONTAINER=eldervibe-farm
export SERVICE_IMAGE="$FARM_IMAGE"
export SERVICE_ENV_FILE="$FARM_ENV_FILE"
export SERVICE_CANDIDATE_PORT=8181
# Farm owns the domain; Farm.Mind adds its /mcp and /api/memory routes by dropping a file in this directory.
export SERVICE_IMPORT_ROUTES_DIR="$FARM_ROUTES_DIR"
export SERVICE_VOLUME="$FARM_KEYS_VOLUME:/keys"
export SERVICE_EXTRA_ARGS="--network $FARM_NETWORK --add-host=host.docker.internal:host-gateway"

exec bash "$DEPLOY_COMMON_SCRIPT"