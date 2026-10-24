# eldervibe.dev

**This repository is a .NET solution for deploying open-source services and plugins. We dont re-invent the wheels, we attach to them**. Branches named `sub/<subdomain>` are deployed as subdomains and rebase from `main` when shared changes are needed.

## Ecosystem

- [ ] Affine
- [ ] Self-writing .NET + angular tools (try reducing this)
- [ ] Azure Devops
- [ ] Github
- [ ] Windows machine
- [ ] Mac Pro installing Ubuntu
- [ ] Ubuntu VPS
- [ ] Gmail
- [ ] Copilot CLI
- [ ] Antigravity CLI
- [ ] Claude CLI
- [ ] Seafile
- [ ] 3D model files.
- [ ] 3D Studio from this forked [repo](https://github.com/quocchungthan/everything#creative-tools)

## Projects

- In Private notes

## Git hooks

Shared hooks live in `githooks/` (not `.git/hooks`, since `.git` isn't versioned). After cloning, run `scripts/Install-GitHooks.ps1` once to activate them via `core.hooksPath`. The `pre-commit` hook scans staged changes for likely secrets (API keys, private keys, tokens, `.env` files, embedded credentials) and blocks the commit if found; bypass a false positive with `git commit --no-verify`.

## Caddy ingress

We use Caddy for coordinating which .NET service serve a specific sub/domain + feature. It also automatically register self-sign SSL cert.

## Backups

There's a github action running daily and back up data of all the services currently run on my VPS, eventually zip them and we can use `scp` to pick up.

### Non-root deployment user

Too tired of typing script on the VPS, I set a non-root user in the github piplines so whenever we need anything, just click.
