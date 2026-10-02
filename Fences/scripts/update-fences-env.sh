#!/usr/bin/env bash
set -euo pipefail

if [[ -n "${FENCES_DEPLOY_DIR:-}" ]]; then
    if [[ ! "$FENCES_DEPLOY_DIR" =~ ^/[A-Za-z0-9_-][A-Za-z0-9._-]*(/[A-Za-z0-9_-][A-Za-z0-9._-]*)*$ ]]; then
        echo "FENCES_DEPLOY_DIR must be an absolute path without dot or parent segments." >&2
        exit 1
    fi
    config_dir="$FENCES_DEPLOY_DIR"
    data_dir="$FENCES_DEPLOY_DIR/data"
    if [[ -e /etc/shuneo/fences.env || -L /etc/shuneo/fences.env || -e /etc/shuneo/fences-secrets || -L /etc/shuneo/fences-secrets || -e /var/lib/fences || -L /var/lib/fences ]]; then
        echo "Legacy Fences material exists; migrate it before updating with FENCES_DEPLOY_DIR." >&2
        exit 1
    fi
else
    config_dir=/etc/shuneo
    data_dir=/var/lib/fences
fi
runtime_env_file="$config_dir/fences.env"
secrets_dir="$config_dir/fences-secrets"
service_container="shuneo-fences"
service_volume="$data_dir:/var/lib/fences"
service_upstream_port=5188

# Provisioner-managed values must only change via a planned certificate/database migration.
protected_keys=(
    IdentityApp__OidcSigningCertificatePath
    IdentityApp__OidcSigningCertificatePassword
    IdentityApp__OidcEncryptionCertificatePath
    IdentityApp__OidcEncryptionCertificatePassword
    IdentityApp__IdentityDatabasePath
    IdentityApp__DataProtectionKeysPath
)

staging_dir=""
new_env_file=""
cleanup() {
    [[ -n "$staging_dir" ]] && rm -rf -- "$staging_dir"
}
trap cleanup EXIT

usage() {
    echo "Usage: $0 --updates-file PATH [--replace-oidc] [--restart]" >&2
}

restart=false
replace_oidc=false
updates_file=""
while [[ "$#" -gt 0 ]]; do
    case "$1" in
        --updates-file)
            [[ -n "${2:-}" ]] || { usage; exit 1; }
            updates_file="$2"
            shift 2
            ;;
        --restart)
            restart=true
            shift
            ;;
        --replace-oidc)
            replace_oidc=true
            shift
            ;;
        *)
            usage
            exit 1
            ;;
    esac
done
[[ -n "$updates_file" ]] || { usage; exit 1; }

if [[ "${EUID}" -ne 0 ]]; then
    echo "Run this script as root; it edits a root-owned runtime environment file." >&2
    exit 1
fi

