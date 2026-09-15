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
export SERVICE_SECRET_DIR=/etc/shuneo/fences-secrets

for secret_file in oidc-signing.pfx oidc-encryption.pfx; do
	if [[ ! -f "$SERVICE_SECRET_DIR/$secret_file" ]]; then
		echo "Missing Fences certificate secret: $SERVICE_SECRET_DIR/$secret_file" >&2
		exit 1
	fi
done

: "${DEPLOY_COMMON_SCRIPT:?DEPLOY_COMMON_SCRIPT must identify the shared deployment script}"
exec bash "$DEPLOY_COMMON_SCRIPT"
