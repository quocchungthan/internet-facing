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
- `GET /connect/authorize` supports the authorization code flow (PKCE per client, see below) and uses the existing GitHub login
- `POST /connect/token` exchanges authorization codes for tokens

## Configuration

Set OAuth secrets with environment variables rather than committing them:

- `Authentication__GitHub__ClientId`
- `Authentication__GitHub__ClientSecret`

`IdentityApp:CookieDomain`, `AllowedReturnHosts`, and `AllowedCorsOrigins` default to the eldervibe.dev services (`identity`, `storage`, `note`, `mail`). The production cookie is `eldervibe.sso`, secure, HTTP-only, SameSite Lax, and shared across `.eldervibe.dev` when configured. Return URLs are limited to relative paths or the configured allowed hosts.

OIDC uses OpenIddict 7.0.0 with EF Core SQLite persistence. The stable subject is `github:<numeric-github-id>`, so a GitHub rename does not change the OIDC subject. The issuer is `https://identity.eldervibe.dev`.

`IdentityApp:OidcClients` is an indexed array and registers as many clients as needed at startup; clients that already exist are updated in place from the current configuration on every start. Each client ID must match the consuming service's configured ID exactly. Each client needs a `ClientId`, one or more exact `RedirectUris`, and optional `DisplayName`, `ClientSecret`, `RequirePkce`, and `PostLogoutRedirectUris`; do not add wildcard redirect URIs.

PKCE policy: a client without `ClientSecret` is public and must use PKCE. A client with `ClientSecret` is confidential and PKCE is optional, unless `RequirePkce=true` forces it (used for `note`/AFFiNE). A missing `RequirePkce` means `false`.

```text
IdentityApp__OidcClients__0__ClientId=mail
IdentityApp__OidcClients__0__DisplayName=Mail
IdentityApp__OidcClients__0__RedirectUris__0=https://mail.eldervibe.dev/auth/github/callback
IdentityApp__OidcClients__0__PostLogoutRedirectUris__0=https://mail.eldervibe.dev/
IdentityApp__OidcClients__1__ClientId=storage
IdentityApp__OidcClients__1__DisplayName=Storage
IdentityApp__OidcClients__1__RedirectUris__0=https://storage.eldervibe.dev/oauth/callback/
IdentityApp__OidcClients__1__PostLogoutRedirectUris__0=https://storage.eldervibe.dev/
IdentityApp__OidcClients__2__ClientId=note
IdentityApp__OidcClients__2__DisplayName=Note
IdentityApp__OidcClients__2__RequirePkce=true
IdentityApp__OidcClients__2__RedirectUris__0=https://note.eldervibe.dev/oauth/callback
IdentityApp__OidcClients__2__PostLogoutRedirectUris__0=https://note.eldervibe.dev/
```

All three clients are confidential: also set `IdentityApp__OidcClients__<n>__ClientSecret` for each from the GitHub environment secret (AFFiNE only enables its OIDC provider when a client secret is configured).

The single **Deploy Fences** action reads the full ordered client list from the `FENCES_OIDC_CLIENTS_JSON` GitHub Environment secret: a JSON array of `{ "clientId", "redirectUri", "displayName", "clientSecret", "requirePkce", "postLogoutRedirectUri" }` objects. The array order defines the contiguous client slots, starting at zero; do not include `index`. A blank/omitted `clientSecret` registers a public PKCE client; `requirePkce` is a JSON boolean and defaults to `false`. For example (set each confidential `clientSecret` only inside the GitHub secret):

