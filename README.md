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
- `FARM_AZURE_DEVOPS_TEAM` — required by `work-items needs-attention` so Azure DevOps can resolve `@CurrentIteration` in team context.
- `FARM_AZURE_DEVOPS_TERMINAL_STATES` — optional comma-separated completed states excluded by `work-items needs-attention`; defaults to `Done,Closed,Removed`.

`Farm.Console` is the project and package ID; its installed command is `sam`. Running it with no arguments prints the tool version and full command list, exiting `0`; `--help`/`-h` and `--version`/`-v` work standalone or after a command (e.g. `sam work-items --help`). List-style output (work items, pull requests, discussion threads) renders as a Spectre.Console table with colored status columns. Available commands:

- `work-items <id> [<id> ...]` — query one or more Azure DevOps work items by ID (fully implemented).
- `work-items assigned-to <email-or-me>` — list work items assigned to the given user (unique name/email), or the current authenticated user when passed `me` (fully implemented, WIQL-based).
- `work-items needs-attention` — list unassigned, non-terminal work items in the configured team's current iteration, using the WIQL `@CurrentIteration` macro and configurable terminal-state exclusions (fully implemented).
- `whoami` — print the current authenticated identity: id, display name, unique name (fully implemented).
- `my-groups` — list the groups/teams the current authenticated user belongs to (fully implemented).
- `pull-requests` — list active pull requests in the configured project (fully implemented).
- `pull-requests approved-by-me` — list active pull requests where the current user is a reviewer who has approved (vote &gt;= 5) (fully implemented).
- `pull-requests assigned-to-me` — list active pull requests where the current user is a reviewer, directly or via a group they belong to (fully implemented).
- `pull-requests pending-review` — of the PRs assigned to the current user (direct or via group), those where the applicable direct or group reviewer vote is still 0 (fully implemented).
- `pull-requests mine` — list active pull requests created by the current user (fully implemented).
- `pr-threads <pr-id>` — list discussion threads on a pull request, showing status (resolved vs unresolved), comment count, and a preview of the first comment (fully implemented).
- `pr-diff <pr-id>`, `work-item-comments <id>`, `work-item-relations <id>` — scaffolded commands that validate their arguments but exit `3` with a "not yet implemented" message, since the backing Farm.Azure/Farm.Core clients don't exist yet.

Query work items with `dotnet run --project Farm.Console -- work-items 123 456`. Credentials are never stored in source files.

The PAT configured via `FARM_AZURE_DEVOPS_PAT` needs these scopes, least-privilege:

- **Work Items (Read)** — `work-items`, `work-items assigned-to`, `work-items needs-attention`.
- **Identity (Read)** — `whoami`, `my-groups`, and PR reviewer resolution (including group membership for `assigned-to-me`/`pending-review`).
- **Code (Read)** — `pull-requests`, `pull-requests approved-by-me`, `assigned-to-me`, `pending-review`, `mine`, `pr-threads`.

CI publishes `Farm.Console` as a real NuGet package to GitHub Packages (`https://nuget.pkg.github.com/<owner>/index.json`) on every push to `main`, in addition to an ephemeral `farm-console-tool` build artifact. GitHub Packages requires authentication even for reads (public visibility does not exempt the NuGet feed), so add the source with a personal access token that has the `read:packages` scope, then install `sam` as a regular global .NET tool:

```bash
dotnet nuget add source https://nuget.pkg.github.com/<owner>/index.json \
  --name github-internet-facing --username <github-username> --password <PAT> --store-password-in-clear-text

dotnet tool install --global Farm.Console --add-source github-internet-facing
sam work-items 123 456
```

To upgrade to a newer published version: `dotnet tool update --global Farm.Console --add-source github-internet-facing`. To remove it: `dotnet tool uninstall --global Farm.Console`.

`dnx` is not a reliable way to consume this package: GitHub Packages' NuGet v3 feed does not implement the search/autocomplete API some `dnx`/`dotnet tool install` discovery paths depend on, and `dnx`'s credential handling against this feed has been unreliable in practice. Prefer `dotnet tool install --global` with an explicit `--add-source`, which resolves packages by exact ID against the feed directly.

The console is not exposed by the Farm web application.

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
## Kỳ Hội browser game

[KyHoi](KyHoi/README.md) contains an independent Node.js 24+ turn-based room game with IP-based identity, team voting and host moderation. Its PARA documentation and downloaded upstream references live alongside the app. See [setup and tests](KyHoi/Projects/TikTok-Live-Turnbased/README.md). This addition does not register a production deployment.

