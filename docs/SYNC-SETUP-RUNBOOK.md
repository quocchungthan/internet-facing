# Sync setup runbook (VPS)

Run on the VPS in the AFFiNE deploy directory (`AFFINE_DEPLOY_DIR`, e.g. `/opt/affine-note`).
Use the existing VPS workspace; do not restore a backup over it.

## 1. Put the files and image on the VPS
The workflow lives only on `sub/note`, and GitHub's **Run workflow** button lists only workflows on the
default branch. So it runs **automatically on every push to `sub/note`** (setup
check only, nothing is started; a green run does NOT mean the sync is running). To trigger it with no code change, push an empty commit:
`git commit --allow-empty -m "run sync check" && git push`. To also start the sync, put `[deploy-sync]`
in the commit message. It:
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
Push an empty commit with `[deploy-sync]` in the message (`git commit --allow-empty -m "[deploy-sync] start sync" && git push`). Verify with the diagnose workflow.

## Q&A (decisions made while setting this up)
- **Why restore/copy the workspace?** Not needed. We use the existing VPS workspace. The sync reads AFFiNE's Postgres on the VPS, so the pages must exist there. (`affine-collections.sh restore` deletes and replaces that workspace's documents; avoid it.)
- **Why a read-only role?** `affine_sync` can only SELECT `snapshots` and `updates`, so a bug or leaked `sync.env` can't change AFFiNE data. `<that user>` in the SQL commands is the AFFiNE admin user (`printenv POSTGRES_USER` in the postgres container, usually `affine`).
- **Where is the sample env?** `Farm.Sync/deploy/sync.env.example`. The workflow copies it to `sync.env` on the VPS if missing, then stops. It never overwrites an existing file.
- **What are the IDs?** Workspace, collection and page IDs: `--check --show-ids`. `AFFINE_FEEDS_BLOCK_ID` is the table block (Feeds.Data) inside the Feeds page, not the page: use "Copy link to block" in AFFiNE, the part after `#`.
- **"No such image eldervibe-affine-sync:latest"?** The image wasn't on the VPS yet; run the workflow (it now tags the image `latest`).
- **Is the check safe in a public repo?** CI output has counts only; IDs and titles only with `--show-ids` in a private terminal.
- **Open question:** AFFiNE may offer a worker/plugin feature that could replace the sidecar; unverified.
