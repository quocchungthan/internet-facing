# Fences

Fences is the central GitHub identity and workspace launcher for the Shuneo services.

## Routes

- `GET /` public sign-in or authenticated app launcher
- `GET /apps` authenticated launcher
- `GET /auth/login?returnUrl=<url>` starts GitHub OAuth
- `GET /auth/post-login?returnUrl=<url>` resolves the post-login target
- `GET /auth/logout?returnUrl=<url>` clears the shared identity cookie
- `GET /api/session` returns session state
- `GET /api/apps` returns the configured app registry
- `GET /api/github/clone-token` returns the stored GitHub token for an authenticated user
- `GET /api/github/repos` lists the user repositories
- `POST /api/github/repos` finds or creates a repository
- `GET /signin-github` is the GitHub OAuth callback

## Configuration

Set OAuth secrets with environment variables rather than committing them:

- `Authentication__GitHub__ClientId`
- `Authentication__GitHub__ClientSecret`

`IdentityApp:CookieDomain`, `AllowedReturnHosts`, and `AllowedCorsOrigins` retain the source deployment policy. The production cookie is `shuneo.identity`, secure, HTTP-only, SameSite Lax, and shared across `.shuneo.com` when configured. Return URLs are limited to relative paths or the configured allowed hosts.

The Dockerfile expects the Docker build context to be `internet-facing`, publishes `Fences.dll`, and serves HTTP on port 80 behind the reverse proxy. It links Farm's canonical shared CSS tokens and base styles into the Fences publish output without maintaining a second copy.

## Local run

```text
dotnet run --project Fences/Fences.csproj
```

The GitHub OAuth callback must match the public host: `<public-host>/signin-github`.

## VPS deployment

Run the Docker build from the `internet-facing` directory so the Dockerfile can copy the shared Farm assets:

```text
docker build -f Fences/Dockerfile -t shuneo-fences .
docker run -d --name shuneo-fences --restart unless-stopped \
	-p 127.0.0.1:5188:80 \
	-e Authentication__GitHub__ClientId="<github-client-id>" \
	-e Authentication__GitHub__ClientSecret="<github-client-secret>" \
	shuneo-fences
```

If the image restore needs the private GitHub package feed, pass `--build-arg GH_NUGET_TOKEN=<github-packages-token>` to `docker build`. Keep all tokens and secrets in the VPS operator's secret store or shell environment; do not commit them.

`deployment/deploy-nginx-service.sh` contains the shared VPS deployment logic. `Fences/scripts/deploy-fences.sh` is its service-specific wrapper. Run the wrapper as the SSH user after loading the image, for example:

```text
FENCES_IMAGE=shuneo-fences:<image-tag> CERTBOT_EMAIL=ops@example.com \
	bash Fences/scripts/deploy-fences.sh
```

It manages only `identity.shuneo.com` and `127.0.0.1:5188`. It takes an exclusive lock, stages the HTTP ACME site, runs `nginx -t` and reloads nginx before Certbot, then verifies the certificate files and SAN before staging HTTPS. A matching existing Certbot renewal lineage is discovered from its renewal configuration and certificate SANs, including suffixed lineage names; otherwise a new `identity.shuneo.com` lineage is created. Failed nginx validation restores the previous managed site and does not touch unrelated enabled sites.

The common script accepts `SERVICE_NAME`, `SERVICE_DOMAIN`, `SERVICE_UPSTREAM_PORT`, `SERVICE_CONTAINER`, `SERVICE_IMAGE`, and `SERVICE_ENV_FILE`, plus VPS path/tool overrides for `NGINX_SITES_AVAILABLE`, `NGINX_SITES_ENABLED`, `CERTBOT_WEBROOT`, `CERTBOT_CONFIG_DIR`, `CERTBOT_BIN`, `OPENSSL_BIN`, `NGINX_SERVICE`, `DEPLOY_LOCK_FILE`, `DOCKER_BIN`, and `NGINX_BIN`. To add another service, create a small wrapper beside Fences that exports those six service values and `exec bash`es the common script. When a wrapper is streamed over SSH, transfer the common script first and set `DEPLOY_NGINX_COMMON_SCRIPT` to its remote path.

The deployment lock is service-specific by default. The managed nginx link and temporary configs are named from the service domain. The script never enumerates, rewrites, disables, or prunes unrelated nginx sites or Docker images.

The VPS nginx configuration is operator-managed through this script; DNS remains operator-managed. The GitHub OAuth callback is:
`https://identity.shuneo.com/signin-github`.

## GitHub Actions deployment

`.github/workflows/deploy-fences.yml` builds the image on pushes to `sub/identity` and can also be started with **Run workflow**. It is a thin caller of `.github/workflows/reusable-deploy-service.yml`, which checks out the repository, builds and transfers the image over SSH, then streams the common deployment script and the Fences wrapper. The wrapper configures only the Fences nginx mapping, renews or issues its independent certificate lineage, and replaces the `shuneo-fences` container. The workflow does not configure DNS or application secrets.

Configure a GitHub `production` environment with these secrets:

- `VPS_HOST`: VPS hostname or IP address
- `VPS_PORT`: SSH port
- `VPS_USER`: SSH user with permission to run Docker
- `VPS_SSH_KEY`: private key for that user
- `VPS_KNOWN_HOSTS`: pinned `ssh-keyscan` output for the VPS host and port
- `CERTBOT_EMAIL`: optional production environment variable or secret, used only when a new Fences lineage is required; a secret takes precedence when both are configured

Before the first deployment, install Docker on the VPS, grant the SSH user Docker access, and create `/etc/shuneo/fences.env` with the required `Authentication__GitHub__ClientId` and `Authentication__GitHub__ClientSecret` values plus the intended `IdentityApp` settings. The SSH user must be able to bind the loopback port `5188`, and nginx must proxy `identity.shuneo.com` to that port. Keep the environment file readable only by the deployment user or its Docker access group.

Before the first workflow deployment, ensure nginx includes `sites-enabled`, `/var/www/certbot` is writable by the SSH user, `certbot`, `openssl`, `flock`, Docker, and `systemctl` are available, and the SSH user can reload the nginx service. The script uses a dedicated nginx site entry for `identity.shuneo.com`; it does not enumerate, rewrite, disable, or reload any unrelated domain configuration.

Future services only need a caller workflow and a service wrapper. The caller should invoke `reusable-deploy-service.yml` with the service image name and tag, Docker context and Dockerfile, wrapper path, common script path, and the wrapper's runtime image environment variable name. The wrapper should export the service-specific deployment values and execute the transferred common script, following `Fences/scripts/deploy-fences.sh` as the template.
