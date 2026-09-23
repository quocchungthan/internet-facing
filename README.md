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

`Farm.Console` is the project and package ID; its installed command is `sam`. Running it with no arguments prints the tool version and full command list, exiting `0`; `sam help`, `sam help <command>`, `--help`/`-h`, and `--version`/`-v` are supported. The command metadata rendered by `sam help` is authoritative; this README is a summary. Query commands render compact Spectre.Console tables. Direct work-item ID lookups render a separate detailed panel per item and remain readable when redirected. Available commands:

- `work-items <id> [<id> ...]` — show full detail per Azure DevOps work item, including identity, paths, audit dates and users, wrapped plain-text description, tags, URL, attachments, and relations (fully implemented).
- `work-items assigned-to <email-or-me>` — list work items assigned to the given user (unique name/email), or the current authenticated user when passed `me` (fully implemented, WIQL-based).
- `work-items needs-attention` — list unassigned, non-terminal work items in the configured team's current iteration, using the WIQL `@CurrentIteration` macro and configurable terminal-state exclusions (fully implemented).
- `work-items assign <id> me` — assign one work item to the current authenticated Azure DevOps identity (fully implemented).
- `work-items assign <id> <email-or-unique-name>` — assign one work item to an explicit Azure DevOps identity (fully implemented).
- `work-items unassign <id>` — remove the current assignment from one work item (fully implemented).
- `whoami` — print the current authenticated identity: id, display name, unique name (fully implemented).
- `my-groups` — list the groups/teams the current authenticated user belongs to (fully implemented).
- `pull-requests` — list active pull requests in the configured project (fully implemented).
- `pull-requests approved-by-me` — list active pull requests where the current user is a reviewer who has approved (vote &gt;= 5) (fully implemented).
- `pull-requests assigned-to-me` — list active pull requests where the current user is a reviewer, directly or via a group they belong to (fully implemented).
- `pull-requests pending-review` — of the PRs assigned to the current user (direct or via group), those where the applicable direct or group reviewer vote is still 0 (fully implemented).
- `pull-requests mine` — list active pull requests created by the current user (fully implemented).
- `pr-threads <pr-id>` — list discussion threads on a pull request, showing status (resolved vs unresolved), comment count, and a preview of the first comment (fully implemented).
- `pr-diff <pr-id>`, `work-item-comments <id>`, `work-item-relations <id>` — scaffolded commands that validate their arguments but exit `3` with a "not yet implemented" message, since the backing Farm.Azure/Farm.Core clients don't exist yet.

Show detailed work items with `dotnet run --project Farm.Console -- work-items 123 456`. List compact reports with `dotnet run --project Farm.Console -- work-items assigned-to me` or `dotnet run --project Farm.Console -- work-items needs-attention`. Mutate assignment with `dotnet run --project Farm.Console -- work-items assign 123 me`, `dotnet run --project Farm.Console -- work-items assign 123 person@example.com`, or `dotnet run --project Farm.Console -- work-items unassign 123`. Successful mutations print a concise confirmation and the full updated work-item detail. Credentials are never stored in source files.

The PAT configured via `FARM_AZURE_DEVOPS_PAT` needs these Azure DevOps PAT UI scopes, least-privilege (OAuth scope identifiers are included for clarity):

- **User profile (Read)** (`vso.profile`) — required by `whoami` and by `work-items assign <id> me` to resolve the authenticated user's profile.
- **Identity (Read)** (`vso.identity`) — required by `my-groups` and PR reviewer/group resolution, including group membership for `pull-requests assigned-to-me` and `pull-requests pending-review`.
- **Work Items (Read)** (`vso.work`) — read-only use of `work-items`, `work-items assigned-to`, and `work-items needs-attention`.
- **Work Items (Read & write)** (`vso.work_write`) — required instead when using `work-items assign` or `work-items unassign`; it also covers the read-only work-item commands.
- **Code (Read)** (`vso.code`) — `pull-requests`, `pull-requests approved-by-me`, `pull-requests assigned-to-me`, `pull-requests pending-review`, `pull-requests mine`, and `pr-threads`.

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

Run `powershell -ExecutionPolicy Bypass -File scripts/Invoke-FarmValidation.ps1 -Scope Core` for Core-only validation, or omit `-Scope` to validate the full solution. Add `-AuditPackages` to audit direct and transitive NuGet dependencies for known vulnerabilities after tests pass.

## Farm.Sandbox.Chickens

