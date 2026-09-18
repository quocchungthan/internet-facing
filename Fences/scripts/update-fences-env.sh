#!/usr/bin/env bash
set -euo pipefail

runtime_env_file="/etc/shuneo/fences.env"
secrets_dir="/etc/shuneo/fences-secrets"
service_container="shuneo-fences"
service_volume="/var/lib/fences:/var/lib/fences"
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
    echo "Usage: $0 --updates-file PATH [--restart]" >&2
}

restart=false
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
        echo "Malformed update line (expected KEY=value): $line" >&2
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

staging_dir="$(mktemp -d /etc/shuneo/.fences-env-update.XXXXXX)"
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
