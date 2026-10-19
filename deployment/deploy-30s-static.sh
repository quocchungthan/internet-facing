#!/usr/bin/env bash
set -Eeuo pipefail

if [[ "$EUID" -ne 0 ]]; then
  exec sudo -n bash "$0" "$@"
fi

staging="${1:?Provide the uploaded staging directory}"
release_id="${2:?Provide the source revision and run ID}"
if [[ ! "$staging" =~ ^/tmp/30s-[A-Za-z0-9.]+$ || ! -d "$staging" || -L "$staging" ]]; then
  echo "Invalid staging directory" >&2
  exit 2
fi
if [[ ! "$release_id" =~ ^[a-f0-9]{40}-[0-9]+-[0-9]+$ ]]; then
  echo "Invalid release identifier" >&2
  exit 2
fi
test -s "$staging/dist/index.html"
test -d "$staging/dist/assets"
test -s "$staging/dist/books/raize.md"
test -s "$staging/dist/books/impala.md"
test -s "$staging/site.caddy"
command -v caddy >/dev/null
command -v curl >/dev/null
systemctl is-active --quiet caddy

install -d -m 0755 /srv/30s/releases /etc/caddy/sites
exec 9>/var/lock/deploy-caddy.lock
flock -w 120 9

release="/srv/30s/releases/$release_id"
current=/srv/30s/current
site=/etc/caddy/sites/30s.eldervibe.dev.caddy
temporary_link="/srv/30s/.current-$release_id"
previous=
if [[ -e "$current" && ! -L "$current" ]]; then
  echo "$current must be a symlink, not a directory" >&2
  exit 1
fi
if [[ -L "$current" ]]; then previous="$(readlink "$current")"; fi
if [[ -e "$release" || -L "$release" ]]; then
  echo "Release already exists: $release" >&2
  exit 1
fi

backup="$(mktemp /etc/caddy/sites/.30s-previous.XXXXXX)"
had_site=false
if [[ -f "$site" ]]; then cp -p "$site" "$backup"; had_site=true; fi
switched=false
committed=false
cleanup() {
  local result=$?
  trap - EXIT
  if [[ "$committed" != true ]]; then
    echo "Deployment failed; restoring the previous release and site." >&2
    if [[ "$had_site" == true ]]; then cp -p "$backup" "$site"; else rm -f -- "$site"; fi
    if [[ "$switched" == true ]]; then
      if [[ -n "$previous" ]]; then
        ln -s "$previous" "$temporary_link"
        mv -Tf "$temporary_link" "$current"
      else
        rm -f -- "$current"
      fi
    fi
    if ! caddy validate --config /etc/caddy/Caddyfile || ! systemctl reload caddy; then
      echo "CRITICAL: rollback Caddy validation/reload failed; operator intervention required." >&2
    fi
  fi
  rm -f -- "$backup" "$temporary_link"
  exit "$result"
}
trap cleanup EXIT

install -d -m 0755 "$release"
cp -R "$staging/dist/." "$release/"
chown -R root:root "$release"
chmod -R a+rX "$release"
ln -s "$release" "$temporary_link"
mv -Tf "$temporary_link" "$current"
switched=true
install -m 0644 "$staging/site.caddy" "$site"
caddy validate --config /etc/caddy/Caddyfile
systemctl reload caddy
systemctl is-active --quiet caddy

# HTTP reaches the local Caddy listener without depending on public DNS or certificate issuance.
status="$(curl --silent --show-error --output /dev/null --write-out '%{http_code}' \
  --header 'Host: 30s.eldervibe.dev' http://127.0.0.1/)"
if [[ "$status" != 308 && "$status" != 200 ]]; then
  echo "Unexpected local Caddy status: $status" >&2
  exit 1
fi
index="$(curl --fail --silent --show-error --retry 12 --retry-all-errors --retry-delay 5 \
  --connect-timeout 5 --max-time 15 --resolve 30s.eldervibe.dev:443:127.0.0.1 https://30s.eldervibe.dev/)"
if [[ "$index" != *'30s'* || "$index" != *'/assets/'* ]]; then
  echo "Caddy did not serve the built frontend over HTTPS" >&2
  exit 1
fi
curl --fail --silent --show-error --connect-timeout 5 --max-time 15 \
  --resolve 30s.eldervibe.dev:443:127.0.0.1 https://30s.eldervibe.dev/books/raize.md -o /dev/null
status="$(curl --silent --show-error --connect-timeout 5 --max-time 15 \
  --resolve 30s.eldervibe.dev:443:127.0.0.1 --output /dev/null --write-out '%{http_code}' \
  https://30s.eldervibe.dev/not-an-app-route)"
if [[ "$status" != 404 ]]; then
  echo "Unknown application paths must return 404, got $status" >&2
  exit 1
fi
committed=true
printf 'Installed static release %s\n' "$release_id"