`Farm.Sandbox.Chickens` is a fixed-delay scheduled Generic Host that resolves the current user's Azure DevOps PR feedback. It only selects active PRs authored by the authenticated user with unresolved external comments whose newest external comment is older than the configured quiet period. It never marks a thread resolved. When publishing is explicitly enabled, validated changes are committed and pushed to the PR source branch with an exact force-with-lease.

The workflow uses direct project references: `Farm.Azure` loads PR/work-item context, including both diff anchors and available iteration/commit context. `Farm.Git` treats the mounted repository as a read-only seed and maintains a private writable clone/cache. Target history and the configured base branch are fetched from target `origin`; the PR source is fetched and pushed through the dedicated private-clone remote `chickens-source`, including for forks. It creates a per-PR/head isolated branch/worktree and rebases it with `--autostash --committer-date-is-author-date` onto the exact fetched base commit. It never mutates the mounted seed. Rebase conflicts remain only in the private worktree for Copilot to resolve before review-driven edits. `Farm.Copilot` runs the Copilot SDK in that worktree. `Farm.State.Sqlite` atomically combines successful-completion suppression with renewable lease acquisition keyed by organization/project/repository/PR/head SHA/external-feedback fingerprint. `Farm.Console`/`sam` is not invoked or referenced.

Required environment variables:

- `FARM_AZURE_DEVOPS_ORGANIZATION_URL`, `FARM_AZURE_DEVOPS_PROJECT`, `FARM_AZURE_DEVOPS_PAT`
- `FARM_CHICKENS_REPOSITORY_PATH` - mounted, pre-cloned, read-only Git seed
- `FARM_CHICKENS_CACHE_PATH`, `FARM_CHICKENS_WORKTREES_PATH`, `FARM_CHICKENS_ARTIFACTS_PATH`, `FARM_CHICKENS_STATE_PATH`, `FARM_CHICKENS_LOCK_PATH`
- `FARM_CHICKENS_GIT_USER_NAME`, `FARM_CHICKENS_GIT_USER_EMAIL`
- `FARM_CHICKENS_BASE_BRANCH` - branch name fetched from `origin` and used as the exact rebase base

Optional environment variables:

- `FARM_CHICKENS_SCHEDULE_SECONDS`, `FARM_CHICKENS_QUIET_PERIOD_SECONDS`, and `FARM_CHICKENS_LEASE_SECONDS` (default `3600`)
- `FARM_CHICKENS_RUN_IMMEDIATELY` (default `true`)
- `FARM_CHICKENS_ENABLE_PUSH` (default `false` for direct runs; canonical Compose requires an explicit value and production publishing requires `true`)
- `FARM_CHICKENS_GIT_AUTH_TOKEN`, `FARM_CHICKENS_GIT_AUTH_USER` (default user `x-access-token`; Azure PAT is used when no dedicated token is set)
- `FARM_CHICKENS_VALIDATION_COMMANDS_JSON`, for example `[{"FileName":"dotnet","Arguments":["test","PiggyFarm.slnx","--no-restore"]}]`
- `FARM_CHICKENS_SAFE_PROCESS_ENV_JSON` - optional JSON object of explicitly non-secret variables added to validation and Copilot runtime environments
- `FARM_CHICKENS_COPILOT_MODEL`, `FARM_CHICKENS_COPILOT_RESOURCES_PATH`, `FARM_CHICKENS_COPILOT_PROMPT_PATH`, `FARM_CHICKENS_COPILOT_AGENT`

The Copilot purpose and safety rules are hardcoded and always prepended: resolve any rebase conflict first, then make focused code changes for unresolved feedback or provide a reviewer explanation, and never resolve review threads, commit, or push. A mounted prompt is supplemental and cannot replace that purpose; mounted agents/resources remain optional. Application permission checks are deny-by-default, independently of the container boundary: lexical and resolved file paths are constrained to the isolated worktree, and shell commands require exact executable/subcommand tokens for `dotnet build`, `dotnet test`, read-only `git status/diff/log/show`, confined `git add`, or editor-disabled `git rebase --continue/--abort`. URLs, shell control/redirection characters, path escapes, sandbox bypass, agent commit, and agent push are denied.

Compose passes credentials from host environment variables into the Chickens broker process. PR-controlled validation commands and the Copilot runtime instead receive a strict allowlist of executable lookup, home/temp, .NET/NuGet cache, timezone, and locale variables. The Copilot token is sent in the session request rather than the runtime process environment, and Git authentication is injected only into broker-owned Git calls. Copilot-launched and configured `dotnet`/`git` validation children therefore receive no Azure PAT, Copilot/GitHub token, Git auth/askpass setting, NuGet authentication variable, or credentialed proxy URL. Outbound network access remains available unless Compose or host network policy restricts it separately.

