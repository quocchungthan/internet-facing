# Bánh Vẽ

I spare this shit for “unemployed” days.

---

## Repo structure
A dotnet solution which contains:
- The main website for SEO deployed at the maindomain. And this also presents my main specialty.
- Each branch starts with `sub/<subdomain>` are deployed at `https://<subdomain>.<maindomain>`. They rebased the main branch whenever the mainbranch changes.
- Whenever they have shared code, there should be a cherry-pick PR to `main`

The Fences identity service serves both `identity.shuneo.com` for compatibility and the canonical OIDC issuer `identity.eldervibe.dev`. See `Fences/README.md` for persistent storage, certificates, and runtime environment requirements.

## Caddy ingress

The main branch owns the shared Caddy ingress baseline. Install Caddy on the VPS as a systemd service, place `deployment/caddy/Caddyfile` at `/etc/caddy/Caddyfile`, and create `/etc/caddy/sites`. The base file imports `/etc/caddy/sites/*.caddy`; each `sub/*` deployment owns only its own domain fragment.

Set `ACME_EMAIL` in the ingress service environment when an email should be registered for ACME account notices. For example, add `Environment=ACME_EMAIL=ops@example.com` to the Caddy service override, then run `systemctl daemon-reload` and `systemctl restart caddy`. Keep Caddy's data directory persistent, normally `/var/lib/caddy/.local/share/caddy`, so certificates and ACME state survive service restarts and image or package upgrades. The deployment workflow may pass the same variable to the remote wrapper, but the ingress service environment is the source of truth.

Allow inbound TCP ports 80 and 443 and point each managed DNS record at the VPS. Caddy obtains and renews certificates automatically after a service fragment is installed. Caddy-managed domains do not use Certbot or nginx; do not add those tools to the Caddy deployment path.

---
