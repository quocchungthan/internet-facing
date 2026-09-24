# eldervibe.dev

This repository is a .NET solution for deploying open-source services and plugins. Branches named `sub/<subdomain>` are deployed as subdomains and rebase from `main` when shared changes are needed.

## Projects

- [Farm.Console](Farm.Console/README.md) is the Azure DevOps CLI and .NET tool, installed as `sam`.
- [Farm.Sandbox.Chickens](Farm.Sandbox.Chickens/README.md) is the scheduled Azure DevOps review-feedback worker and its container deployment.
- [Fences](Fences/README.md) documents the identity service and its persistent runtime requirements.

The solution entry point is `PiggyFarm.slnx`. Use `scripts/Invoke-FarmValidation.ps1` for repository validation and add `-AuditPackages` for the NuGet vulnerability audit.

## Caddy ingress

The main branch owns the shared Caddy ingress baseline. Install Caddy on the VPS as a systemd service, place `deployment/caddy/Caddyfile` at `/etc/caddy/Caddyfile`, and create `/etc/caddy/sites`. The base file imports `/etc/caddy/sites/*.caddy`; each `sub/*` deployment owns only its own domain fragment.

Set `ACME_EMAIL` in the ingress service environment when an email should be registered for ACME account notices. Keep Caddy's data directory persistent, normally `/var/lib/caddy/.local/share/caddy`, so certificates and ACME state survive restarts and upgrades.

Allow inbound TCP ports 80 and 443 and point managed DNS records at the VPS. Caddy obtains and renews certificates automatically. Caddy-managed domains do not use Certbot or nginx.

Farm deployments start a candidate container on an unused loopback port, require a local HTTP response, validate, and reload Caddy before switching traffic. The previous container remains running until the switch succeeds. Static Vite output is extracted into `/var/lib/caddy/farm/hanging-post/releases`; Caddy serves only `/hanging-post/assets/*` and `/hanging-post/favicon.svg` directly.

### Non-root deployment user

`VPS_USER` may be a dedicated non-root deploy user with Docker daemon access through the `docker` group. When direct Docker access is unavailable, deployment falls back to passwordless `sudo -n docker`.

The deploy user needs narrowly scoped, passwordless sudo for maintaining `/etc/caddy/sites`, `/var/lib/caddy/farm/**`, `/var/lock/deploy-caddy.lock`, and reloading Caddy. Do not grant unrestricted `ALL` sudo access. The deployment script checks `sudo -n -l` before creating a candidate.
