# Production: kyhoi.shuneo.com

`Deploy Kỳ Hội` builds/tests the image, transfers it over verified SSH and deploys on the existing `hostkey-server`. It runs on main when KyHoi changes, or manually through Actions. Forks never run production deployment. Merge PR #29 in the upstream repository to make this workflow available; pushing the fork alone does not deploy.

## Existing GitHub environment

Configure these secrets in **upstream repository → Settings → Environments → hostkey-server** (reuse the existing values):

| Secret | Meaning |
| --- | --- |
| VPS_HOST | Target VPS address |
| VPS_PORT | SSH port; defaults to 22 |
| VPS_USER | Existing deploy user |
| VPS_SSH_KEY | Private key authorized for that user |
| VPS_KNOWN_HOSTS | Verified SSH host key entries, including port if nonstandard |

No game API key or additional GitHub domain variable is required. Domain is explicitly `kyhoi.shuneo.com`. DNS must reach this VPS; ports 80/443 must reach the existing Caddy service. Preserve the Caddy data directory for certificates. CI credentials were not inspectable by the contributor account (403); presence and validity must be confirmed by the upstream workflow/maintainer.

## Host contract

Linux Docker Engine, curl, flock and the existing systemd Caddy ingress are required. Caddy's `/etc/caddy/Caddyfile` must import `/etc/caddy/sites/*.caddy`. The deploy user needs Docker access and passwordless sudo for ingress directory/fragment management, `/var/lock/deploy-caddy.lock`, Caddy validation and reload, as in the existing repo deployment contract. No new SSH key is embedded in this project. Ensure port 3400 is unused before first deployment.

Container `kyhoi` runs as UID 1000, read-only root, dropped capabilities and `unless-stopped`. It uses Linux host networking but binds only `127.0.0.1:3400`; Caddy is the sole public entry. Fastify trusts only loopback proxy IP. Caddy's standard forwarded headers preserve the client IP for HTTP and WebSocket. If adding a CDN/proxy in front of Caddy, explicitly configure and validate that proxy's IP handling before enabling it; otherwise clients may collapse to one identity.

## Data, backups and rollback

- `kyhoi-data` named volume contains `/data/rooms`, including the identity registry and each room database. Never remove the volume during upgrades.
- Deploy stops the old engine before copying data or starting another engine. This causes brief downtime; two engines never share the live data concurrently.
- Every deploy archives the entire stopped volume to `kyhoi-backups`, then starts the candidate. Local Docker health and HTTPS health through Caddy must pass.
- On failure, the candidate is removed, data is restored from the archive, the prior ingress fragment is restored and the previous container restarts if it was running. On success the stopped `kyhoi-previous` container remains until the next deploy.
- Backups are on the same VPS, not offsite. Export them regularly and monitor volume space; automated pruning is intentionally not configured.

For a manual rollback after a successful deployment, run the same script with the prior loaded image tag (`KYHOI_IMAGE=kyhoi:<40-character-commit-sha> bash deploy.sh`). This makes a fresh backup before switching. Review schema compatibility before rolling back across future data migrations.

## Checks and operations

```sh
docker ps --filter name=kyhoi
docker logs --tail 100 kyhoi
curl -f https://kyhoi.shuneo.com/health
docker volume inspect kyhoi-data kyhoi-backups
```

CI runs `test-image.sh` on Linux: non-root/read-only startup, forwarded-IP separation and room/team persistence across container restart. Existing browser tests cover WebSocket privacy. Actual VPS deployment, TLS and an eight-hour real-time soak are separate checks; do not infer success from the image build.
