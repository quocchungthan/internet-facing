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

---