```json
[
  { "clientId": "mail", "displayName": "Mail", "redirectUri": "https://mail.eldervibe.dev/auth/github/callback", "postLogoutRedirectUri": "https://mail.eldervibe.dev/" },
  { "clientId": "storage", "displayName": "Storage", "redirectUri": "https://storage.eldervibe.dev/oauth/callback/", "postLogoutRedirectUri": "https://storage.eldervibe.dev/" },
  { "clientId": "note", "displayName": "Note", "requirePkce": true, "redirectUri": "https://note.eldervibe.dev/oauth/callback", "postLogoutRedirectUri": "https://note.eldervibe.dev/" }
]
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

Before the first deployment, configure the GitHub `hostkey-server` environment. Required secrets are `VPS_HOST`, `VPS_PORT`, `VPS_USER`, `VPS_SSH_KEY`, `VPS_KNOWN_HOSTS`, `FENCES_GITHUB_CLIENT_ID`, `FENCES_GITHUB_CLIENT_SECRET`, and `FENCES_OIDC_CLIENTS_JSON`. Required variables are `FENCES_BRAND_NAME`, `FENCES_COOKIE_DOMAIN`, `FENCES_OIDC_ISSUER`, `FENCES_PERSISTENT_LOGIN_DAYS`, `FENCES_ALLOWED_RETURN_HOST`, and `FENCES_ALLOWED_CORS_ORIGIN`. Optional: `FENCES_DEPLOY_DIR` (Environment variable) and `ACME_EMAIL` (secret or variable). Each OIDC client requires `clientId` and an HTTPS `redirectUri`; optional fields are `displayName`, `clientSecret`, `requirePkce` (boolean), and `postLogoutRedirectUri`. For eldervibe.dev set `FENCES_COOKIE_DOMAIN=.eldervibe.dev`, `FENCES_OIDC_ISSUER=https://identity.eldervibe.dev`, `FENCES_ALLOWED_RETURN_HOST=identity.eldervibe.dev`, and `FENCES_ALLOWED_CORS_ORIGIN=https://identity.eldervibe.dev`; these override index 0 of the application defaults. Require Environment reviewers and restrict deployments to the protected `sub/identity` branch in GitHub settings; invoke the action only from that branch. `VPS_USER` must be root. Certificate paths, generated passwords, and storage paths are provisioner-managed and must not be GitHub secrets/variables.

Run **Deploy Fences** manually from GitHub Actions. It builds the image, validates the complete desired configuration, and transfers it into a mode `600` root-only staging area with pinned SSH host verification. On first run it provisions certificates and persistent directories; on later runs it reconciles GitHub values, loads the image, then uses a candidate health check and Caddy rollback before switching traffic. Existing certificates, Data Protection keys, and SQLite data are retained. The deployment action and **Backup All Services** serialize through one GitHub concurrency group; run a verified backup before upgrades involving schema, issuer, credentials, or deployment-directory changes. Never run this from an untrusted branch with production Environment access.

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

The provisioner is intentionally first-time-only and refuses to run if either final destination exists. If a first deployment fails after provisioning, leave the generated material intact and rerun **Deploy Fences**; inspect incomplete state privately if it does not pass preflight. Certificate rotation requires a separate planned replacement procedure.

## Updating runtime environment values after bootstrap

Change the relevant GitHub Environment secret/variable, then run **Deploy Fences** again. Missing required values fail preflight; the full ordered OIDC array replaces old OIDC settings, including optional fields omitted from an entry. Newly added clients are registered and existing clients are updated on candidate startup. Removing an existing client is refused: OpenIddict keeps registrations in SQLite even when their configuration disappears, so removal needs an explicit planned database migration. Changing the GitHub secret alone does not update the VPS. The updater preserves generated certificate passwords and persistent-storage paths. On a failed deployment of an existing installation, the previous environment file is restored; candidate startup may already have changed OIDC registrations in SQLite, so validate identity flows and restore from a verified backup if needed. A failure after Caddy has switched traffic may require operator inspection of the active candidate. Never assume the runtime-file rollback also rolls back database mutations.

`Fences/scripts/deploy-fences.sh` is the service-specific wrapper for `deployment/deploy-caddy-service.sh`. It mounts `/etc/shuneo/fences-secrets` read-only inside the container at `/run/secrets`, mounts `/var/lib/fences` for persistent state, and creates the Caddy site for `identity.eldervibe.dev`.

For a manual deployment after bootstrap, run the root-only wrapper as root:

