#!/usr/bin/env bash
set -Eeuo pipefail

: "${DEPLOY_COMMON_SCRIPT:?DEPLOY_COMMON_SCRIPT must identify the shared deployment script}"
: "${MIND_IMAGE:?MIND_IMAGE must name the Farm.Mind image loaded on the VPS}"

# Farm.Mind shares eldervibe.dev with Farm through path routes and uses Farm's database and key ring,
# so Farm (scripts/deploy-farm.sh) must have been deployed first.
FARM_NETWORK="${FARM_NETWORK:-farm-network}"
FARM_PG_CONTAINER="${FARM_PG_CONTAINER:-eldervibe-farm-postgres}"
FARM_PG_PORT="${FARM_PG_PORT:-4554}"
FARM_PG_DB="${FARM_PG_DB:-aurafarming}"
FARM_PG_USER="${FARM_PG_USER:-postgres}"
FARM_ENV_DIR="${FARM_ENV_DIR:-/etc/farm}"
FARM_PG_PASS_FILE="$FARM_ENV_DIR/postgres.password"
MIND_ENV_FILE="$FARM_ENV_DIR/mind.env"
FARM_AUTH_AUTHORITY="${FARM_AUTH_AUTHORITY:-https://identity.eldervibe.dev}"
FARM_AUTH_CLIENT_ID="${FARM_AUTH_CLIENT_ID:-farm}"
FARM_AUTH_PUBLIC_BASE_URL="${FARM_AUTH_PUBLIC_BASE_URL:-https://eldervibe.dev}"
FARM_KEYS_VOLUME="${FARM_KEYS_VOLUME:-eldervibe-farm-keys}"
FARM_ROUTES_DIR="${FARM_ROUTES_DIR:-/etc/caddy/farm-routes}"

as_root() {
    if [[ "$EUID" -eq 0 ]]; then "$@"; else sudo -n -- "$@"; fi
}

if ! as_root test -s "$FARM_PG_PASS_FILE"; then
    echo "$FARM_PG_PASS_FILE is missing: deploy Farm (build-deploy-farm.yml) before Farm.Mind." >&2
    exit 1
fi
FARM_PG_PASS="$(as_root cat "$FARM_PG_PASS_FILE")"

write_env_content() {
    printf '%s=%s\n' "POSTGRES_HOST" "$FARM_PG_CONTAINER"
    printf '%s=%s\n' "POSTGRES_PORT" "$FARM_PG_PORT"
    printf '%s=%s\n' "POSTGRES_DB" "$FARM_PG_DB"
    printf '%s=%s\n' "POSTGRES_USER" "$FARM_PG_USER"
    printf '%s=%s\n' "POSTGRES_PASSWORD" "$FARM_PG_PASS"
    printf '%s=%s\n' "FarmAuth__Authority" "$FARM_AUTH_AUTHORITY"
    printf '%s=%s\n' "FarmAuth__ClientId" "$FARM_AUTH_CLIENT_ID"
    printf '%s=%s\n' "FarmAuth__PublicBaseUrl" "$FARM_AUTH_PUBLIC_BASE_URL"
    printf '%s=%s\n' "FarmAuth__DataProtectionKeysPath" "/keys"
}

write_env_content | as_root tee "$MIND_ENV_FILE" >/dev/null
as_root chmod 0600 "$MIND_ENV_FILE"

export SERVICE_NAME=farm-mind
export SERVICE_DOMAIN=eldervibe.dev
# 8180/8181 are Farm's.
export SERVICE_UPSTREAM_PORT=8182
export SERVICE_CANDIDATE_PORT=8183
export SERVICE_CONTAINER=eldervibe-farm-mind
export SERVICE_IMAGE="$MIND_IMAGE"
export SERVICE_ENV_FILE="$MIND_ENV_FILE"
export SERVICE_HEALTH_PATH=/healthz
export SERVICE_ROUTES_DIR="$FARM_ROUTES_DIR"
export SERVICE_ROUTE_PATHS="/mcp /mcp/* /api/memory /api/memory/* /.well-known/oauth-protected-resource /.well-known/oauth-protected-resource/*"
export SERVICE_VOLUME="$FARM_KEYS_VOLUME:/keys"
export SERVICE_EXTRA_ARGS="--network $FARM_NETWORK"

exec bash "$DEPLOY_COMMON_SCRIPT"
