# Seafile Server Storage (`storage.eldervibe.dev`)

Open-source cloud storage powered by [Seafile Community Edition](https://github.com/haiwen/seafile-server) behind Caddy reverse proxy, authenticated via OpenID Connect / OAuth2 from Fences (`identity.eldervibe.dev`).

Replaces previous custom-built storage implementation (`Shed`) with official, production-grade Seafile server components while keeping the repository folder name `Shed/`.

## GitHub workflows

The storage deploy workflow uses the `hostkey-server` environment. Configure these environment secrets:

- `VPS_HOST`, `VPS_PORT`, `VPS_USER`, `VPS_SSH_KEY`, `VPS_KNOWN_HOSTS`

Configure this environment variable:

- `STORAGE_DEPLOY_DIR`, an absolute VPS path such as `/srv/storage`

Backups run from `backup-all.yml` on `main`. It saves `.env`, `docker-compose.yml`, the Seafile `data/` tree (conf, `seafile-data`, `seahub-data`; no logs) and a `mariadb-dump` of all databases into `/var/backups/eldervibe/eldervibe-backup-<UTC>.zip`. The raw `mysql/` directory is not copied. Restore steps are in `RESTORE.md` inside the archive. The archive stays on the VPS and is not uploaded as a GitHub artifact.

---

## Architecture

- **Seafile Server (`seafile-server`)**: Runs `seafileltd/seafile-mc` handling Web UI (Seahub), WebDAV, file syncing daemon, and chunk file server (`seafhttp`).
- **Database (`seafile-mysql`)**: MariaDB 10.11 for Seafile metadata, accounts, and sharing permissions.
- **Cache (`seafile-memcached`)**: Memcached for session and cache layer.
- **Ingress**: Caddy reverse proxy on the host handling automatic TLS and proxying `storage.eldervibe.dev` to container port `8080`.

---

## Directory Structure on VPS

- Compose & root config: `$STORAGE_DEPLOY_DIR/docker-compose.yml`
- MariaDB data: `$STORAGE_DEPLOY_DIR/mysql`
- Seafile & Seahub data / configs: `$STORAGE_DEPLOY_DIR/data`
   - Seahub settings: `$STORAGE_DEPLOY_DIR/data/seafile/conf/seahub_settings.py`

---

## OpenID Connect / OAuth2 Configuration with Fences

To authenticate Seafile users via `identity.eldervibe.dev`:

1. **Register Seafile client in Fences (`sub/identity`)**:
   - Client ID: `storage` (the subdomain label)
   - Client Secret: `<generated-secret>` (confidential client; PKCE optional)
   - Redirect URI: `https://storage.eldervibe.dev/oauth/callback/`
   - Scopes: `openid`, `profile`, `email`

2. **Configure Seahub** (reads from environment variables via container):
   The deploy workflow writes these values to `$STORAGE_DEPLOY_DIR/.env` and passes them to the Seafile container:
   ```bash
   OAUTH_CLIENT_ID=storage
   OAUTH_CLIENT_SECRET=<generated-secret>
   OAUTH_REDIRECT_URL=https://storage.eldervibe.dev/oauth/callback/
   OAUTH_AUTHORIZATION_URL=https://identity.eldervibe.dev/connect/authorize
   OAUTH_TOKEN_URL=https://identity.eldervibe.dev/connect/token
   OAUTH_USER_INFO_URL=https://identity.eldervibe.dev/connect/userinfo
   ```

      Keep Seafile's generated `data/seafile/conf/seahub_settings.py`, including its database, cache and secret-key settings. Do not replace it with the standalone template. The deploy script replaces only its managed block, adding HTTPS proxy and OAuth settings to existing installations without removing local Seahub configuration. It restarts `seafile-server` after updating the file. The `STORAGE_OAUTH_CLIENT_ID`, `STORAGE_OAUTH_CLIENT_SECRET` and redirect URI must match the client registered in Fences exactly.

3. **Restart Seahub**:
   ```bash
   docker exec -it seafile-server /opt/seafile/seafile-server-latest/seahub.sh restart
   ```