Artifact repository segments are normalized to `[A-Za-z0-9_-]`, and the resolved path is verified to remain below the configured artifact root. Before persistence, context, transcript, patch, validation, explanation, publication, and result content is redacted and scanned for configured secret values and high-confidence token, JWT, private-key, password/connection-string, and credentialed-URL signatures; rejected files are removed. Staged content is scanned with the same policy before commit and push.

Each attempt uses `<artifacts>/<repository>/pr-<id>/<fingerprint>/`. Attempts that reach review execution write the baseline metadata `context.json` and `result.json`; failures before context acquisition can only record `result.json` where feasible. Other files are stage-dependent: `transcript.md` requires a transcript or captured exception, while `changes.patch` and `validation.txt` require workspace execution to reach patch and validation collection. `publication.json` records the commit SHA and push result when publication is attempted. `explanation.md` is written only for a valid explanation-only outcome and explanation-only runs never commit or push. Changes require a completed rebase with no unmerged entries, at least one configured validation command, and all commands must pass. Validation command display, process output, exceptions, results, and artifacts are redacted using the configured Azure, Copilot, and Git credentials; raw validation arguments are passed only to the configured process and are not persisted. The runner rejects common secret and artifact paths before staging, creates a deterministic PR/fingerprint commit, and pushes `HEAD` to the source ref with `--force-with-lease=<source-ref>:<original-head-sha>`. The final commit author/committer date comes from the latest unresolved external feedback timestamp, with a stable fingerprint-derived fallback. Rebase uses each original commit's author date as its committer date; therefore retries are deterministic when source/base commits, Git version, conflict resolution, and produced tree are unchanged, but the system does not claim deterministic rewritten history across different Git implementations or conflict resolutions. A missing, inaccessible, or unwritable source repository is deferred before Copilot and remains retryable. A moved remote, disabled push, unresolved conflict, validation failure, or other exception remains failed and retryable without marking the attempt complete. `ChangesProduced` is successful only after push succeeds; successful explanations also suppress the same head and external-feedback fingerprint.

Run directly with `dotnet run --project Farm.Sandbox.Chickens`. The canonical container definition is `deployment/farm-sandbox-chickens.compose.yml`. It exposes no ports and assumes one replica sharing one state/lock directory.

Build the Linux image from Windows with `powershell -ExecutionPolicy Bypass -File scripts/Build-FarmSandboxChickensImage.ps1`. The script removes the canonical output before host validation, restores and tests the solution, publishes `Farm.Sandbox.Chickens` for `linux-x64` to a unique staging directory, and atomically promotes it to `artifacts/farm-sandbox-chickens/linux-x64/` only after publish succeeds. It then runs `docker build` with the repository root as its context. Failed validation or publish leaves no canonical output for Docker to consume. Use `-SkipDockerBuild` to stop after producing the publish directory, or `-ImageTag <name:tag>` to select the local image tag.

The publish directory is the Dockerfile input contract and must exist before `docker compose -f deployment/farm-sandbox-chickens.compose.yml build` or a direct Docker build. Docker only copies that pre-published output; it does not restore, build, test, or publish the Chickens application. The final image intentionally uses the .NET 10 SDK image rather than a runtime-only image because configured validation commands may run `dotnet build` or `dotnet test` against the private cloned source. It also installs Git, CA certificates, timezone data, and `tini` for runtime operations.

Before using Compose, create these absolute host directories: the pre-cloned repository seed, artifacts, state, cache, worktrees, and agent overrides. The repository seed and agent overrides are mounted read-only. The other four directories must be writable by fixed container UID/GID `1654:1654`; on Linux, prepare them with `install -d -o 1654 -g 1654 <artifacts> <state> <cache> <worktrees>`. The container creates only repository, PR, SDK, NuGet, and state content beneath those writable mounts. The agent-overrides directory may be empty.

Set `FARM_CHICKENS_REPOSITORY_HOST_PATH`, `FARM_CHICKENS_ARTIFACTS_HOST_PATH`, `FARM_CHICKENS_STATE_HOST_PATH`, `FARM_CHICKENS_CACHE_HOST_PATH`, `FARM_CHICKENS_WORKTREES_HOST_PATH`, and `FARM_CHICKENS_AGENT_OVERRIDES_HOST_PATH`. Also set the Azure variables, `FARM_CHICKENS_COPILOT_TOKEN`, `FARM_CHICKENS_GIT_USER_NAME`/`FARM_CHICKENS_GIT_USER_EMAIL`, `FARM_CHICKENS_BASE_BRANCH`, and explicitly set `FARM_CHICKENS_ENABLE_PUSH=true` when publication is intended. The authenticated identity must be able to update the current PR source branch; fork PRs require a repository URL and credentials that can write that fork branch. `FARM_CHICKENS_GIT_AUTH_TOKEN` and `FARM_CHICKENS_GIT_AUTH_USER` are optional; when omitted, Git authentication uses the Azure PAT. Compose passes credentials directly from the host environment and never uses Docker secrets or prints token values. Git identity is written only to the private writable clone, never to the read-only seed.

