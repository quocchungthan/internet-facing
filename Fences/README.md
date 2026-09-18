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

Before the first deployment, configure the GitHub `hostkey-server` environment. Required secrets are `VPS_HOST`, `VPS_PORT`, `VPS_USER`, `VPS_SSH_KEY`, `VPS_KNOWN_HOSTS`, `FENCES_GITHUB_CLIENT_ID`, `FENCES_GITHUB_CLIENT_SECRET`, and `FENCES_OIDC_CLIENT_ID`. Required variables are `FENCES_BRAND_NAME`, `FENCES_COOKIE_DOMAIN`, `FENCES_OIDC_ISSUER`, `FENCES_PERSISTENT_LOGIN_DAYS`, `FENCES_ALLOWED_RETURN_HOST`, `FENCES_ALLOWED_CORS_ORIGIN`, `FENCES_OIDC_CLIENT_NAME`, and `FENCES_OIDC_REDIRECT_URI`. Optional variable: `FENCES_OIDC_POST_LOGOUT_REDIRECT_URI`. In the GitHub Environment UI, require reviewers, restrict deployments to the protected deployment branch, and run this workflow only from that branch; these controls are Environment and branch-protection configuration, not something the YAML can fully enforce. `VPS_USER` must be the root SSH account. The bootstrap workflow assembles these fields into the runtime environment input and never logs their values. It must not contain certificate paths, certificate passwords, or persistent-storage paths; those values are provisioner-managed.

Run **Bootstrap Fences OIDC certificates** manually from GitHub Actions and type the exact confirmation `BOOTSTRAP_FENCES`. The workflow pins SSH host verification with `VPS_KNOWN_HOSTS`, transfers the bootstrap input in a mode `600` temporary file, and never logs or exports its contents. It first refuses when either final Fences output exists. It creates `/etc/shuneo` and `/var/lib/fences` only when absent; existing directories are not changed and must already be root-owned, non-symlink directories. `/etc/shuneo` must not be group- or other-writable, and `/var/lib/fences` must be mode `700`. It then transfers the provisioner into a root-only temporary directory on the VPS.

For an offline root-session alternative, create a runtime environment input from [`Fences/fences.env.example`](fences.env.example). The same template rules apply.

```text
sudo install -o root -g root -m 600 Fences/fences.env.example /root/fences.bootstrap.env
sudoedit /root/fences.bootstrap.env
sudo bash Fences/scripts/provision-oidc-certificates.sh --runtime-env-file /root/fences.bootstrap.env
```

The provisioner requires a regular root-owned input file with mode `600`; it reads it as data and never sources it. It preserves comments and dotenv values, rejects malformed or duplicate assignments, blank required values, certificate password overrides, and managed-value conflicts. At least one OIDC client must provide a nonblank `ClientId` and matching exact `RedirectUris__0`. It uses only `/etc/shuneo/fences-secrets` and requires `/etc/shuneo` to be root-owned, non-symlinked, and not group- or other-writable before staging the complete secret set in a root-owned temporary sibling directory.

On success, `/etc/shuneo/fences-secrets` is root-owned mode `700`, both PFX files are root-owned mode `600`, and `/etc/shuneo/fences.env` is root-owned mode `600`. The final environment includes the operator configuration plus authoritative certificate paths, generated certificate passwords, and persistent-storage paths. Runtime values remain on the VPS.

```text
sudo stat -c '%a %U:%G %n' /etc/shuneo/fences.env /etc/shuneo/fences-secrets /etc/shuneo/fences-secrets/oidc-*.pfx
sudo test -s /etc/shuneo/fences.env
sudo test -s /etc/shuneo/fences-secrets/oidc-signing.pfx
sudo test -s /etc/shuneo/fences-secrets/oidc-encryption.pfx
```

The bootstrap is intentionally first-time-only and refuses to run if either final destination exists. If an installation failure leaves a destination behind, inspect it in a private root session, remove only the incomplete Fences material after confirming it contains nothing needed, then rerun the complete bootstrap. Certificate rotation requires a separate planned replacement procedure.

## Updating runtime environment values after bootstrap

Use **Update Fences runtime environment** (`update-fences-env.yml`) to change values such as an OIDC client secret or redirect URI on an already-bootstrapped host, without touching `/etc/shuneo/fences-secrets` or its certificates. Type the exact confirmation `UPDATE_FENCES_ENV`, and only the secrets/variables you actually set (for example `FENCES_OIDC_CLIENT_SECRET`) are applied; unset values are left unchanged. It refuses to run unless `/etc/shuneo/fences.env` and `/etc/shuneo/fences-secrets` already exist, edits `fences.env` in place while preserving its ownership and mode, and rejects any attempt to change certificate paths, certificate passwords, the identity database path, or the Data Protection keys path. When the `restart` input is left at its default `true`, it recreates the `shuneo-fences` container from its currently running image so the new values take effect immediately; it does not touch the Caddy configuration or rebuild the image.

`Fences/scripts/deploy-fences.sh` is the service-specific wrapper for `deployment/deploy-caddy-service.sh`. It mounts `/etc/shuneo/fences-secrets` read-only inside the container at `/run/secrets`, mounts `/var/lib/fences` for persistent state, and creates the Caddy site for `identity.eldervibe.dev`.

For a manual deployment after bootstrap, run the root-only wrapper as root:

```text
sudo env FENCES_IMAGE=shuneo-fences:<image-tag> \
	bash Fences/scripts/deploy-fences.sh
```

The shared deployment script validates the Caddy configuration, reloads Caddy, replaces the container, and passes `/etc/shuneo/fences.env` to Docker. It does not create application secrets, certificates, DNS records, or persistent directories.

The deployment lock is service-specific by default. The managed Caddy fragment is named from the service domain. The script never enumerates, rewrites, disables, or prunes unrelated Caddy sites or Docker images.

The VPS Caddy configuration and DNS remain operator-managed. The GitHub OAuth callback is `https://identity.eldervibe.dev/signin-github`.

## Automated image deployment

The automated deployment transfers and starts the image only; it does not configure DNS, application runtime values, or certificates. Configure `VPS_USER` as the root SSH account: the shared deployment script performs Caddy and Docker operations that require root, and the Fences wrapper rejects non-root execution before it reads root-only runtime files. There is no GitHub runtime dotenv secret or runtime-configuration transport. The deploy preflight requires `/etc/shuneo` to be root-owned, non-symlinked, and not group- or other-writable; `/etc/shuneo/fences-secrets` must be root-owned mode `700`; and the environment/PFX files must be root-owned mode `600`, all regular non-symlink paths. The deploy script requires `flock`, `systemctl`, and the configured Caddy and Docker binaries.
