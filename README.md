# eldervibe.dev

I dont invent new stuff.
> I deploy opensources and plugins for the well-known existing systems.
I find ways to use the resources more proficient.

---

## Repo structure
A dotnet solution which contains:
- The main website for SEO deployed at the maindomain. And this also presents my main specialty.
- Each branch starts with `sub/<subdomain>` are deployed at `https://<subdomain>.<maindomain>`. They rebased the main branch whenever the mainbranch changes.
- Whenever they have shared code, there should be a cherry-pick PR to `main`

The Fences identity service serves both `identity.shuneo.com` for compatibility and the canonical OIDC issuer `identity.eldervibe.dev`. See `Fences/README.md` for persistent storage, certificates, and runtime environment requirements.

## Azure DevOps tools

`Farm.Core` owns provider-neutral resource contracts, workspace export specifications/manifests, and safe local workspace rules. `Farm.Azure` is the Azure DevOps adapter and maps Azure SDK responses into Core contracts. `Farm.Console` is a Linux-capable CLI/bootstrap entry point and owns runtime configuration loading.

Set these environment variables before running the console:

- `FARM_AZURE_DEVOPS_ORGANIZATION_URL`
- `FARM_AZURE_DEVOPS_PROJECT`
- `FARM_AZURE_DEVOPS_PAT`

Then query work items with `dotnet run --project Farm.Console -- work-items 123 456`. Credentials are never stored in source files.

CI publishes `Farm.Console` as a local .NET tool package artifact. Download the `farm-console-tool` artifact on a Linux machine, then run `dnx --source /path/to/farm-console-tool Farm.Console -- work-items 123 456` with the same environment variables. The console is not exposed by the Farm web application.

Run `powershell -ExecutionPolicy Bypass -File scripts/Invoke-FarmValidation.ps1 -Scope Core` for Core-only validation, or omit `-Scope` to validate the full solution.

## Caddy ingress

The main branch owns the shared Caddy ingress baseline. Install Caddy on the VPS as a systemd service, place `deployment/caddy/Caddyfile` at `/etc/caddy/Caddyfile`, and create `/etc/caddy/sites`. The base file imports `/etc/caddy/sites/*.caddy`; each `sub/*` deployment owns only its own domain fragment.

Set `ACME_EMAIL` in the ingress service environment when an email should be registered for ACME account notices. For example, add `Environment=ACME_EMAIL=ops@example.com` to the Caddy service override, then run `systemctl daemon-reload` and `systemctl restart caddy`. Keep Caddy's data directory persistent, normally `/var/lib/caddy/.local/share/caddy`, so certificates and ACME state survive service restarts and image or package upgrades. The deployment workflow may pass the same variable to the remote wrapper, but the ingress service environment is the source of truth.

Allow inbound TCP ports 80 and 443 and point each managed DNS record at the VPS. Caddy obtains and renews certificates automatically after a service fragment is installed. Caddy-managed domains do not use Certbot or nginx; do not add those tools to the Caddy deployment path.

Farm deployments start a candidate container on the unused loopback port, require a local HTTP response, then validate and reload Caddy to switch traffic. The previous container remains running until that switch succeeds. The deployer extracts the Vite output from the candidate image into `/var/lib/caddy/farm/hanging-post/releases`, makes it readable by the `caddy` service account, and Caddy directly serves only `/hanging-post/assets/*` and `/hanging-post/favicon.svg`. Requests for `/hanging-post/index.html` continue to reach Farm; there is no Caddy SPA fallback.

### Non-root deployment user

`VPS_USER` may be a dedicated non-root deploy user. It must have Docker daemon access through the `docker` group; the provisioning script adds its invoking non-root user to that group. When direct Docker access is unavailable, deployment falls back to `sudo -n docker` and requires that command to be passwordless.

The deploy user must also have passwordless, noninteractive sudo access for the shared ingress operations: creating and maintaining `/etc/caddy/sites`, writing and restoring `/etc/caddy/sites/<domain>.caddy` at mode `0644`, creating and maintaining `/var/lib/caddy/farm/**` at Caddy-readable directory/file modes, creating `/var/lock/deploy-caddy.lock`, and `systemctl reload caddy`. These capabilities are intentionally limited to deployment paths and Caddy reloads; do not grant unrestricted `ALL` sudo access. The deployment script checks `sudo -n -l` before creating a candidate and exits with a clear error if this contract is missing.

---