if [[ -n "${FENCES_DEPLOY_DIR:-}" ]]; then
    path="$config_dir"
    while [[ "$path" != / ]]; do
        if [[ ! -d "$path" || -L "$path" ]]; then
            echo "Expected non-symlink directory: $path" >&2
            exit 1
        fi
        metadata="$(stat -c '%u:%g:%a' -- "$path")"
        IFS=: read -r owner group mode <<< "$metadata"
        if [[ "$owner:$group" != 0:0 ]] || (( (8#$mode & 022) != 0 )); then
            echo "Expected root-owned directory with no group or other write permission: $path" >&2
            exit 1
        fi
        path="$(dirname -- "$path")"
    done
fi

require_root_regular_file_600() {
    local path="$1" label="$2"
    if [[ ! -f "$path" || -L "$path" ]]; then
        echo "Expected non-symlink regular file ($label): $path" >&2
        exit 1
    fi
    if [[ "$(stat -c '%u:%g:%a' -- "$path")" != "0:0:600" ]]; then
        echo "Expected root-owned mode 600 file ($label): $path" >&2
        exit 1
    fi
}

require_root_regular_file_600 "$runtime_env_file" "Fences runtime environment"
require_root_regular_file_600 "$updates_file" "updates input"
[[ -s "$updates_file" ]] || { echo "Updates input must not be empty." >&2; exit 1; }

if [[ ! -d "$config_dir" || -L "$config_dir" ]]; then
    echo "Expected non-symlink directory: $config_dir" >&2
    exit 1
fi
config_metadata="$(stat -c '%u:%g:%a' -- "$config_dir")"
IFS=: read -r config_owner config_group config_mode <<< "$config_metadata"
if [[ "$config_owner:$config_group" != "0:0" ]] || (( (8#$config_mode & 022) != 0 )); then
    echo "Expected root-owned directory with no group or other write permission: $config_dir" >&2
    exit 1
fi
if [[ ! -d "$data_dir" || -L "$data_dir" || "$(stat -c '%u:%g:%a' -- "$data_dir")" != "0:0:700" ]]; then
    echo "Expected root-owned mode 700 data directory: $data_dir" >&2
    exit 1
fi

if [[ ! -d "$secrets_dir" || -L "$secrets_dir" ]]; then
    echo "Expected non-symlink directory: $secrets_dir" >&2
    exit 1
fi
if [[ "$(stat -c '%u:%g:%a' -- "$secrets_dir")" != "0:0:700" ]]; then
    echo "Expected root-owned mode 700 directory: $secrets_dir" >&2
    exit 1
fi

env_metadata="$(stat -c '%a' -- "$runtime_env_file")"

declare -A updates
ordered_keys=()
while IFS= read -r line || [[ -n "$line" ]]; do
    line="${line%$'\r'}"
    [[ -z "$line" ]] && continue
    if [[ ! "$line" =~ ^[A-Za-z_][A-Za-z0-9_]*=.*$ ]]; then
        echo "Malformed update line (expected KEY=value)." >&2
        exit 1
    fi
    key="${line%%=*}"
    value="${line#*=}"
    [[ -n "$value" ]] || { echo "Update value for $key must not be blank." >&2; exit 1; }
    for protected in "${protected_keys[@]}"; do
        if [[ "$key" == "$protected" ]]; then
            echo "Refusing to change provisioner-managed key: $key" >&2
            exit 1
        fi
    done
    if [[ -n "${updates[$key]+set}" ]]; then
        echo "Duplicate update key: $key" >&2
        exit 1
    fi
    updates["$key"]="$value"
    ordered_keys+=("$key")
done < "$updates_file"
[[ "${#ordered_keys[@]}" -gt 0 ]] || { echo "No valid updates found in input." >&2; exit 1; }

if [[ "$replace_oidc" == true ]]; then
    declare -A desired_clients=()
    has_desired_client=false
    for key in "${ordered_keys[@]}"; do
        if [[ "$key" =~ ^IdentityApp__OidcClients__[0-9]+__ClientId$ ]]; then
            desired_clients["${updates[$key]}"]=1
            has_desired_client=true
        fi
    done
    [[ "$has_desired_client" == true ]] || { echo "Reconciliation requires at least one OIDC client." >&2; exit 1; }
    while IFS= read -r line || [[ -n "$line" ]]; do
        if [[ "$line" =~ ^IdentityApp__OidcClients__[0-9]+__ClientId=(.*)$ ]]; then
            if [[ -z "${desired_clients[${BASH_REMATCH[1]}]+set}" ]]; then
                echo "Removing an existing OIDC client requires an explicit database migration." >&2
                exit 1
            fi
        fi
    done < "$runtime_env_file"
fi

staging_dir="$(mktemp -d "$config_dir/.fences-env-update.XXXXXX")"
chmod 700 -- "$staging_dir"
new_env_file="$staging_dir/fences.env"
: > "$new_env_file"

declare -A applied
while IFS= read -r line || [[ -n "$line" ]]; do
    existing_key=""
    if [[ "$line" =~ ^([A-Za-z_][A-Za-z0-9_]*)= ]]; then
        existing_key="${BASH_REMATCH[1]}"
    fi
    if [[ -n "$existing_key" && -n "${updates[$existing_key]+set}" ]]; then
        printf '%s=%s\n' "$existing_key" "${updates[$existing_key]}" >> "$new_env_file"
        applied["$existing_key"]=1
    elif [[ "$replace_oidc" == true && "$existing_key" == IdentityApp__OidcClients__* ]]; then
        continue
    else
        printf '%s\n' "$line" >> "$new_env_file"
    fi
done < "$runtime_env_file"

for key in "${ordered_keys[@]}"; do
    if [[ -z "${applied[$key]+set}" ]]; then
        printf '%s=%s\n' "$key" "${updates[$key]}" >> "$new_env_file"
    fi
done

chown root:root -- "$new_env_file"
chmod "$env_metadata" -- "$new_env_file"
mv -f -- "$new_env_file" "$runtime_env_file"
rm -rf -- "$staging_dir"
staging_dir=""

echo "Updated keys: ${ordered_keys[*]}"

if [[ "$restart" == true ]]; then
    command -v docker >/dev/null 2>&1 || { echo "docker is required to restart Fences." >&2; exit 1; }
    image="$(docker inspect --format '{{.Config.Image}}' "$service_container")"
    docker rm --force "$service_container" >/dev/null
    docker run --detach --name "$service_container" --restart unless-stopped \
        --publish "127.0.0.1:$service_upstream_port:80" \
        --volume "$service_volume" \
        --mount "type=bind,source=$secrets_dir,destination=/run/secrets,readonly" \
        --env-file "$runtime_env_file" "$image" >/dev/null
    echo "Restarted $service_container using image $image"
fi

exit 0
