#!/usr/bin/env bash
set -Eeuo pipefail

root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)"
temporary="$(mktemp -d)"
trap 'rm -rf -- "$temporary"' EXIT
mkdir "$temporary/bin" "$temporary/sites"
touch "$temporary/runtime.env" "$temporary/caddyfile"
cat > "$temporary/bin/docker" <<'MOCK'
#!/usr/bin/env bash
printf '%s\n' "$*" >> "$TEST_COMMANDS"
case "$1 $2" in
  'container inspect') exit 1 ;;
  'run --detach') echo fixture ;;
  'network connect') [[ "${FAIL_NETWORK:-}" != true ]] ;;
esac
MOCK
cat > "$temporary/bin/curl" <<'MOCK'
#!/usr/bin/env bash
printf '%s\n' "$*" >> "$TEST_CURL"
printf 200
MOCK
cat > "$temporary/bin/caddy" <<'MOCK'
#!/usr/bin/env bash
exit 0
MOCK
cat > "$temporary/bin/systemctl" <<'MOCK'
#!/usr/bin/env bash
printf '%s\n' "$*" >> "$TEST_SYSTEMCTL"
MOCK
chmod +x "$temporary/bin/"*
export PATH="$temporary/bin:$PATH" TEST_COMMANDS="$temporary/commands" TEST_CURL="$temporary/curl" TEST_SYSTEMCTL="$temporary/systemctl"
export SERVICE_NAME=users SERVICE_DOMAIN=users.eldervibe.dev SERVICE_UPSTREAM_PORT=5280 SERVICE_CANDIDATE_PORT=5281
export SERVICE_CONTAINER=users-test SERVICE_IMAGE=fixture SERVICE_ENV_FILE="$temporary/runtime.env"
export SERVICE_CONTAINER_PORT=8080 SERVICE_EXTRA_ARGS='--network litellm-test' SERVICE_ADDITIONAL_NETWORK=database-test
export SERVICE_HEALTH_PATH=/health/ready CADDY_SITES_DIR="$temporary/sites" CADDY_CONFIG="$temporary/caddyfile" CADDY_LOCK_FILE="$temporary/lock"
bash "$root/deployment/deploy-caddy-service.sh" >/dev/null
grep -q -- '--publish 127.0.0.1:5281:8080' "$TEST_COMMANDS"
grep -q -- 'network connect database-test users-test-candidate-' "$TEST_COMMANDS"
grep -q -- 'http://127.0.0.1:5281/health/ready' "$TEST_CURL"
grep -q -- 'users.eldervibe.dev {' "$temporary/sites/users.eldervibe.dev.caddy"
grep -q -- 'reverse_proxy 127.0.0.1:5281' "$temporary/sites/users.eldervibe.dev.caddy"
grep -q -- 'reload caddy' "$TEST_SYSTEMCTL"
cp "$temporary/sites/users.eldervibe.dev.caddy" "$temporary/previous"
export FAIL_NETWORK=true
if bash "$root/deployment/deploy-caddy-service.sh" >/dev/null 2>&1; then echo 'Failed network attachment was accepted.' >&2; exit 1; fi
cmp "$temporary/previous" "$temporary/sites/users.eldervibe.dev.caddy"
grep -q -- 'rm --force users-test-candidate-' "$TEST_COMMANDS"
echo 'Users Caddy candidate and network checks passed.'
