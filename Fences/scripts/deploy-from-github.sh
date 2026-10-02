#!/usr/bin/env bash
set -euo pipefail
umask 077

for name in VPS_HOST VPS_PORT VPS_USER VPS_SSH_KEY VPS_KNOWN_HOSTS FENCES_IMAGE \
    FENCES_GITHUB_CLIENT_ID FENCES_GITHUB_CLIENT_SECRET FENCES_OIDC_CLIENTS_JSON \
    FENCES_BRAND_NAME FENCES_COOKIE_DOMAIN FENCES_OIDC_ISSUER \
    FENCES_PERSISTENT_LOGIN_DAYS FENCES_ALLOWED_RETURN_HOST FENCES_ALLOWED_CORS_ORIGIN; do
    [[ -n "${!name:-}" ]] || { echo "Missing required value: $name" >&2; exit 1; }
done
[[ "$VPS_HOST" =~ ^[A-Za-z0-9.:-]+$ && "$VPS_PORT" =~ ^[0-9]+$ && "$VPS_USER" == root ]] || {
    echo 'Invalid VPS connection details; VPS_USER must be root.' >&2; exit 1;
}
if [[ -n "${FENCES_DEPLOY_DIR:-}" && ! "$FENCES_DEPLOY_DIR" =~ ^/[A-Za-z0-9_-][A-Za-z0-9._-]*(/[A-Za-z0-9_-][A-Za-z0-9._-]*)*$ ]]; then
    echo 'Invalid FENCES_DEPLOY_DIR.' >&2; exit 1
fi
for name in FENCES_GITHUB_CLIENT_ID FENCES_GITHUB_CLIENT_SECRET FENCES_BRAND_NAME \
    FENCES_COOKIE_DOMAIN FENCES_OIDC_ISSUER FENCES_PERSISTENT_LOGIN_DAYS \
    FENCES_ALLOWED_RETURN_HOST FENCES_ALLOWED_CORS_ORIGIN ACME_EMAIL; do
    [[ "${!name:-}" != *$'\n'* && "${!name:-}" != *$'\r'* ]] || {
        echo "$name must be single-line." >&2; exit 1;
    }
