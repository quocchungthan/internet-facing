#!/usr/bin/env bash
set -Eeuo pipefail
cd "$(dirname "$0")/.."
root="$(pwd)"
temporary="$(mktemp -d)"
container="30s-caddy-check-$$"
cleanup() {
  docker stop "$container" >/dev/null 2>&1 || true
  rm -f -- "$temporary/config.json"
  rmdir "$temporary"
}
trap cleanup EXIT

MSYS_NO_PATHCONV=1 docker run --rm \
  -v "$(cygpath -w "$root/../deployment/caddy/sites/30s.eldervibe.dev.caddy" 2>/dev/null || printf '%s' "$root/../deployment/caddy/sites/30s.eldervibe.dev.caddy"):/etc/caddy/Caddyfile:ro" \
  caddy:2-alpine caddy adapt --config /etc/caddy/Caddyfile \
  | node scripts/caddy-test-config.mjs > "$temporary/config.json"

config_path="$temporary/config.json"
dist_path="$root/dist"
if command -v cygpath >/dev/null 2>&1; then
  config_path="$(cygpath -w "$config_path")"
  dist_path="$(cygpath -w "$dist_path")"
fi
MSYS_NO_PATHCONV=1 docker run --rm --name "$container" -p 127.0.0.1:4180:80 \
  -v "$config_path:/etc/caddy/config.json:ro" -v "$dist_path:/srv/30s/current:ro" \
  caddy:2-alpine caddy run --config /etc/caddy/config.json &
server=$!
curl --fail --silent --show-error --retry 15 --retry-connrefused --retry-delay 1 \
  -H 'Host: 30s.eldervibe.dev' http://127.0.0.1:4180/ -o /dev/null
check() {
  local path="$1" expected="$2" method="${3:-GET}" actual
  actual="$(curl --silent --show-error -X "$method" -H 'Host: 30s.eldervibe.dev' \
    --output /dev/null --write-out '%{http_code}' "http://127.0.0.1:4180$path")"
  if [[ "$actual" != "$expected" ]]; then
    echo "$method $path: expected $expected, got $actual" >&2
    exit 1
  fi
}
check '/?car=impala&part=engine' 200
check /books/raize.md 200
check /books/impala.md 200
check /media/raize-engine.jpg 200
check /not-a-route 404
check /index.html 404
check /src/main.tsx 404
check /assets/missing.js 404
check / 405 POST
docker stop "$container" >/dev/null
wait "$server"
echo 'Production Caddy routes passed: root/query state, books, media, missing assets and method restrictions.'