Environment variables are not secret from Docker administrators. The resolved credentials are visible through the Docker API, including container inspection, and `docker compose config` renders interpolated values. Run Chickens under a dedicated host account, restrict interactive access to the container and its writable mounts, and grant Docker daemon access only to trusted administrators because Docker access is effectively root-equivalent. Never publish, attach, or persist full `docker inspect`, `docker compose config`, `env`, `printenv`, shell tracing, or debug output from this service. Validate Compose with `docker compose -f deployment/farm-sandbox-chickens.compose.yml config --quiet`, which checks the model without printing the resolved configuration.

The broker requires outbound network access for Azure DevOps, Copilot, and Git, so `network_mode: none` is not compatible with this service. It exposes no inbound ports. Credentials remain available to the broker process, but configured validation child processes run with a restricted allowlist containing paths, locale, temporary-directory, and .NET/NuGet runtime settings only; Azure, Copilot, Git, and other ambient credentials are removed. A separate validation container is not used because launching one would require a Docker control-plane integration such as a mounted Docker socket, which would expand privileges beyond this deployment.

Container path variables normally need no changes. If overriding them, keep each `*_CONTAINER_PATH` mount destination aligned with its corresponding application path; state and lock files must remain below `/workspace/state`, and SDK/NuGet caches remain below `/workspace/cache` because the root filesystem is read-only.

Run focused tests with `powershell -ExecutionPolicy Bypass -File scripts/Invoke-FarmValidation.ps1 -Scope Chickens`.

## Caddy ingress

The main branch owns the shared Caddy ingress baseline. Install Caddy on the VPS as a systemd service, place `deployment/caddy/Caddyfile` at `/etc/caddy/Caddyfile`, and create `/etc/caddy/sites`. The base file imports `/etc/caddy/sites/*.caddy`; each `sub/*` deployment owns only its own domain fragment.

Set `ACME_EMAIL` in the ingress service environment when an email should be registered for ACME account notices. For example, add `Environment=ACME_EMAIL=ops@example.com` to the Caddy service override, then run `systemctl daemon-reload` and `systemctl restart caddy`. Keep Caddy's data directory persistent, normally `/var/lib/caddy/.local/share/caddy`, so certificates and ACME state survive service restarts and image or package upgrades. The deployment workflow may pass the same variable to the remote wrapper, but the ingress service environment is the source of truth.

Allow inbound TCP ports 80 and 443 and point each managed DNS record at the VPS. Caddy obtains and renews certificates automatically after a service fragment is installed. Caddy-managed domains do not use Certbot or nginx; do not add those tools to the Caddy deployment path.

Farm deployments start a candidate container on the unused loopback port, require a local HTTP response, then validate and reload Caddy to switch traffic. The previous container remains running until that switch succeeds. The deployer extracts the Vite output from the candidate image into `/var/lib/caddy/farm/hanging-post/releases`, makes it readable by the `caddy` service account, and Caddy directly serves only `/hanging-post/assets/*` and `/hanging-post/favicon.svg`. Requests for `/hanging-post/index.html` continue to reach Farm; there is no Caddy SPA fallback.

### Non-root deployment user

`VPS_USER` may be a dedicated non-root deploy user. It must have Docker daemon access through the `docker` group; the provisioning script adds its invoking non-root user to that group. When direct Docker access is unavailable, deployment falls back to `sudo -n docker` and requires that command to be passwordless.

The deploy user must also have passwordless, noninteractive sudo access for the shared ingress operations: creating and maintaining `/etc/caddy/sites`, writing and restoring `/etc/caddy/sites/<domain>.caddy` at mode `0644`, creating and maintaining `/var/lib/caddy/farm/**` at Caddy-readable directory/file modes, creating `/var/lock/deploy-caddy.lock`, and `systemctl reload caddy`. These capabilities are intentionally limited to deployment paths and Caddy reloads; do not grant unrestricted `ALL` sudo access. The deployment script checks `sudo -n -l` before creating a candidate and exits with a clear error if this contract is missing.

---