# Farm.Mind

The AI feature of eldervibe.dev: a personal, portable "AI setup" (rules, preferences, decisions, context)
that any AI assistant can load through one **MCP server**, so behaviour stays consistent across models,
workspaces and machines. The same operations are exposed as REST for the Farm UI.

> **Status:** sign-in, MCP tools (full-text search) and REST read access work. Vector search and the
> Farm UI for memories are not built yet.

## Endpoints

| Route | Auth | Purpose | Status |
|---|---|---|---|
| `GET /healthz` | none | Liveness, used by the deploy script. | working |
| `/.well-known/oauth-protected-resource[/mcp]` | none | RFC 9728 metadata: tells MCP clients which authorization server (Fences) to use. | working |
| `POST /mcp` | bearer token only | MCP server, Streamable HTTP (stateless). | working |
| `GET /api/memory/whoami` | session cookie or bearer | Who the token or session belongs to (`sub`, `name`). | working |
| `GET /api/memory?q=&workspace=&limit=`, `/briefing`, `/{id}`; `DELETE /{id}` | session cookie or bearer | REST twin of the read/forget tools, for the Farm UI. | working |

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

Fences supports dynamic client registration, so VS Code and similar clients register themselves. Access tokens
are signed JWTs whose audience is the MCP URL (`PublicBaseUrl` + `/mcp`, RFC 8707 resource).

## Tools

Every tool acts only on the signed-in user's data (the Fences subject, `github:<id>`).

| Tool | Purpose |
|---|---|
| `get_briefing` | Start here: the 10 most recent memories and the linked workspaces. |
| `search(query, workspace?, limit?)` | Full-text search (quoted phrases, `OR`, `-exclude`); an empty query returns recent memories. |
| `get_memory(id)` | One memory. |
| `save_memory(title, content, tags?, workspace?)` | Create a memory (written directly). |
| `update_memory(id, ...)` | Change only the provided fields. |
| `forget_memory(id)` | Permanently delete. |
| `list_workspaces`, `link_workspace(name, affineWorkspaceId?)` | Named workspaces, optionally tied to an AFFiNE workspace. |

## Data model (PostgreSQL)

Stored in Farm's database, in its own `mind` schema with its own `MindDbContext` and migration history
(`mind.__MindMigrationsHistory`); migrations are applied at startup. `Memories` has a generated `tsvector`
column (title, content, tags) with a GIN index; `Workspaces` is unique per subject and name.

Not yet built: vector search. It needs the `pgvector` extension, which the current `postgres:17-alpine`
image does not ship, plus an embedding endpoint; full-text search covers the need until then.

## Configuration

Shared with Farm through `Farm.Auth` (`FarmAuth__*`):

| Setting | Meaning |
|---|---|
| `FarmAuth__Authority` | Fences base URL (`https://identity.eldervibe.dev`). |
| `FarmAuth__ClientId` | Client id registered in Fences. |
| `FarmAuth__PublicBaseUrl` | Public URL of the site. |
| `FarmAuth__DataProtectionKeysPath` | Key ring shared with Farm so the session cookie works in both. |
| `POSTGRES_HOST`, `POSTGRES_PORT`, `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD` | Farm's database (same variables as Farm). |

## Run locally

```bash
cd deployment/local && cp local.env.example .env   # set POSTGRES_PASSWORD
docker compose up -d --build                        # Mind on http://localhost:8081
curl http://localhost:8081/healthz
```

Tests: `dotnet test Farm.Mind.Tests`.
