#!/usr/bin/env bash
set -Eeuo pipefail

: "${FENCES_IMAGE:?FENCES_IMAGE must name the image loaded on the VPS}"

export SERVICE_NAME=fences
export SERVICE_DOMAIN=identity.eldervibe.dev
export SERVICE_UPSTREAM_PORT=5188
export SERVICE_CONTAINER=shuneo-fences
export SERVICE_IMAGE="$FENCES_IMAGE"
export SERVICE_ENV_FILE=/etc/shuneo/fences.env
export SERVICE_VOLUME=/var/lib/fences:/var/lib/fences

: "${DEPLOY_COMMON_SCRIPT:?DEPLOY_COMMON_SCRIPT must identify the shared deployment script}"
exec bash "$DEPLOY_COMMON_SCRIPT"
