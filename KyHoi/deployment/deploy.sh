#!/usr/bin/env bash
# One writer only: never use the generic overlapping-container deployer for rooms.
set -Eeuo pipefail
: "${KYHOI_IMAGE:?Set KYHOI_IMAGE to the loaded image tag}"
[[ "$KYHOI_IMAGE" =~ ^kyhoi:[a-f0-9]{40}$ ]] || { echo 'Expected kyhoi:<commit SHA>' >&2; exit 2; }
domain=kyhoi.shuneo.com
container=kyhoi
fragment=/etc/caddy/sites/$domain.caddy
as_root() { if [[ $EUID -eq 0 ]]; then "$@"; else sudo -n -- "$@"; fi; }
docker_cmd=(docker)
if ! docker info >/dev/null 2>&1; then docker_cmd=(sudo -n docker); fi
d() { "${docker_cmd[@]}" "$@"; }
d info >/dev/null
command -v flock >/dev/null
as_root install -d -m 0755 /etc/caddy/sites
# Share the existing ingress lock inode; never truncate/recreate it during a deploy.
exec 9>/tmp/kyhoi-deploy.lock
flock -n 9 || { echo 'Another Ky Hoi deployment is running' >&2; exit 1; }
as_root touch /var/lock/deploy-caddy.lock
as_root chmod 0660 /var/lock/deploy-caddy.lock
as_root chown "$(id -u):$(id -g)" /var/lock/deploy-caddy.lock
exec 8>/var/lock/deploy-caddy.lock
flock -n 8 || { echo 'Another ingress deployment is running' >&2; exit 1; }
d image inspect "$KYHOI_IMAGE" >/dev/null
work=$(mktemp -d)
had_old=false; old_running=false; candidate=false; ingress_changed=false; committed=false; backup=''
if d container inspect "$container" >/dev/null 2>&1; then
  had_old=true
  old_running=$(d inspect -f '{{.State.Running}}' "$container")
fi
if [[ -f "$fragment" ]]; then as_root cp "$fragment" "$work/previous.caddy"; fi
rollback() {
  code=$?
  if [[ "$committed" != true ]]; then
    if [[ "$candidate" == true ]]; then d rm -f "$container" >/dev/null || true; fi
    if [[ "$ingress_changed" == true ]]; then
      if [[ -f "$work/previous.caddy" ]]; then as_root cp "$work/previous.caddy" "$fragment"; else as_root rm -f "$fragment"; fi
      as_root systemctl reload caddy || true
    fi
    # Restore the stopped snapshot before restarting the previous image.
    if [[ -n "$backup" && "$candidate" == true ]]; then
      d run --rm --network none --user 0 --entrypoint sh -v kyhoi-data:/data -v kyhoi-backups:/backups:ro "$KYHOI_IMAGE" -c 'find /data -mindepth 1 -maxdepth 1 -exec rm -rf -- {} +; tar -xzf "/backups/$1" -C /data' sh "$backup" || { echo 'Data restore failed; old engine stays stopped' >&2; exit 1; }
    fi
    if [[ "$had_old" == true ]] && d container inspect kyhoi-previous >/dev/null 2>&1; then d rename kyhoi-previous "$container"; fi
    if [[ "$old_running" == true ]]; then d start "$container" >/dev/null || true; fi
  fi
  rm -rf -- "$work"
  exit "$code"
}
trap rollback EXIT
printf '%s {\n    reverse_proxy 127.0.0.1:3400\n}\n' "$domain" > "$work/new.caddy"
# Validate the full proposed ingress before stopping the app.
as_root cp "$work/new.caddy" "$fragment"
ingress_changed=true
as_root chmod 0644 "$fragment"
as_root caddy validate --config /etc/caddy/Caddyfile --adapter caddyfile
d volume create kyhoi-data >/dev/null
d volume create kyhoi-backups >/dev/null
if [[ "$had_old" == true ]]; then
  d stop --time 30 "$container" >/dev/null
  if d container inspect kyhoi-previous >/dev/null 2>&1; then d rm kyhoi-previous >/dev/null; fi
  d rename "$container" kyhoi-previous
fi
backup="rooms-$(date -u +%Y%m%dT%H%M%SZ)-${KYHOI_IMAGE#*:}.tar.gz"
d run --rm --network none --user 0 --entrypoint sh -v kyhoi-data:/data:ro -v kyhoi-backups:/backups "$KYHOI_IMAGE" -c 'umask 077; tar -czf "/backups/$1" -C /data .' sh "$backup"
d run --rm --network none --user 0 --entrypoint sh -v kyhoi-data:/data "$KYHOI_IMAGE" -c 'chown -R 1000:1000 /data'
# Linux host networking preserves the loopback source from the host Caddy service.
# App binds only 127.0.0.1:3400; no public Docker port is published.
candidate=true
d run -d --name "$container" --network host --restart unless-stopped --read-only --tmpfs /tmp:rw,noexec,nosuid,size=64m --cap-drop ALL --security-opt no-new-privileges --memory 512m --cpus 1 -v kyhoi-data:/data "$KYHOI_IMAGE" >/dev/null
healthy=false
for ((i=0;i<30;i++)); do
  if [[ $(d inspect -f '{{.State.Health.Status}}' "$container") == healthy ]]; then healthy=true; break; fi
  sleep 2
done
[[ "$healthy" == true ]] || { d logs --tail 50 "$container" >&2; exit 1; }
as_root systemctl reload caddy
# First-time ACME issuance can take longer than the local app health check.
curl --fail --silent --show-error --retry 12 --retry-all-errors --retry-delay 5 --max-time 15 --resolve "$domain:443:127.0.0.1" "https://$domain/health" | grep -q '"mode":"rooms"'
committed=true
echo "Deployed $KYHOI_IMAGE at https://$domain; backup volume: kyhoi-backups/$backup"
