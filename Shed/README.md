# Seafile Server Storage (`storage.eldervibe.dev`)

Open-source cloud storage powered by [Seafile Community Edition](https://github.com/haiwen/seafile-server) behind Caddy reverse proxy, authenticated via OpenID Connect / OAuth2 from Fences (`identity.eldervibe.dev`).

Replaces previous custom-built storage implementation (`Shed`) with official, production-grade Seafile server components while keeping the repository folder name `Shed/`.

## GitHub workflows

The storage deploy and backup workflows use the `hostkey-server` environment. Configure these environment secrets:

- `VPS_HOST`, `VPS_PORT`, `VPS_USER`, `VPS_SSH_KEY`, `VPS_KNOWN_HOSTS`

Configure this environment variable:

- `STORAGE_DEPLOY_DIR`, an absolute VPS path such as `/srv/storage`

The backup workflow leaves the archive on the VPS and prints an `scp` command for manual copy-out. It does not upload a GitHub artifact.

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
   - Client ID: `seafile`
   - Client Secret: `<generated-secret>`
   - Redirect URI: `https://storage.eldervibe.dev/oauth/callback/`
   - Scopes: `openid`, `profile`, `email`

2. **Configure Seahub** (reads from environment variables via container):
   Pass environment variables in `/srv/storage/.env` or docker-compose:
   ```bash
   OAUTH_CLIENT_ID=seafile
   OAUTH_CLIENT_SECRET=<generated-secret>
   OAUTH_REDIRECT_URL=https://storage.eldervibe.dev/oauth/callback/
   OAUTH_AUTHORIZATION_URL=https://identity.eldervibe.dev/connect/authorize
   OAUTH_TOKEN_URL=https://identity.eldervibe.dev/connect/token
   OAUTH_USER_INFO_URL=https://identity.eldervibe.dev/connect/userinfo
   ```

   Place [Shed/conf/seahub_settings_template.py](Shed/conf/seahub_settings_template.py) into `/srv/storage/data/seafile/conf/seahub_settings.py`:
   ```python
   import os

   ENABLE_OAUTH = os.environ.get('ENABLE_OAUTH', 'True').lower() in ('true', '1', 't')
   OAUTH_CLIENT_ID = os.environ.get('OAUTH_CLIENT_ID', 'seafile')
   OAUTH_CLIENT_SECRET = os.environ.get('OAUTH_CLIENT_SECRET', '')
   OAUTH_REDIRECT_URL = os.environ.get('OAUTH_REDIRECT_URL', 'https://storage.eldervibe.dev/oauth/callback/')
   OAUTH_AUTHORIZATION_URL = os.environ.get('OAUTH_AUTHORIZATION_URL', 'https://identity.eldervibe.dev/connect/authorize')
   OAUTH_TOKEN_URL = os.environ.get('OAUTH_TOKEN_URL', 'https://identity.eldervibe.dev/connect/token')
   OAUTH_USER_INFO_URL = os.environ.get('OAUTH_USER_INFO_URL', 'https://identity.eldervibe.dev/connect/userinfo')
   OAUTH_SCOPE = os.environ.get('OAUTH_SCOPE', 'openid profile email').split()
   OAUTH_ATTRIBUTE_MAP = {
       "id": (True, "email"),
       "name": (False, "name"),
       "email": (True, "email"),
   }
   OAUTH_ACTIVATE_USER_AFTER_CREATION = True
   OAUTH_CREATE_UNKNOWN_USER = True
   ```

3. **Restart Seahub**:
   ```bash
   docker exec -it seafile-server /opt/seafile/seafile-server-latest/seahub.sh restart
   ```
