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
| `get_briefing` | Call first, every session: standing guidance on when to consult and save (also sent as MCP server instructions), the 10 most recent memories and the linked workspaces. |
| `search(query, workspace?, limit?)` | Full-text search (quoted phrases, `OR`, `-exclude`); an empty query returns recent memories. |
| `get_memory(id)` | One memory. |
| `save_memory(title, content, tags?, workspace?)` | Create a memory (written directly). |
| `update_memory(id, ...)` | Change only the provided fields. |
| `forget_memory(id)` | Permanently delete. |
| `list_workspaces`, `link_workspace(name, affineWorkspaceId?)` | Named workspaces, optionally tied to an AFFiNE workspace. |
| `list_note_workspaces` | Note workspaces explicitly connected to this account; never returns credentials. |
| `search_notes(workspaceId, query, limit?)` | Search readable persisted Note documents (1-20 hits). |
| `read_note(workspaceId, docId, offset?, length?)` | Read Markdown, with `nextOffset` for continuation (default 20,000 characters). |

## Read-only Note portal

Mind delegates reads to `https://note.eldervibe.dev/api/workspaces/{workspaceId}/mcp`, using AFFiNE's
native `doc_search` and `read_document`. AFFiNE rechecks the credential, workspace membership and document
permissions on every request. No Note create/update/delete tools or arbitrary upstream calls are exposed.
Existing memory write tools are unchanged. Search excludes attachments; reads are limited to an 8 MiB
upstream response and can be paged by character offset.

**One-time authorization is required:** AFFiNE does not accept Mind's Fences bearer token, and sharing a
sign-in provider does not grant workspace access. While signed in to Note, create a **read-only workspace
MCP credential** in its workspace settings. Connect it to your signed-in Farm account through the browser-only
REST API below. This explicitly delegates that Note account's access to your Mind account, including clients
using your Mind access keys. Do not send credentials to an agent or save them as memories. There is no
connection form in the Farm UI yet.

From the browser developer console on `https://eldervibe.dev` while signed in (the prompt keeps the credential
out of the command text/history):

```javascript
const token = prompt("Read-only Note workspace MCP credential");
if (token) {
  const response = await fetch("/api/memory/notes/connections", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      workspaceId: prompt("AFFiNE workspace id (from its URL)"),
      name: prompt("Display name"),
      token
    })
  });
  console.log(response.status, await response.json());
}
```

`GET /api/memory/notes/connections` lists connection metadata (session or Mind bearer).
`DELETE /api/memory/notes/connections/{workspaceId}` disconnects a workspace (browser session only).
POST and DELETE require the same-origin browser `Origin` header; bearer tokens and Mind access keys cannot
manage connections. Credentials are encrypted using the persistent Farm data-protection key ring and scoped
to both the Fences subject and workspace id. Connecting a memory workspace via `link_workspace` grants no
Note access. Revoke the credential in Note to immediately disable reads; disconnecting Mind removes its stored
credential but does not revoke it in Note. Expired credentials must be replaced by reconnecting.

Requires an AFFiNE release providing the workspace MCP credential API and both native read tools
(contracts verified at the repository's pinned AFFiNE revision `868acf8505eb349223e367ef75070d24eb04f7ad`).
Incompatible Note servers fail closed. No AFFiNE database access, shared service account, or automatic
email/subject matching is used.

AFFiNE gates its workspace MCP endpoint behind `copilot.enabled`, even for read-only tools. If Note returns
`403 ACTION_FORBIDDEN: Copilot is disabled.`, its administrator must enable that setting before connections work.
This change does not modify Note's server configuration or enable any AI providers.

## Access keys

Clients that cannot do the browser sign-in (Copilot CLI, scripts) use a personal access key: create it on `/connect`
(browser session required) and send it as `Authorization: Bearer fmk_...`. Only the SHA-256 is stored; the key is
shown once, is scoped to your account, and can be revoked. `GET/POST /api/memory/keys` and `DELETE /api/memory/keys/{id}`
manage them and accept the session cookie only, so a key cannot create keys. Farm.Auth accepts `fmk_` bearer tokens through
`IAccessKeyValidator`; anything else is validated as a Fences JWT.

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
