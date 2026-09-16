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
- `GET /.well-known/openid-configuration` is the OIDC discovery document
- `GET /connect/authorize` supports authorization code + PKCE and uses the existing GitHub login
- `POST /connect/token` exchanges authorization codes for tokens

## Configuration

Set OAuth secrets with environment variables rather than committing them:

- `Authentication__GitHub__ClientId`
- `Authentication__GitHub__ClientSecret`

`IdentityApp:CookieDomain`, `AllowedReturnHosts`, and `AllowedCorsOrigins` retain the source deployment policy. The production cookie is `shuneo.identity`, secure, HTTP-only, SameSite Lax, and shared across `.shuneo.com` when configured. Return URLs are limited to relative paths or the configured allowed hosts.

OIDC uses OpenIddict 7.0.0 with EF Core SQLite persistence. The stable subject is `github:<numeric-github-id>`, so a GitHub rename does not change the OIDC subject. The issuer is `https://identity.eldervibe.dev`; `identity.shuneo.com` remains a compatibility alias.

`IdentityApp:OidcClients` registers public PKCE clients at startup. Each client needs a `ClientId`, one or more exact `RedirectUris`, and optional `PostLogoutRedirectUris`; do not add wildcard redirect URIs. For example:

```text
IdentityApp__OidcClients__0__ClientId=affine
IdentityApp__OidcClients__0__DisplayName=Affine
IdentityApp__OidcClients__0__RedirectUris__0=https://<your-affine-host>/oauth/callback
```

Development uses OpenIddict development certificates. Production refuses to start without both certificate files:

```text
IdentityApp__OidcIssuer=https://identity.eldervibe.dev
IdentityApp__OidcSigningCertificatePath=/run/secrets/oidc-signing.pfx
IdentityApp__OidcSigningCertificatePassword=<secret>
IdentityApp__OidcEncryptionCertificatePath=/run/secrets/oidc-encryption.pfx
IdentityApp__OidcEncryptionCertificatePassword=<secret>
IdentityApp__IdentityDatabasePath=/var/lib/fences/identity.db
IdentityApp__DataProtectionKeysPath=/var/lib/fences/keys
```

The SQLite file and Data Protection key directory are persistent runtime state and must be mounted from durable VPS storage. The MVP uses `EnsureCreated` rather than EF migrations; back up the SQLite file before schema/package upgrades and introduce an explicit migration process before schema changes.

The Dockerfile expects the Docker build context to be `internet-facing`, publishes `Fences.dll`, and serves HTTP on port 80 behind the reverse proxy. It links Farm's canonical shared CSS tokens and base styles into the Fences publish output without maintaining a second copy.

## Local run

```text
dotnet run --project Fences/Fences.csproj
```

The default project launch profile selects the `Development` environment and HTTP on `http://localhost:5188`, so local runs do not require a trusted HTTPS certificate. Development also uses OpenIddict development certificates. Production mode still requires both configured PFX files. To run without the launch profile, set `ASPNETCORE_ENVIRONMENT=Development` explicitly.

Use the optional HTTPS profile when testing HTTPS-specific behavior:

```text
dotnet run --project Fences/Fences.csproj --launch-profile FencesHttps
```

On Linux, trust the HTTPS developer certificate for Kestrel and system clients. If `dotnet dev-certs https --trust` reports that the certificate is trusted only by some clients, install the generated PEM in the system CA store:

```text
dotnet dev-certs https --trust
certificate=$(find "$HOME/.aspnet/dev-certs/trust" -name '*.pem' -print -quit)
sudo install -m 0644 "$certificate" /usr/local/share/ca-certificates/aspnetcore-localhost.crt
sudo update-ca-certificates
```

Restart the terminal and browser after updating the trust store, then run the app again.

For GitHub login, set these user secrets or environment variables locally:

```text
Authentication__GitHub__ClientId=<github-client-id>
Authentication__GitHub__ClientSecret=<github-client-secret>
```

The GitHub OAuth callback must match the public host: `<public-host>/signin-github`.

## VPS deployment

The active deployment uses Caddy for HTTPS and runs the container on loopback port `5188`. Run the Docker build from the `internet-facing` directory so the Dockerfile can copy the shared Farm assets:

```text
docker build -f Fences/Dockerfile -t shuneo-fences .
```

Create the runtime environment file and two certificate files on the VPS. The files must be readable by the Docker daemon and should be restricted to the deployment operator:

```text
install -d -m 700 /etc/shuneo/fences-secrets /var/lib/fences
install -m 600 /dev/null /etc/shuneo/fences.env
install -m 600 /dev/null /etc/shuneo/fences-secrets/oidc-signing.pfx
install -m 600 /dev/null /etc/shuneo/fences-secrets/oidc-encryption.pfx
```

Generate or provision separate private-key PFX files for signing and encryption. Do not reuse the same certificate for both purposes. Put their passwords in `/etc/shuneo/fences.env`:

```text
ASPNETCORE_ENVIRONMENT=Production
Authentication__GitHub__ClientId=<github-client-id>
Authentication__GitHub__ClientSecret=<github-client-secret>
IdentityApp__OidcIssuer=https://identity.eldervibe.dev
IdentityApp__OidcSigningCertificatePath=/run/secrets/oidc-signing.pfx
IdentityApp__OidcSigningCertificatePassword=<signing-pfx-password>
IdentityApp__OidcEncryptionCertificatePath=/run/secrets/oidc-encryption.pfx
IdentityApp__OidcEncryptionCertificatePassword=<encryption-pfx-password>
IdentityApp__IdentityDatabasePath=/var/lib/fences/identity.db
IdentityApp__DataProtectionKeysPath=/var/lib/fences/keys
IdentityApp__OidcClients__0__ClientId=<client-id>
IdentityApp__OidcClients__0__DisplayName=<client-name>
IdentityApp__OidcClients__0__RedirectUris__0=https://<app-host>/oauth/callback
IdentityApp__OidcClients__0__PostLogoutRedirectUris__0=https://<app-host>/
```

`Fences/scripts/deploy-fences.sh` is the service-specific wrapper for `deployment/deploy-caddy-service.sh`. It mounts `/etc/shuneo/fences-secrets` read-only inside the container at `/run/secrets`, mounts `/var/lib/fences` for persistent state, and creates the Caddy site for `identity.eldervibe.dev`.

After loading the image on the VPS, run:

```text
FENCES_IMAGE=shuneo-fences:<image-tag> \
	bash Fences/scripts/deploy-fences.sh
```

The shared deployment script validates the Caddy configuration, reloads Caddy, replaces the container, and passes `/etc/shuneo/fences.env` to Docker. It does not create application secrets, certificates, DNS records, or persistent directories.

The deployment lock is service-specific by default. The managed Caddy fragment is named from the service domain. The script never enumerates, rewrites, disables, or prunes unrelated Caddy sites or Docker images.

The VPS Caddy configuration and DNS remain operator-managed. The GitHub OAuth callback is `https://identity.eldervibe.dev/signin-github`.

## GitHub Actions deployment

`.github/workflows/deploy-fences.yml` can be started with **Run workflow**. It calls `.github/workflows/reusable-deploy-service.yml`, which checks out the repository, builds and transfers the image over SSH, then streams the common deployment script and the Fences wrapper. The workflow does not configure DNS, Caddy, application secrets, or certificates.

Configure a GitHub `production` environment with these secrets:

- `VPS_HOST`: VPS hostname or IP address
- `VPS_PORT`: SSH port
- `VPS_USER`: SSH user with permission to run Docker
- `VPS_SSH_KEY`: private key for that user
- `VPS_KNOWN_HOSTS`: pinned `ssh-keyscan` output for the VPS host and port

Before the first workflow deployment, install Docker and Caddy on the VPS, grant the SSH user Docker access, create `/etc/shuneo/fences.env`, create `/etc/shuneo/fences-secrets` with both PFX files, and create durable writable `/var/lib/fences` storage. Caddy must import `/etc/caddy/sites/*.caddy`; DNS for `identity.eldervibe.dev` must point to the VPS and ports 80/443 must be available. Keep the environment file and certificate files readable only by the deployment operator or its Docker access group.

The SSH user must be able to reload Caddy and use Docker. The deploy script requires `flock`, `systemctl`, and the configured Caddy and Docker binaries.

Future services only need a caller workflow and a service wrapper. The caller should invoke `reusable-deploy-service.yml` with the service image name and tag, Docker context and Dockerfile, wrapper path, common script path, and the wrapper's runtime image environment variable name. The wrapper should export the service-specific deployment values and execute the transferred common script, following `Fences/scripts/deploy-fences.sh` as the template.
