#!/usr/bin/env bash
set -Eeuo pipefail
script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
merge="$script_dir/merge-users-client.sh"
existing='[{"clientId":"mail","redirectUri":"https://mail.eldervibe.dev/callback","clientSecret":"existing"}]'
merged="$(printf '%s' "$existing" | bash "$merge")"
printf '%s' "$merged" | jq -e 'length == 2 and .[0].clientId == "mail" and .[0].clientSecret == "existing" and .[1].clientId == "opensourcelab" and .[1].redirectUri == "https://users.eldervibe.dev/signin-oidc" and .[1].requirePkce == true' >/dev/null
[[ "$(printf '%s' "$merged" | bash "$merge" | jq length)" == 2 ]]
for invalid in \
  '[{"clientId":"opensourcelab","redirectUri":"https://evil.example/signin-oidc","requirePkce":true}]' \
  '[{"clientId":"opensourcelab","redirectUri":"https://users.eldervibe.dev/signin-oidc","requirePkce":false}]' \
  '[{"clientId":"opensourcelab","redirectUri":"https://users.eldervibe.dev/signin-oidc","requirePkce":true,"clientSecret":"conflict"}]' \
  '[{"clientId":"mail","redirectUri":"https://mail.example"},{"clientId":"mail","redirectUri":"https://mail.example"}]'; do
    if printf '%s' "$invalid" | bash "$merge" >/dev/null; then echo 'Invalid client configuration was accepted.' >&2; exit 1; fi
done
echo 'Users OIDC client merge checks passed.'
