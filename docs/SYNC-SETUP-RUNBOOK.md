# Sync setup runbook (VPS)

Run on the VPS in the AFFiNE deploy directory (`AFFINE_DEPLOY_DIR`, e.g. `/opt/affine-note`).
Use the existing VPS workspace; do not restore a backup over it.

## 1. Put the files and image on the VPS
Run **Deploy Note Sync** (GitHub Actions, `sub/note`) with `check_only` ticked. It:
- loads the image (tagged with the commit and `eldervibe-affine-sync:latest`),
- copies `compose.sync.yml` and `sync.env.example`,
- creates `sync.env` from the example if missing, then stops.

## 2. Read-only AFFiNE login
```
docker compose exec -T postgres printenv POSTGRES_USER          # the admin user, usually affine
docker compose exec -T postgres psql -U affine -d affine < provision-source-role.sql
docker compose exec -T postgres psql -U affine -d affine -c "ALTER ROLE affine_sync PASSWORD 'CHOOSE-A-PASSWORD';"
```
`provision-source-role.sql` is in `Farm.Sync/reader/`. Avoid `@ : /` in the password.

## 3. Fill in `sync.env`
```
sudo cat /etc/farm/postgres.password                  # Farm DB password
nano sync.env
```
- `DATABASE_CONNECTION_STRING`: put the Farm DB password in.
- `AFFINE_DATABASE_URL=postgresql://affine_sync:CHOOSE-A-PASSWORD@postgres:5432/affine`
- Leave the `AFFINE_*_ID` keys empty for now.

## 4. Find the IDs (private terminal only, the repo is public)
```
docker compose -f compose.yml -f compose.sync.yml run --rm --no-deps affine-sync --check --show-ids
```
Copy the workspace, collection and page IDs into `sync.env`. Re-run until all lines are PASS.

## 5. Start the sync
Run **Deploy Note Sync** without `check_only`. Verify with the diagnose workflow.
