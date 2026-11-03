# Users deployment

The `sub/users` branch deploys the private OpenSourceLab image by immutable digest and configures Caddy for `users.eldervibe.dev`. Trigger **Deploy Users** on `sub/users` with the digest from OpenSourceLab's successful image build. The workflow does not run on push.

Configure the `hostkey-server` GitHub environment:

- Secrets: `VPS_HOST`, `VPS_PORT`, `VPS_USER` (root), `VPS_SSH_KEY`, and `VPS_KNOWN_HOSTS`. The workflow passes its short-lived `GITHUB_TOKEN` to the VPS for the image pull; grant the `internet-facing` repository read access to the OpenSourceLab GHCR package after the first image is published.
- The upstream LiteLLM credential never passes through GitHub. Deployment reads `MASTER_LLM_LITE_KEY` from the VPS environment, falling back to `MASTER_LLM_LITE_KEY` or `LITELLM_MASTER_KEY` in the running `litellm-proxy` container. Optional variable `USERS_LITELLM_CONTAINER` changes that container name.
- Variable: `USERS_IMAGE_REPOSITORY`, e.g. `ghcr.io/OWNER/opensourcelab` in lowercase. Optional `USERS_LITELLM_NETWORK` and `USERS_LITELLM_URL` default to `ilovecodex_default` and `http://litellm:4000`.

The LiteLLM network and alias must exist on the deployment host. PostgreSQL gets a separate internal network and persistent named volume; the app joins both networks. The database password is generated once in `/etc/opensourcelab/postgres.password`, and the runtime environment is `/etc/opensourcelab/users.env`; both are root-only. The Data Protection key ring uses `opensourcelab-keys`. Back up these volumes and password file. Never delete or replace the password file while the database exists.

The candidate uses loopback port 5280 or 5281, checks `/health/ready`, then the shared Caddy deployment script validates and reloads the domain fragment. On failure before traffic switches, the existing container, runtime environment and Caddy configuration remain active. Schema migration runs before the candidate; verify database backups and compatibility before rolling back images.

Before deploying, publish the frontend and backend pipelines, grant the image token read access to the private package, and register `https://users.eldervibe.dev/signin-oidc` for client `opensourcelab` in Fences. Confirm DNS and that ports 5280/5281 are unused. The identity service and LiteLLM must already be reachable from this host.
