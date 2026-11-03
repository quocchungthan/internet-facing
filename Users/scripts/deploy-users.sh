#!/usr/bin/env bash
set -Eeuo pipefail
umask 077

: "${USERS_IMAGE:?USERS_IMAGE must be an immutable GHCR digest}"
: "${DEPLOY_COMMON_SCRIPT:?DEPLOY_COMMON_SCRIPT is required}"
: "${USERS_LITELLM_NETWORK:=ilovecodex_default}"
: "${USERS_LITELLM_URL:=http://litellm:4000}"
: "${USERS_LITELLM_CONTAINER:=litellm-proxy}"
: "${USERS_POSTGRES_IMAGE:=postgres:17-alpine}"

[[ "$EUID" -eq 0 ]] || { echo 'Users deployment requires root.' >&2; exit 1; }
[[ "$USERS_IMAGE" =~ ^ghcr\.io/[a-z0-9._/-]+@sha256:[a-f0-9]{64}$ ]] || { echo 'An immutable GHCR image digest is required.' >&2; exit 1; }
[[ "$USERS_LITELLM_NETWORK" =~ ^[A-Za-z0-9][A-Za-z0-9_.-]*$ ]] || { echo 'Invalid LiteLLM network.' >&2; exit 1; }
[[ "$USERS_LITELLM_CONTAINER" =~ ^[A-Za-z0-9][A-Za-z0-9_.-]*$ ]] || { echo 'Invalid LiteLLM container.' >&2; exit 1; }
[[ "$USERS_LITELLM_URL" =~ ^https?://[A-Za-z0-9._:-]+(/[^[:space:]]*)?$ ]] || { echo 'Invalid LiteLLM URL.' >&2; exit 1; }
docker network inspect "$USERS_LITELLM_NETWORK" >/dev/null
docker pull "$USERS_IMAGE" >/dev/null

upstream_api_key="${MASTER_LLM_LITE_KEY:-}"
if [[ -z "$upstream_api_key" ]]; then
    while IFS= read -r entry; do
        case "$entry" in
            MASTER_LLM_LITE_KEY=*) upstream_api_key="${entry#*=}" ;;
            LITELLM_MASTER_KEY=*) [[ -n "$upstream_api_key" ]] || upstream_api_key="${entry#*=}" ;;
        esac
    done < <(docker container inspect "$USERS_LITELLM_CONTAINER" --format '{{range .Config.Env}}{{println .}}{{end}}')
fi
[[ -n "$upstream_api_key" && "$upstream_api_key" != *$'\n'* && "$upstream_api_key" != *$'\r'* ]] || {
    echo 'MASTER_LLM_LITE_KEY is unavailable on the VPS and was not found in the LiteLLM container.' >&2
    exit 1
}

config_dir=/etc/opensourcelab
env_file="$config_dir/users.env"
password_file="$config_dir/postgres.password"
[[ ! -L "$config_dir" && ! -L "$env_file" && ! -L "$password_file" ]] || { echo 'Symlinks are not permitted for Users configuration.' >&2; exit 1; }
install -d -m 0700 -o root -g root "$config_dir"
[[ ! -L "$config_dir" && "$(stat -c '%u:%g:%a' "$config_dir")" == 0:0:700 ]] || { echo 'Invalid Users configuration directory.' >&2; exit 1; }

if [[ ! -e "$password_file" ]]; then
    if docker container inspect opensourcelab-postgres >/dev/null 2>&1 || docker volume inspect opensourcelab-postgres-data >/dev/null 2>&1; then
        echo 'Existing database state has no password file; refusing to generate a replacement.' >&2; exit 1
    fi
    openssl rand -hex 32 > "$password_file"
    chmod 0600 "$password_file"
fi
[[ -f "$password_file" && ! -L "$password_file" && "$(stat -c '%u:%g:%a' "$password_file")" == 0:0:600 ]] || { echo 'Invalid database password file.' >&2; exit 1; }
password="$(cat "$password_file")"
[[ "$password" =~ ^[a-f0-9]{64}$ ]] || { echo 'Invalid stored database password.' >&2; exit 1; }

