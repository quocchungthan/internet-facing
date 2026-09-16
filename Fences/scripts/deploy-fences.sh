#!/usr/bin/env bash
set -Eeuo pipefail

: "${FENCES_IMAGE:?FENCES_IMAGE must name the image loaded on the VPS}"

if [[ "${EUID}" -ne 0 ]]; then
	echo "Fences deployment must run as root; configure VPS_USER as the root SSH account." >&2
	exit 1
fi

export SERVICE_NAME=fences
export SERVICE_DOMAIN=identity.eldervibe.dev
export SERVICE_UPSTREAM_PORT=5188
export SERVICE_CONTAINER=shuneo-fences
export SERVICE_IMAGE="$FENCES_IMAGE"
export SERVICE_ENV_FILE=/etc/shuneo/fences.env
export SERVICE_VOLUME=/var/lib/fences:/var/lib/fences
export SERVICE_SECRET_DIR=/etc/shuneo/fences-secrets

require_root_regular_file() {
	local path="$1"
	if [[ ! -f "$path" || -L "$path" || ! -s "$path" ]]; then
		echo "Expected nonempty regular file: $path" >&2
		exit 1
	fi
	if [[ "$(stat -c '%u:%g:%a' -- "$path")" != "0:0:600" ]]; then
		echo "Expected root-owned mode 600 file: $path" >&2
		exit 1
	fi
}

if [[ ! -d /etc/shuneo || -L /etc/shuneo ]]; then
	echo "Expected non-symlink directory: /etc/shuneo" >&2
	exit 1
fi
shuneo_parent_metadata="$(stat -c '%u:%g:%a' -- /etc/shuneo)"
IFS=: read -r shuneo_parent_owner shuneo_parent_group shuneo_parent_mode <<< "$shuneo_parent_metadata"
if [[ "$shuneo_parent_owner:$shuneo_parent_group" != "0:0" ]] || (( (8#$shuneo_parent_mode & 022) != 0 )); then
	echo "Expected root-owned directory with no group or other write permission: /etc/shuneo" >&2
	exit 1
fi
if [[ ! -d "$SERVICE_SECRET_DIR" || -L "$SERVICE_SECRET_DIR" ]]; then
	echo "Expected non-symlink directory: $SERVICE_SECRET_DIR" >&2
	exit 1
fi
if [[ "$(stat -c '%u:%g:%a' -- "$SERVICE_SECRET_DIR")" != "0:0:700" ]]; then
	echo "Expected root-owned mode 700 directory: $SERVICE_SECRET_DIR" >&2
	exit 1
fi
require_root_regular_file "$SERVICE_ENV_FILE"

if [[ ! -s "$SERVICE_ENV_FILE" ]]; then
	echo "Missing or empty Fences runtime environment file: $SERVICE_ENV_FILE" >&2
	exit 1
fi

for secret_file in oidc-signing.pfx oidc-encryption.pfx; do
	secret_path="$SERVICE_SECRET_DIR/$secret_file"
	require_root_regular_file "$secret_path"
done

: "${DEPLOY_COMMON_SCRIPT:?DEPLOY_COMMON_SCRIPT must identify the shared deployment script}"
exec bash "$DEPLOY_COMMON_SCRIPT"
