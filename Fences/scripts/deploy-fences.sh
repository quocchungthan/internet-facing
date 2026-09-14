#!/usr/bin/env bash
set -Eeuo pipefail

: "${FENCES_IMAGE:?FENCES_IMAGE must name the image loaded on the VPS}"

export SERVICE_NAME=fences
export SERVICE_DOMAIN=identity.shuneo.com
export SERVICE_ALIASES=identity.eldervibe.dev
export SERVICE_UPSTREAM_PORT=5188
export SERVICE_CONTAINER=shuneo-fences
export SERVICE_IMAGE="$FENCES_IMAGE"
export SERVICE_ENV_FILE=/etc/shuneo/fences.env
export SERVICE_VOLUME=/var/lib/fences:/var/lib/fences

common_script="${DEPLOY_NGINX_COMMON_SCRIPT:-$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)/deployment/deploy-nginx-service.sh}"
exec bash "$common_script"
