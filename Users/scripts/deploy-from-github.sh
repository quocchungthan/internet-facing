#!/usr/bin/env bash
set -Eeuo pipefail
umask 077

for name in VPS_HOST VPS_PORT VPS_USER VPS_SSH_KEY VPS_KNOWN_HOSTS GHCR_READ_USER GHCR_READ_TOKEN \
    USERS_LITELLM_NETWORK USERS_LITELLM_URL USERS_LITELLM_CONTAINER USERS_IMAGE_REPOSITORY IMAGE_DIGEST; do
    [[ -n "${!name:-}" ]] || { echo "Missing required value: $name" >&2; exit 1; }
done
[[ "$VPS_HOST" =~ ^[A-Za-z0-9.:-]+$ && "$VPS_PORT" =~ ^[0-9]+$ && "$VPS_USER" == root ]] || { echo 'VPS_USER must be root and the host/port must be valid.' >&2; exit 1; }
[[ "$GHCR_READ_USER" =~ ^[A-Za-z0-9][A-Za-z0-9-]*$ ]] || { echo 'Invalid GHCR username.' >&2; exit 1; }
[[ "$USERS_IMAGE_REPOSITORY" =~ ^ghcr\.io/[a-z0-9._/-]+$ && "$IMAGE_DIGEST" =~ ^sha256:[a-f0-9]{64}$ ]] || { echo 'Invalid image repository or immutable digest.' >&2; exit 1; }
for name in GHCR_READ_TOKEN USERS_LITELLM_NETWORK USERS_LITELLM_URL USERS_LITELLM_CONTAINER; do
    [[ "${!name}" != *$'\n'* && "${!name}" != *$'\r'* ]] || { echo "$name must be single-line." >&2; exit 1; }
done

work_dir="$(mktemp -d)"
remote_dir=''
cleanup() {
    if [[ -n "$remote_dir" ]]; then ssh "${ssh_options[@]}" "$ssh_target" "rm -rf -- '$remote_dir'" < /dev/null || true; fi
    rm -rf -- "$work_dir"
}
trap cleanup EXIT
printf '%s\n' "$VPS_SSH_KEY" > "$work_dir/key"
printf '%s\n' "$VPS_KNOWN_HOSTS" > "$work_dir/known_hosts"
chmod 600 "$work_dir/key" "$work_dir/known_hosts"
ssh_options=(-i "$work_dir/key" -p "$VPS_PORT" -o BatchMode=yes -o StrictHostKeyChecking=yes -o UserKnownHostsFile="$work_dir/known_hosts" -o GlobalKnownHostsFile=/dev/null)
scp_options=(-i "$work_dir/key" -P "$VPS_PORT" -o BatchMode=yes -o StrictHostKeyChecking=yes -o UserKnownHostsFile="$work_dir/known_hosts" -o GlobalKnownHostsFile=/dev/null)
ssh_target="$VPS_USER@$VPS_HOST"
remote_dir="$(ssh "${ssh_options[@]}" "$ssh_target" 'set -eu; test "$(id -u)" -eq 0; umask 077; mktemp -d /tmp/users-deploy.XXXXXX')"
if [[ ! "$remote_dir" =~ ^/tmp/users-deploy\.[A-Za-z0-9]{6}$ ]]; then
    remote_dir=''
    echo 'Invalid remote staging directory.' >&2
    exit 1
fi
printf '%s' "$GHCR_READ_TOKEN" > "$work_dir/ghcr-token"
chmod 600 "$work_dir/ghcr-token"
scp "${scp_options[@]}" Users/scripts/deploy-users.sh deployment/deploy-caddy-service.sh "$work_dir/ghcr-token" "$ssh_target:$remote_dir/" >/dev/null
printf -v remote_command 'set -Eeuo pipefail; umask 077; export DOCKER_CONFIG=%q; mkdir -m 700 "$DOCKER_CONFIG"; trap '\''rm -rf -- %q'\'' EXIT; docker login ghcr.io --username %q --password-stdin < %q; export USERS_IMAGE=%q USERS_LITELLM_NETWORK=%q USERS_LITELLM_URL=%q USERS_LITELLM_CONTAINER=%q DEPLOY_COMMON_SCRIPT=%q; bash %q' \
    "$remote_dir/docker-config" "$remote_dir" "$GHCR_READ_USER" "$remote_dir/ghcr-token" \
    "$USERS_IMAGE_REPOSITORY@$IMAGE_DIGEST" "$USERS_LITELLM_NETWORK" "$USERS_LITELLM_URL" "$USERS_LITELLM_CONTAINER" "$remote_dir/deploy-caddy-service.sh" "$remote_dir/deploy-users.sh"
ssh "${ssh_options[@]}" "$ssh_target" "$remote_command" < /dev/null
remote_dir=''
echo "Deployed $USERS_IMAGE_REPOSITORY@$IMAGE_DIGEST to https://users.eldervibe.dev"
