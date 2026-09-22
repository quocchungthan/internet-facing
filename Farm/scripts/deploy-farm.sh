#!/usr/bin/env bash
set -Eeuo pipefail

: "${DEPLOY_COMMON_SCRIPT:?DEPLOY_COMMON_SCRIPT must identify the shared deployment script}"
: "${FARM_IMAGE:?FARM_IMAGE must name the Farm image loaded on the VPS}"

# exec spawns a fresh bash process for $DEPLOY_COMMON_SCRIPT, which only inherits exported vars.
export SERVICE_NAME=farm
export SERVICE_DOMAIN=eldervibe.dev
# 8080/8081 are already bound by an unrelated Seafile instance on this VPS; use 8180/8181 instead.
export SERVICE_UPSTREAM_PORT=8180
export SERVICE_CONTAINER=eldervibe-farm
export SERVICE_IMAGE="$FARM_IMAGE"
export SERVICE_ENV_FILE=/dev/null
export SERVICE_CANDIDATE_PORT=8181
export SERVICE_STATIC_CONTAINER_PATH=/app/wwwroot/hanging-post
export SERVICE_STATIC_ROOT=/var/lib/caddy/farm/hanging-post
export SERVICE_STATIC_PUBLIC_PREFIX=/hanging-post

exec bash "$DEPLOY_COMMON_SCRIPT"