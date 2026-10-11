# Farm.Mind

The AI feature of eldervibe.dev: a personal, portable "AI setup" (rules, preferences, decisions, context)
that any AI assistant can load through one **MCP server**, so behaviour stays consistent across models,
workspaces and machines. The same operations are exposed as REST for the Farm UI.

> **Status: skeleton.** Authentication, health and discovery work. The MCP tools and MongoDB storage are not
> implemented: `/mcp` answers `501` after a successful sign-in.

## Endpoints

| Route | Auth | Purpose | Status |
|---|---|---|---|
| `GET /healthz` | none | Liveness, used by the deploy script. | working |
| `/.well-known/oauth-protected-resource[/mcp]` | none | RFC 9728 metadata: tells MCP clients which authorization server (Fences) to use. | working |
| `GET/POST/DELETE /mcp` | bearer token only | MCP server, Streamable HTTP transport. | 501 placeholder |
| `GET /api/memory/whoami` | session cookie or bearer | Who the token or session belongs to (`sub`, `name`). | working |
| `/api/memory/*` (items, search, workspaces) | session cookie or bearer | REST twin of the MCP tools, for the Farm UI. | planned |

## Connecting an AI client

Endpoint: `https://eldervibe.dev/mcp` (local: `http://localhost:8081/mcp`). Transport: Streamable HTTP.
Authentication: browser sign-in (OAuth 2.1, authorization code with PKCE) against Fences, the identity provider.

1. The client calls `/mcp` without a token and gets `401` with a `WWW-Authenticate` header that points to the
   protected resource metadata.
2. It reads the metadata, finds Fences, and opens the browser sign-in (GitHub through Fences).
3. After consent it exchanges the code for a token and retries `/mcp` with `Authorization: Bearer <token>`.

Typical client configuration:

```json
{ "mcpServers": { "eldervibe": { "type": "http", "url": "https://eldervibe.dev/mcp" } } }
```

(Copilot CLI: `/mcp add`, Claude Code: `claude mcp add --transport http eldervibe https://eldervibe.dev/mcp`.)
The `/connect` page of the site shows the same instructions and the clients connected to your account.

**Known gap:** Fences must issue tokens Farm.Mind can validate (audience `farm-mind`, unencrypted access
tokens) and either support dynamic client registration or have the client pre-registered. Until then a real
MCP client cannot complete the flow; see `docs/DEPLOYMENT-PLAN.md` (G6) on `sub/note`.

## Planned tools

Small and model-agnostic; a session only receives what it needs.

| Tool | Purpose |
|---|---|
| `get_briefing(context)` | Always-on rules for the global scope and the matching workspace, within a token budget, plus an index (title, summary, id) of everything else. |
| `search(query, scope?, kind?, tags?)` | Keyword search over memories. |
| `get_memory(id)` | Full body of one memory. |
| `save_memory(...)` | The AI proposes a memory; it records client, model and machine as the source. |
| `update_memory(id, patch, expectedVersion)` | Optimistic-concurrency update. |
| `forget_memory(id)` | Soft delete. |
| `list_workspaces`, `link_workspace(context)` | Map a repo or folder to a workspace. |

`context` is a fingerprint sent by the client: `client`, `model`, `machineId`, workspace hints (repo remote,
folder name, languages) and an optional task.

## Data model (MongoDB, planned)

Memories are keyed by the Fences subject (`github:<id>`). A memory has `scope` (global or workspace), `kind`
(rule, preference, decision, context, snippet), `title`, a short `summary`, markdown `body`, `tags`, optional
`appliesTo` filters, `alwaysOn`, `priority`, `version` and a soft-delete marker. MongoDB is the source of
truth for this data; AFFiNE is only a visual editor, synced both ways (latest write wins). Full design and open
questions: `docs/DISCUSSION-NOTES.md` on `sub/note`.

## Configuration

Shared with Farm through `Farm.Auth` (`FarmAuth__*`):

| Setting | Meaning |
|---|---|
| `FarmAuth__Authority` | Fences base URL (`https://identity.eldervibe.dev`). |
| `FarmAuth__ClientId` | Client id registered in Fences. |
| `FarmAuth__PublicBaseUrl` | Public URL of the site. |
| `FarmAuth__DataProtectionKeysPath` | Key ring shared with Farm so the session cookie works in both. |

## Run locally

```bash
cd deployment/local && cp local.env.example .env   # set POSTGRES_PASSWORD
docker compose up -d --build                        # Mind on http://localhost:8081
curl http://localhost:8081/healthz
```

Tests: `dotnet test Farm.Mind.Tests`.
