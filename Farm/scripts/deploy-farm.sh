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
pg_secret_env="${POSTGRES_PASSWORD:-}"
FARM_PG_PASS="${pg_secret_env:-postgres}"
FARM_PG_VOLUME="${FARM_PG_VOLUME:-eldervibe-farm-postgres-data}"

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

# ── 2. Configure Environment File for Farm Candidate ─────────────────────────
FARM_ENV_DIR="/etc/farm"
FARM_ENV_FILE="$FARM_ENV_DIR/farm.env"

write_env_content() {
    printf '%s=%s\n' "POSTGRES_HOST" "${FARM_PG_CONTAINER}"
    printf '%s=%s\n' "POSTGRES_PORT" "${FARM_PG_PORT}"
    printf '%s=%s\n' "POSTGRES_DB" "${FARM_PG_DB}"
    printf '%s=%s\n' "POSTGRES_USER" "${FARM_PG_USER}"
    printf '%s=%s\n' "$env_key_pw" "$FARM_PG_PASS"
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
export SERVICE_STATIC_CONTAINER_PATH=/app/wwwroot/hanging-post
export SERVICE_STATIC_ROOT=/var/lib/caddy/farm/hanging-post
export SERVICE_STATIC_PUBLIC_PREFIX=/hanging-post
export SERVICE_EXTRA_ARGS="--network $FARM_NETWORK --add-host=host.docker.internal:host-gateway"

exec bash "$DEPLOY_COMMON_SCRIPT"