#!/usr/bin/env bash
set -Eeuo pipefail

: "${DEPLOY_COMMON_SCRIPT:?DEPLOY_COMMON_SCRIPT must identify the shared deployment script}"
: "${FARM_IMAGE:?FARM_IMAGE must name the Farm image loaded on the VPS}"

SERVICE_NAME=farm
SERVICE_DOMAIN=eldervibe.dev
SERVICE_UPSTREAM_PORT=8080
SERVICE_CONTAINER=eldervibe-farm
SERVICE_IMAGE="$FARM_IMAGE"
SERVICE_ENV_FILE=/dev/null
SERVICE_CANDIDATE_PORT=8081
SERVICE_STATIC_CONTAINER_PATH=/app/wwwroot/hanging-post
SERVICE_STATIC_ROOT=/var/lib/caddy/farm/hanging-post
SERVICE_STATIC_PUBLIC_PREFIX=/hanging-post

exec bash "$DEPLOY_COMMON_SCRIPT"