```text
sudo env FENCES_IMAGE=shuneo-fences:<image-tag> \
	bash Fences/scripts/deploy-fences.sh
```

The shared deployment script validates the Caddy configuration, reloads Caddy, replaces the container, and passes `/etc/shuneo/fences.env` to Docker. It does not create application secrets, certificates, DNS records, or persistent directories.

The deployment lock is service-specific by default. The managed Caddy fragment is named from the service domain. The script never enumerates, rewrites, disables, or prunes unrelated Caddy sites or Docker images.

The VPS Caddy configuration and DNS remain operator-managed. The GitHub OAuth callback is `https://identity.eldervibe.dev/signin-github`.

### Optional consolidated deployment directory

Set `FENCES_DEPLOY_DIR` as a **GitHub Environment variable** in `hostkey-server` (not a secret) to an absolute Linux path such as `/opt/fences`. Only root-owned, non-symlink ancestors without group/other write access are accepted; no empty, `.` or `..` path segments are allowed. The variable is consumed by **Deploy Fences** and **Backup All Services**. When unset, both keep the legacy `/etc/shuneo/fences.env`, `/etc/shuneo/fences-secrets`, and `/var/lib/fences` layout. With the variable set, the files live at `<FENCES_DEPLOY_DIR>/fences.env`, `<FENCES_DEPLOY_DIR>/fences-secrets/`, and `<FENCES_DEPLOY_DIR>/data/`. The directory and data/secrets directories are root-owned mode `700`; the environment and PFX files are mode `600`. The container still sees the database and Data Protection keys at `/var/lib/fences/identity.db` and `/var/lib/fences/keys`, and the PFX files at `/run/secrets/`. Caddy's site configuration and ACME data remain system-managed in `/etc/caddy` and Caddy's own data directory, backed up separately.

On a new host, set the variable **before** the first deployment; the provisioner refuses existing Fences outputs or a populated data directory. On an existing host, do not just set the variable: deployment refuses to switch while legacy material exists or while the running container mounts a different directory. In a root maintenance session, first run **Backup All Services** with the variable still unset and verify that the archive contains the environment, both PFX files, the database snapshot, and keys (install `sqlite3` on the VPS for a consistent live Fences snapshot). Stop the Fences container, create the chosen root-only directory, and copy the legacy `fences.env`, `fences-secrets/`, and **all** of `/var/lib/fences/` (including SQLite WAL files and Data Protection keys) into the new layout. Verify ownership, modes and byte-for-byte contents against the originals before moving the old Fences-only paths to a private quarantine outside `/etc/shuneo` and `/var/lib/fences`; do not delete the archive or quarantine until the new instance is healthy. Leave unrelated `/etc/shuneo` content in place. Then set `FENCES_DEPLOY_DIR`, run **Deploy Fences**, check login/OIDC and persistence, and run **Backup All Services** again to verify archive and generated `RESTORE.md` target the new layout. Do not rerun bootstrap or regenerate PFX passwords during migration. If deployment fails, restore the original paths and unset the variable before redeploying the old installation. Moving between two custom directories also requires an explicit backup and migration; when no container exists the action cannot discover arbitrary old directories. The backup captures any remaining legacy Fences files separately when the variable is set, but refuses an inconsistent Fences SQLite snapshot. Merge these workflow/script changes into the branch used by the scheduled backup before relying on that run.

## Automated image deployment

The action provisions/reconciles Fences configuration before image deployment; it does not configure DNS, migrate an existing deployment directory, rotate generated certificates, change the SQLite schema, or automatically run a backup. Configure `VPS_USER` as root: the service wrapper checks root-only runtime files and the shared ingress script manages Caddy and Docker. The deploy preflight requires a root-owned non-symlink config parent without group/other write access, a mode `700` secrets directory, and mode `600` regular environment/PFX files. The VPS requires `openssl`, `flock`, `systemctl`, Caddy, and Docker. No deployment or VPS modification is performed by merely editing these files.