done
[[ "$FENCES_PERSISTENT_LOGIN_DAYS" =~ ^[1-9][0-9]*$ ]] || { echo 'Invalid login duration.' >&2; exit 1; }
[[ "$FENCES_OIDC_ISSUER" == https://* && "$FENCES_ALLOWED_CORS_ORIGIN" == https://* ]] || {
    echo 'Issuer and CORS origin must use HTTPS.' >&2; exit 1;
}
command -v jq >/dev/null || { echo 'jq is required.' >&2; exit 1; }
oidc_clients_json="$(printf '%s' "$FENCES_OIDC_CLIENTS_JSON" | jq -ec '
    select(type == "array" and length > 0)
    | select(all(.[]; type == "object" and
        ((keys - ["clientId", "redirectUri", "displayName", "clientSecret", "requirePkce", "postLogoutRedirectUri"]) | length == 0) and
        (.clientId | type == "string" and test("^[A-Za-z0-9][A-Za-z0-9_-]*$")) and
        (.redirectUri | type == "string" and startswith("https://")) and
        ((.displayName // "") | type == "string") and
        ((.clientSecret // "") | type == "string") and
        ((.postLogoutRedirectUri // "") | type == "string" and (. == "" or startswith("https://"))) and
        ((.requirePkce // false) | type == "boolean") and
        ([.clientId, .redirectUri, (.displayName // ""), (.clientSecret // ""), (.postLogoutRedirectUri // "")] | all(.[]; (contains("\n") or contains("\r")) | not))))
    | select((map(.clientId) | unique | length) == length)
')" || { echo 'Invalid FENCES_OIDC_CLIENTS_JSON.' >&2; exit 1; }

work_dir="$(mktemp -d)"
remote_dir=""
existing=false
committed=false
cleanup() {
    if [[ -n "$remote_dir" ]]; then
        if [[ "$existing" == true && "$committed" != true ]]; then
            ssh "${ssh_options[@]}" "$ssh_target" "if test -f '$remote_dir/previous.env'; then mv -f -- '$remote_dir/previous.env' '$remote_config_dir/fences.env'; fi" \
                < /dev/null || {
                    echo 'Remote environment rollback failed; previous.env remains in the remote staging directory for manual recovery.' >&2
                    rm -rf -- "$work_dir"
                    return
                }
        fi
        ssh "${ssh_options[@]}" "$ssh_target" "rm -rf -- '$remote_dir'" < /dev/null || true
    fi
    rm -rf -- "$work_dir"
}
trap cleanup EXIT
trap 'exit 1' HUP INT TERM
stage='prepare deployment'
trap 'status=$?; printf "::error::Fences deployment failed during %s (exit %d).\n" "$stage" "$status" >&2; exit "$status"' ERR
runtime_env_file="$work_dir/fences.env"
{
    printf '%s\n' 'ASPNETCORE_ENVIRONMENT=Production'
    printf 'Authentication__GitHub__ClientId=%s\n' "$FENCES_GITHUB_CLIENT_ID"
    printf 'Authentication__GitHub__ClientSecret=%s\n' "$FENCES_GITHUB_CLIENT_SECRET"
    printf 'IdentityApp__BrandName=%s\n' "$FENCES_BRAND_NAME"
    printf 'IdentityApp__CookieDomain=%s\n' "$FENCES_COOKIE_DOMAIN"
    printf 'IdentityApp__OidcIssuer=%s\n' "$FENCES_OIDC_ISSUER"
    printf 'IdentityApp__PersistentLoginDays=%s\n' "$FENCES_PERSISTENT_LOGIN_DAYS"
    printf 'IdentityApp__AllowedReturnHosts__0=%s\n' "$FENCES_ALLOWED_RETURN_HOST"
    printf 'IdentityApp__AllowedCorsOrigins__0=%s\n' "$FENCES_ALLOWED_CORS_ORIGIN"
    client_count="$(printf '%s' "$oidc_clients_json" | jq 'length')"
    for ((index = 0; index < client_count; index++)); do
        entry="$(printf '%s' "$oidc_clients_json" | jq -c ".[$index]")"
        printf 'IdentityApp__OidcClients__%d__ClientId=%s\n' "$index" "$(printf '%s' "$entry" | jq -r '.clientId')"
        printf 'IdentityApp__OidcClients__%d__RedirectUris__0=%s\n' "$index" "$(printf '%s' "$entry" | jq -r '.redirectUri')"
        for field in displayName clientSecret postLogoutRedirectUri; do
            value="$(printf '%s' "$entry" | jq -r --arg field "$field" '.[$field] // empty')"
            case "$field" in
                displayName) key=DisplayName ;;
                clientSecret) key=ClientSecret ;;
                postLogoutRedirectUri) key=PostLogoutRedirectUris__0 ;;
            esac
            [[ -z "$value" ]] || printf 'IdentityApp__OidcClients__%d__%s=%s\n' "$index" "$key" "$value"
        done
        [[ "$(printf '%s' "$entry" | jq -r '.requirePkce // false')" == true ]] && \
            printf 'IdentityApp__OidcClients__%d__RequirePkce=true\n' "$index"
    done
    true
} > "$runtime_env_file"
printf '%s\n' "$VPS_SSH_KEY" > "$work_dir/key"
printf '%s\n' "$VPS_KNOWN_HOSTS" > "$work_dir/known_hosts"
chmod 600 "$work_dir"/*
ssh_options=(-i "$work_dir/key" -p "$VPS_PORT" -o BatchMode=yes -o StrictHostKeyChecking=yes -o UserKnownHostsFile="$work_dir/known_hosts" -o GlobalKnownHostsFile=/dev/null)
scp_options=(-i "$work_dir/key" -P "$VPS_PORT" -o BatchMode=yes -o StrictHostKeyChecking=yes -o UserKnownHostsFile="$work_dir/known_hosts" -o GlobalKnownHostsFile=/dev/null)
ssh_target="$VPS_USER@$VPS_HOST"
remote_config_dir="${FENCES_DEPLOY_DIR:-/etc/shuneo}"
remote_dir="$(ssh "${ssh_options[@]}" "$ssh_target" 'umask 077; mktemp -d /tmp/fences-deploy.XXXXXX' < /dev/null)"
[[ "$remote_dir" =~ ^/tmp/fences-deploy\.[A-Za-z0-9]{6}$ ]] || {
    remote_dir=""; echo 'Invalid remote staging path.' >&2; exit 1;
}
scp "${scp_options[@]}" "$runtime_env_file" \
    Fences/scripts/provision-oidc-certificates.sh Fences/scripts/update-fences-env.sh \
    Fences/scripts/deploy-fences.sh deployment/deploy-caddy-service.sh "$ssh_target:$remote_dir/"
ssh "${ssh_options[@]}" "$ssh_target" "chmod 600 -- '$remote_dir/fences.env'; chmod 700 -- '$remote_dir'" < /dev/null
expected_data_dir="${FENCES_DEPLOY_DIR:+$FENCES_DEPLOY_DIR/data}"
expected_data_dir="${expected_data_dir:-/var/lib/fences}"
ssh "${ssh_options[@]}" "$ssh_target" "set -euo pipefail; if docker container inspect shuneo-fences >/dev/null 2>&1; then mounts=\"\$(docker inspect --format '{{range .Mounts}}{{println .Destination .Source}}{{end}}' shuneo-fences)\"; data_found=false; secrets_found=false; while read -r destination source; do case \"\$destination\" in /var/lib/fences) data_found=true; [[ \"\$source\" == '$expected_data_dir' ]] || { echo 'Active Fences data mount differs from selected deploy directory; migrate explicitly.' >&2; exit 1; } ;; /run/secrets) secrets_found=true; [[ \"\$source\" == '$remote_config_dir/fences-secrets' ]] || { echo 'Active Fences secrets mount differs from selected deploy directory; migrate explicitly.' >&2; exit 1; } ;; esac; done <<< \"\$mounts\"; [[ \"\$data_found\" == true && \"\$secrets_found\" == true ]] || { echo 'Active Fences mounts could not be verified.' >&2; exit 1; }; fi" < /dev/null
state="$(ssh "${ssh_options[@]}" "$ssh_target" "set -euo pipefail; if [[ -n '${FENCES_DEPLOY_DIR:-}' && ( -e /etc/shuneo/fences.env || -L /etc/shuneo/fences.env || -e /etc/shuneo/fences-secrets || -L /etc/shuneo/fences-secrets || -e /var/lib/fences || -L /var/lib/fences ) ]]; then echo 'Legacy Fences state exists; migrate it before changing FENCES_DEPLOY_DIR.' >&2; exit 1; fi; if [[ -e '$remote_config_dir/fences.env' && -d '$remote_config_dir/fences-secrets' ]]; then echo existing; elif [[ ! -e '$remote_config_dir/fences.env' && ! -L '$remote_config_dir/fences.env' && ! -e '$remote_config_dir/fences-secrets' && ! -L '$remote_config_dir/fences-secrets' ]]; then echo new; else echo 'Incomplete Fences state; refusing to provision.' >&2; exit 1; fi" < /dev/null)"
if [[ "$state" == existing ]]; then
    existing=true
    stage='reconcile runtime environment'
    ssh "${ssh_options[@]}" "$ssh_target" "set -euo pipefail; test ! -L '$remote_config_dir/fences.env'; test -f '$remote_config_dir/fences.env'; test \"\$(stat -c '%u:%g:%a' '$remote_config_dir/fences.env')\" = 0:0:600; cp -p -- '$remote_config_dir/fences.env' '$remote_dir/previous.env'; FENCES_DEPLOY_DIR='${FENCES_DEPLOY_DIR:-}' bash '$remote_dir/update-fences-env.sh' --updates-file '$remote_dir/fences.env' --replace-oidc" < /dev/null
elif [[ "$state" == new ]]; then
    stage='provision runtime environment'
    ssh "${ssh_options[@]}" "$ssh_target" "FENCES_DEPLOY_DIR='${FENCES_DEPLOY_DIR:-}' bash '$remote_dir/provision-oidc-certificates.sh' --runtime-env-file '$remote_dir/fences.env'" < /dev/null
else
    echo 'Invalid remote Fences state.' >&2; exit 1
fi
stage='load image on VPS'
echo 'Loading Fences image on VPS.'
docker save "$FENCES_IMAGE" | gzip | ssh "${ssh_options[@]}" "$ssh_target" 'docker load'
echo 'Fences image loaded on VPS.'
stage='transfer deployment wrapper'
printf 'export FENCES_IMAGE=%q\nexport FENCES_DEPLOY_DIR=%q\nexport ACME_EMAIL=%q\nexport DEPLOY_COMMON_SCRIPT=%q\n' \
    "$FENCES_IMAGE" "${FENCES_DEPLOY_DIR:-}" "${ACME_EMAIL:-}" "$remote_dir/deploy-caddy-service.sh" > "$work_dir/wrapper.sh"
cat Fences/scripts/deploy-fences.sh >> "$work_dir/wrapper.sh"
scp "${scp_options[@]}" "$work_dir/wrapper.sh" "$ssh_target:$remote_dir/wrapper.sh"
stage='start candidate and switch traffic'
echo 'Starting Fences candidate deployment.'
ssh "${ssh_options[@]}" "$ssh_target" "bash '$remote_dir/wrapper.sh'" < /dev/null
committed=true