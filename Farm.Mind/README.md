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
| `list_note_workspaces` | Note workspaces where the MCP identity has active membership. |
| `search_notes(workspaceId, query, limit?, offset?)` | Search live document titles (1-20 hits); empty query lists notes. |
| `read_note(workspaceId, docId, offset?, length?)` | Read Markdown, with `nextOffset` for continuation (default 20,000 characters). |

## Read-only Note portal

Mind reads **AFFiNE's PostgreSQL database directly**, with a dedicated SELECT-only role. It does not call
Note's HTTP/MCP endpoint, need `copilot.enabled`, use an AI index, or require users to create workspace
credentials. Existing memory write tools are unchanged; no Note writes are exposed.

The validated MCP subject maps to `user_connected_accounts` using AFFiNE's exact provider namespace:
SHA-256 of the provider issuer, newline, and **Note's** OAuth client id (first 16 bytes, lowercase hex).
OIDC subjects must match exactly. Optionally, `github:<id>` can match Note's GitHub provider account id
and configured namespace (or its legacy null namespace). No email, display-name or arbitrary subject-to-user
mapping is accepted. Missing, disabled, unregistered or ambiguous accounts fail closed; sign in to Note
with the same provider identity first. A Mind access key uses its owner's subject in the same way.

Each operation starts a repeatable-read, **read-only** transaction and requires active `member`, `admin`
or `owner` membership. Removed or pending members cannot read. Document reads/searches additionally
check explicit user grants and member-default policies; owners inherit read access. Admin inheritance
depends on a commercial license, which this reader does not verify: admins use explicit/default grants
instead, conservatively denying documents available solely through licensed admin inheritance.
External/public-only shares are not enumerated. Unknown role values fail closed. `link_workspace` grants no access.

The workspace root's current `meta.pages` supplies ids and titles, including pages missing from
`workspace_pages`. Trashed, blocked and unregistered documents are excluded. **Search is title-only**;
empty query lists readable notes, with `nextOffset` for pagination. `read_note` merges the stored Yjs snapshot
and all pending updates in memory, without compacting or modifying AFFiNE. A bounded Node/Yjs decoder renders
Page text, headings, lists, code and dividers as Markdown. Canvas elements, databases, attachments and unknown
blocks are marked in `unsupportedBlocks`, not silently treated as a complete document export.

Limits: 16 MiB stored data and 10,000 updates per document/root, 10,000 root page entries, 1,000,000 rendered
characters, 15-second decoding/query timeouts, and 100,000 characters per read response. Check `nextOffset`
and `unsupportedBlocks` when consuming results. Corrupt/incomplete Yjs data fails closed.

Database contracts were verified against pinned AFFiNE revision
`868acf8505eb349223e367ef75070d24eb04f7ad` and its `affine_core` 0.0.4 read policy.
Schema changes require rechecking identity and permission semantics before enabling the reader on a new release.

### Server setup

Run `Notes/provision-reader.sql` as the AFFiNE database administrator to create `affine_mind_reader`.
It grants only the required columns, excluding passwords, session tokens and OAuth credentials. Set:

| Setting | Meaning |
|---|---|
| `AffineNotes__ConnectionString` | Npgsql connection string to the **AFFiNE** database, using `affine_mind_reader`. Keep the password in server secret configuration, not git. |
| `AffineNotes__OidcClientId` | Note's client id at the same Fences issuer configured by `FarmAuth__Authority`. |
| `AffineNotes__GitHubClientId` | Optional Note GitHub OAuth client id, if Note uses GitHub directly instead of Fences OIDC. |

At least one Note provider client id is required. Do not use Farm's OAuth client id in its place.
For VPS deployments, put these settings in root-owned `/etc/farm/note-reader.env` with mode `0600`;
the Mind deploy wrapper loads that separate file without overwriting it. Ensure Note's PostgreSQL
container is reachable from `farm-network` (for example, attach it to that private Docker network).
Do not expose the AFFiNE database publicly. Local Compose accepts `AFFINE_NOTE_CONNECTION_STRING`,
`AFFINE_NOTE_OIDC_CLIENT_ID` and `AFFINE_NOTE_GITHUB_CLIENT_ID`.

The Docker image includes Node and the pinned Yjs dependency. For local `dotnet run`, install Node,
then run `npm --prefix Farm.Mind/Notes/reader ci`. Missing database settings produce a safe tool error;
memory tools and health checks still work.

The previous `/api/memory/notes/connections` credential-management API is removed. A forward Mind migration
drops its obsolete encrypted-credential table, preserving migration history. Previously issued Note
credentials are no longer used and can be revoked in Note.

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

Tests: `dotnet test Farm.Mind.Tests` and `npm --prefix Farm.Mind/Notes/reader test`.
For real database integration tests, set `MIND_NOTE_TEST_CONNECTION` to an isolated PostgreSQL administrator
connection; tests create/drop their own database and restricted reader role. Without it, those tests are
explicitly skipped. CI provisions the test PostgreSQL service and installs the decoder dependency.
