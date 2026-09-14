#!/usr/bin/env bash
set -Eeuo pipefail

: "${DEPLOY_COMMON_SCRIPT:?DEPLOY_COMMON_SCRIPT must identify the shared deployment script}"
: "${STORAGE_IMAGE:?STORAGE_IMAGE must identify the Storage image}"

export SERVICE_NAME=storage
export SERVICE_DOMAIN=storage.eldervibe.dev
export SERVICE_ALIASES=""
export SERVICE_UPSTREAM_PORT=8080
export SERVICE_CONTAINER=storage
export SERVICE_IMAGE="$STORAGE_IMAGE"
export SERVICE_ENV_FILE=/etc/storage/storage.env
export SERVICE_VOLUME=/srv/storage/data:/app/data

exec bash "$DEPLOY_COMMON_SCRIPT"
