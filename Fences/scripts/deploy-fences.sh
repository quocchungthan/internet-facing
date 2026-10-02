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
if [[ -n "${FENCES_DEPLOY_DIR:-}" ]]; then
	if [[ ! "$FENCES_DEPLOY_DIR" =~ ^/[A-Za-z0-9_-][A-Za-z0-9._-]*(/[A-Za-z0-9_-][A-Za-z0-9._-]*)*$ ]]; then
		echo "FENCES_DEPLOY_DIR must be an absolute path without empty, dot or parent segments." >&2
		exit 1
	fi
	export SERVICE_ENV_FILE="$FENCES_DEPLOY_DIR/fences.env"
	export SERVICE_VOLUME="$FENCES_DEPLOY_DIR/data:/var/lib/fences"
	export SERVICE_SECRET_DIR="$FENCES_DEPLOY_DIR/fences-secrets"
	config_parent="$FENCES_DEPLOY_DIR"
	if [[ -e /etc/shuneo/fences.env || -L /etc/shuneo/fences.env || -e /etc/shuneo/fences-secrets || -L /etc/shuneo/fences-secrets || -e /var/lib/fences || -L /var/lib/fences ]]; then
		echo "Legacy Fences material exists; migrate it before deploying with FENCES_DEPLOY_DIR." >&2
		exit 1
	fi
else
	export SERVICE_ENV_FILE=/etc/shuneo/fences.env
	export SERVICE_VOLUME=/var/lib/fences:/var/lib/fences
	export SERVICE_SECRET_DIR=/etc/shuneo/fences-secrets
	config_parent=/etc/shuneo
fi

if [[ -n "${FENCES_DEPLOY_DIR:-}" ]]; then
	path="$config_parent"
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

if [[ ! -d "$config_parent" || -L "$config_parent" ]]; then
	echo "Expected non-symlink directory: $config_parent" >&2
	exit 1
fi
shuneo_parent_metadata="$(stat -c '%u:%g:%a' -- "$config_parent")"
IFS=: read -r shuneo_parent_owner shuneo_parent_group shuneo_parent_mode <<< "$shuneo_parent_metadata"
if [[ "$shuneo_parent_owner:$shuneo_parent_group" != "0:0" ]] || (( (8#$shuneo_parent_mode & 022) != 0 )); then
	echo "Expected root-owned directory with no group or other write permission: $config_parent" >&2
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

if [[ -n "${FENCES_DEPLOY_DIR:-}" ]]; then
	data_dir="$FENCES_DEPLOY_DIR/data"
	if [[ ! -d "$data_dir" || -L "$data_dir" || "$(stat -c '%u:%g:%a' -- "$data_dir")" != "0:0:700" ]]; then
		echo "Expected root-owned mode 700 data directory: $data_dir" >&2
		exit 1
	fi
fi

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