docker network inspect opensourcelab-db >/dev/null 2>&1 || docker network create --internal opensourcelab-db >/dev/null
[[ "$(docker network inspect opensourcelab-db --format '{{.Internal}}')" == true ]] || { echo 'Database network must be internal.' >&2; exit 1; }
if docker container inspect opensourcelab-postgres >/dev/null 2>&1; then
    [[ "$(docker inspect opensourcelab-postgres --format '{{.State.Running}}')" == true ]] || docker start opensourcelab-postgres >/dev/null
else
    docker run -d --name opensourcelab-postgres --network opensourcelab-db --restart unless-stopped \
        -e POSTGRES_DB=opensourcelab -e POSTGRES_USER=opensourcelab -e "POSTGRES_PASSWORD=$password" \
        -v opensourcelab-postgres-data:/var/lib/postgresql/data "$USERS_POSTGRES_IMAGE" >/dev/null
fi
for attempt in {1..60}; do
    if docker exec opensourcelab-postgres pg_isready -U opensourcelab -d opensourcelab >/dev/null 2>&1; then break; fi
    if [[ "$attempt" == 60 ]]; then echo 'PostgreSQL did not become ready.' >&2; exit 1; fi
    sleep 1
done

litellm_gateway="$(docker network inspect "$USERS_LITELLM_NETWORK" --format '{{(index .IPAM.Config 0).Gateway}}')"
db_gateway="$(docker network inspect opensourcelab-db --format '{{(index .IPAM.Config 0).Gateway}}')"
[[ "$litellm_gateway" =~ ^[0-9.]+$ && "$db_gateway" =~ ^[0-9.]+$ ]] || { echo 'Docker network gateways are unavailable.' >&2; exit 1; }
temporary_env="$(mktemp "$config_dir/.users-env.XXXXXX")"
cleanup() { rm -f -- "$temporary_env"; }
trap cleanup EXIT
{
    printf '%s\n' 'ASPNETCORE_ENVIRONMENT=Production' 'ASPNETCORE_HTTP_PORTS=8080'
    printf 'ConnectionStrings__Database=Host=opensourcelab-postgres;Database=opensourcelab;Username=opensourcelab;Password=%s\n' "$password"
    printf 'Gateway__ApiKey=%s\n' "$upstream_api_key"
    printf 'Gateway__Upstream=%s\n' "$USERS_LITELLM_URL"
    printf '%s\n' 'Gateway__PublicUrl=https://users.eldervibe.dev' 'AllowedHosts=users.eldervibe.dev;localhost;127.0.0.1'
    printf '%s\n' 'Identity__Authority=https://identity.eldervibe.dev' 'Identity__ClientId=opensourcelab'
    printf '%s\n' 'DataProtection__KeyPath=/var/lib/opensourcelab/keys'
    printf 'TrustedProxies__0=%s\nTrustedProxies__1=%s\n' "$litellm_gateway" "$db_gateway"
} > "$temporary_env"
chmod 0600 "$temporary_env"

docker run --rm --network opensourcelab-db --env-file "$temporary_env" "$USERS_IMAGE" --migrate
if ! docker volume inspect opensourcelab-keys >/dev/null 2>&1; then
    docker volume create opensourcelab-keys >/dev/null
    docker run --rm --user root -v opensourcelab-keys:/keys --entrypoint /bin/sh "$USERS_IMAGE" -c 'chown "$(id -u app):$(id -g app)" /keys && chmod 700 /keys'
fi
docker run --rm -v opensourcelab-keys:/keys --entrypoint /bin/sh "$USERS_IMAGE" -c 'test -w /keys' || { echo 'The key volume is not writable by the app user.' >&2; exit 1; }

export SERVICE_NAME=users SERVICE_DOMAIN=users.eldervibe.dev SERVICE_CONTAINER=opensourcelab-users
export SERVICE_UPSTREAM_PORT=5280 SERVICE_CANDIDATE_PORT=5281 SERVICE_CONTAINER_PORT=8080
export SERVICE_IMAGE="$USERS_IMAGE" SERVICE_ENV_FILE="$temporary_env"
export SERVICE_VOLUME=opensourcelab-keys:/var/lib/opensourcelab/keys
export SERVICE_EXTRA_ARGS="--network $USERS_LITELLM_NETWORK"
export SERVICE_ADDITIONAL_NETWORK=opensourcelab-db
export SERVICE_HEALTH_PATH=/health/ready
bash "$DEPLOY_COMMON_SCRIPT"
install -m 0600 -o root -g root "$temporary_env" "$env_file"
