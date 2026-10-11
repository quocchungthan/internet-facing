# Discussion notes

Senior developer notes from the architecture discussion. Updated after every decision.
Background: [ARCHITECTURE.md](ARCHITECTURE.md).

## Where we are (current state)

- AFFiNE is the source of truth; a Node reader + .NET worker mirror it into PostgreSQL
  through `Farm.Data` (pinned `1.0.0-ci.6.1`); the Farm website reads that database.
- Everything runs in Docker (`postgres`, `sync`, `farm`); verified end to end on 2026-10-10.
- Content model is page = record (properties = scalars, body = prose).

## Decisions

1. **Reading AFFiNE (2026-10-10):** keep direct database reads. Accepted risk: AFFiNE's
   private storage format can change on upgrade. Mitigation: a `--check` mode that validates
   AFFiNE against the EF model, so breakage is found before a sync, not during one.
2. **Model ownership (2026-10-10):** `Farm.Data` stays the single authority for the schema.
   AFFiNE conventions and the reader mapping must conform to it. `--check` compares both
   against the EF model (reflection over entities, limits, required flags, enums). No
   separate mapping file for now; revisit if the three-places problem keeps costing us.
3. **Deletes (2026-10-10):** soft delete. Farm.Data gets a `DeletedAtUtc` (nullable) column on
   the page-backed tables (profile, project, experience, gig; social links and skills by
   their own rows). The worker sets it when a page is trashed/removed from its collection
   and clears it if the page returns. The Farm API filters deleted rows out.
   Needs a Farm.Data migration, a new package version and a Farm query change.
4. **Atomicity (2026-10-10):** accept two separate commits. The checkpoint is saved only after
   both succeed and every write is idempotent, so a retry heals a half-applied batch.
   Revisit if a reader ever shows inconsistent data in practice.
5. **MCP storage (2026-10-10):** documents AI sessions store go through a dedicated API and
   persist in a document database (MongoDB), not in AuraFarming (Postgres) or AFFiNE. Keeps
   user-written data separate from the one-way AFFiNE-synced showcase data. Owner key is the
   Fences OIDC subject (`github:<id>`). Still open: hosting/transport of the API and MCP.
6. **Styling (2026-10-10):** the React apps use plain CSS (no SCSS), so styles are easy to
   inject across micro-frontends. Shared design tokens as CSS custom properties; each tab
   theme overrides them, scoped per tab root.
7. **Sign-in and MCP auth (2026-10-10, revised):** the Farm UI has sign in / sign out via
   Fences (OIDC redirect). MCP auth is browser-based, per the MCP OAuth 2.1 flow: the MCP
   endpoint answers 401 with protected-resource metadata, the client discovers Fences as the
   authorization server, opens the browser, the user signs in and consents, and the client
   gets a PKCE token. The MCP server only validates tokens (resource server).
   Needs in Fences: authorization-server metadata, client registration (dynamic registration
   or client-ID metadata documents, or pre-registered clients as a first step), an MCP
   audience/scope. Revocation: the Farm UI lists granted authorizations and revokes them.
   Personal access tokens (shown once, hashed) stay only as an optional fallback for
   headless clients.
9. **MCP hosting (2026-10-10):** the MCP server and its document API live inside `Farm`
   (main domain), not a separate `sub/*` service. Farm becomes an OAuth resource server
   (validates Fences tokens) and gains a MongoDB dependency. Consequence: Farm is no longer
   read-only/unauthenticated; public portfolio endpoints stay anonymous, MCP/document
   endpoints require a token. Keep them in separate route groups and namespaces.
10. **Sync worker moves to sub/note (2026-10-11, proposed by user):** production AFFiNE is
   `note.eldervibe.dev` (sub/note). The sync worker + reader move into `sub/note` as a sidecar of
   the AFFiNE compose project, referencing `Farm.Data` by ProjectReference (no NuGet pin). The
   auto rebase (`rebase-sub-branches.yml`, after green main builds) keeps `Farm.Data` current, and
   `--check` runs in sub/note CI. Supersedes decision 2's package pinning for the worker.
   Status: implemented in sub/note (Farm.Sync, sidecar compose, manual Deploy Note Sync workflow); VPS deploy untested. Open: fate of `affine-github-repo`.
8. **Future AFFiNE <-> Mongo pipe (2026-10-10):** MongoDB is the store for now, but later the
   user must be able to view and edit their data directly in AFFiNE. That means a pipe from
   Mongo to AFFiNE, and eventually two-way sync (conflict rules, ownership, per-user
   workspace, deletes). Not now; keep the document model sync-friendly (stable ids,
   updatedAt, soft delete, markdown body).
   **Design agreed 2026-10-11 (now wanted, so users can edit AI learning in a visual editor):**
   - Mongo is the source of truth for AI memory; AFFiNE is the visual editor. One AFFiNE page per item
     (structured fields as properties, body limited to a lossy-safe markdown subset).
   - Each item has `rev`, a synced hash and a tombstone (`DeletedAtUtc`); no hard deletes.
   - Mongo -> AFFiNE on MCP write; AFFiNE -> Mongo via the reader. The synced hash prevents echo loops.
   - Conflict rule (user): **latest write wins** (compare timestamps; keep the overwritten version in an
     audit/history record so nothing is silently lost).
   - Write path into AFFiNE (user): **AFFiNE API with a service token**. Never write its Yjs docs in
     Postgres directly. Spike first to confirm the API can create/update pages and properties.
   - Open: clock source for "latest" (server time, not client), per-user workspace mapping.

## Farm UI findings (user review of http://localhost:8080, 2026-10-10)

**Favicon.** Root cause: `Farm/Views/Home/Index.cshtml` sets `Layout = null` and hand-writes its
`<head>` without `<link rel="icon">`/`apple-touch-icon`. `Farm/Views/Shared/_Layout.cshtml` and
`Farm.HangingPost/index.html` both have them, but the page actually served is the Razor view,
so the browser only falls back to `/favicon.ico` (which is served, 200). The `sub/*` branches
carry the same `favicon.ico` as main (identical hash), so there is nothing different to adopt;
`sub/identity` also has a `Fences/wwwroot/favicon.svg` (green gradient) that is a candidate if
we want per-service icons later.
Plan: add the icon and apple-touch-icon links (plus `theme-color`) to the Home view head; keep
`favicon.ico` as fallback; verify in the container with curl and a hard refresh.

**Page padding.** The micro-frontend shell (`Farm.HangingPost`, with `Farm.Microsoft` injected as a
tab) has oversized padding across screen widths. Plan: use the installed UI UX Pro Max skill to
audit and realign the layout (container max-width, fluid spacing scale, breakpoints) in a new
session where the skill is loaded; check at ~360, 768, 1024, 1440 and 1920 px.

## Product vision (user, 2026-10-10) - eldervibe.dev

"Elder vibe coder": decades in the industry, so the age shows more than the seniority (a joke).
The domain is a business brand, not just a CV.

**Goals.** (1) Introduce the person and link the platforms they use. (2) Show experience,
showcases and gigs (problems solved and the path taken). (3) Serve users a way to attach to an
AI assistant such as Copilot: eldervibe.dev is a plugin to an existing AI platform.

**Frontend: four React projects per one backend (main branch).**

| App | Role | Theme / concept |
| --- | --- | --- |
| `Farm.HangingPost` | Shell: the navbar. Changes the content below by active tab; the navbar itself changes completely with the active tab. | per active tab |
| Tab 1 (`Farm.Microsoft`) | Renders one React app | Microsoft-docs style |
| Tab 2 (`BrickTracker` today) | Renders one React app | Brick-breaker game mixed with a habit tracker |
| Tab 3 (future) | Gigs showcase | to be decided; may include the 3D Winchester brothers' Impala '67 (`WinchesterView` today) |

Rules: one app per tab; each app shares its style with its own tab item; a 3D library visualises
the car. Current code: `Farm.HangingPost/src/{App,BrickTracker,WinchesterView}.tsx`,
`themes/{brick,microsoft,winchester}`, `Farm.Microsoft/src/MicrosoftTab.tsx`.

**Backend.** The `Farm` ASP.NET app on main serves the endpoints; its data comes from PostgreSQL,
which the sync fills from AFFiNE.

**AI integration.**
- Auth must reuse the existing identity service (`sub/identity`, "Fences"): GitHub OAuth plus an
  OIDC provider (OpenIddict, issuer `https://identity.eldervibe.dev`, auth-code flow with PKCE,
  per-client config via `IdentityApp:OidcClients`, shared `.eldervibe.dev` cookie). Wire it, do
  not reimplement it.
- Serve an **MCP server** so an AI can store and load documents on eldervibe over time, so users
  do not retype their working context.

## Two areas of the main domain (2026-10-10)

1. **Introduction/showcase** (profile, platforms, experience, showcases, gigs): read-only, public, fed from AFFiNE. Understood; user will add detail if needed.
2. **Vibe-coder functional area** (personal AI context via MCP, sign-in, tokens, MongoDB): needs deep digging. To be explored with the user; questions below.

**Purpose of area 2 (user, 2026-10-10):** the MCP server exists so a user gets a *consistent AI setup, behaviour and decision-making* across AI models (Copilot, Claude, etc.) and across workspaces/machines. It is a portable, personal "AI memory + operating preferences" layer, not just a document store. Implication: content is likely instructions, preferences, conventions and decisions (things an AI should obey/recall), so the model needs precedence/scoping (global vs. workspace), and must be model-agnostic and machine-independent (identity = account, not device). User will answer the questions below later.

Questions to dig into area 2 (not asked yet, user leads):
- What is a "document" an AI stores (project brief, decisions, prompts, session summary, snippets)? Free markdown or typed kinds?
- Who reads them: only the AI via MCP, or also the user in a Farm UI (browse/edit/delete)?
- MCP surface: which tools/resources (save, load, search, list, tag, delete)? Search: keyword, tags, semantic?
- Scoping: per user only, or per project/workspace within a user? Sharing/team later?
- Limits: size, count, retention, rate limits, privacy/PII handling, export/delete-my-data.

## Draft: AI memory I/O and model (area 2) - DRAFT, not decided

Principle (user): ONE MCP for every session; a session only receives what it needs, so retrieval and indexing are first-class. Context windows are small and costly, so the server returns a compact briefing, not everything.

**Session handshake.** The client harness sends a context fingerprint: `client` (copilot-cli, claude-code...), `model`, `machineId` (opaque, client-generated), `workspace` hints (repo remote URL, folder name, languages/frameworks detected), optional `task` text. The server resolves it to a workspace (or none) for the token's owner.

**Memory item** (Mongo `memories`): `_id`, `ownerSub`, `scope` (global | workspace), `workspaceId?`, `kind` (rule | preference | decision | context | snippet), `title`, `summary` (<= ~200 chars, this is what the index shows), `body` (markdown), `tags[]`, `appliesTo` { clients[], models[], languages[], pathGlobs[] } (all optional = applies everywhere), `alwaysOn` (bool, injected in every briefing), `priority` (int), `tokenEstimate`, `version`, `source` { client, model, machineId }, `createdAt`, `updatedAt`, `deletedAt?`, `affineDocId?` (future editing link).
Other collections: `workspaces` { ownerSub, name, matchers (repo URLs, name patterns), machineIds[] }, `grants` (OAuth authorizations, revocable).

**Retrieval in three tiers (keeps sessions small):**
1. *Briefing* (`get_briefing`): alwaysOn global rules + alwaysOn workspace rules matching the fingerprint, filtered by appliesTo, sorted by priority, hard token budget. Workspace overrides global on same `key`/kind conflict (rule precedence: workspace > global; explicit `supersedes`).
2. *Index*: the rest as title + summary + id only, so the AI knows what exists and fetches on demand.
3. *On demand*: `search`, `get`.

**MCP tools (small, model-agnostic):** `get_briefing(context)`, `search(query, scope?, kind?, tags?)`, `get_memory(id)`, `save_memory(...)` (AI proposes; item records source), `update_memory(id, patch, expectedVersion)`, `forget_memory(id)` (soft delete), `list_workspaces`, `link_workspace(context)`.
Same operations as REST under `/api/memory/*` for the Farm UI, behind the same token validation.

**Indexing:** compound `{ownerSub, deletedAt, scope, workspaceId, alwaysOn, priority}` for the briefing; `{ownerSub, tags}`; text index on title/summary/body for keyword search; `workspaces` matcher lookup by `{ownerSub, matchers.repoUrl}`. Embeddings/semantic search: later option (Atlas Vector Search or a sidecar), not v1; keep `summary` good because it is the index entry the AI reads.

**Open (draft):** is `save_memory` write-direct or a proposal the user approves? Who curates alwaysOn (user only?). Conflict resolution when two machines edit. Is a token budget per client configurable? Do Note/Shed already hold documents (could replace Mongo)?

## Observations (senior dev)

- Farm is read-only and unauthenticated today (`/api/*` only reads). The MCP needs authenticated
  writes and per-user data, which is a different shape from the AFFiNE-owned showcase content.
- Fences already exposes OIDC discovery, `/connect/authorize` and `/connect/token`; Farm would be
  a new OIDC client/resource (needs an `OidcClients` entry and JWT validation in Farm).
- Tension to resolve: AFFiNE is the one-way source of truth for showcase content, but MCP
  "inject/store documents" is user-written data. They probably need separate tables (decision 5 below, pending).
- Four apps, one shell: needs a shared design-token layer plus a per-tab theme contract and a
  navbar-per-tab contract, otherwise each tab re-solves padding/spacing (see Farm UI findings).

## Backlog

### Design
- [ ] Shell/tab contract: per-tab theme + navbar, shared design tokens (HangingPost <-> tab apps).
- [ ] MCP server design: tools (store/load documents), auth via Fences OIDC, storage model, per-user scoping.
- [ ] Gigs tab design and the 3D Impala '67 scene.
- [x] Decide how AFFiNE is read long term -> direct DB reads (decision 1).
- [x] Decide who owns the content model -> Farm.Data (decision 2).
- [x] Delete semantics -> soft delete (decision 3). Rename semantics still open: `--check` reports unmapped/missing properties.

### Development
- [ ] Wire Farm to Fences OIDC (client registration + token validation), no reimplementation.
- [ ] Gigs API endpoints in Farm (the data already syncs from AFFiNE into `GigProblems`/`GigSteps`).
- [ ] Farm.Data: `DeletedAtUtc` on page-backed tables + migration + test; publish; bump pin.
- [ ] Worker: mark rows deleted when their page is missing/trashed; restore if it returns.
- [ ] Farm: filter deleted rows in `/api/portfolio/*` (and gigs endpoints when added).
- [ ] **Next:** `--check` mode (needed by decision 1): validate AFFiNE pages against the EF model and report mismatches.
- [ ] ~~Declarative mapping file~~ deferred by decision 2 (revisit if drift hurts).

### Improvement
- [x] Two contexts -> accepted as is (decision 4); only documentation left.
- [ ] Change detection that does not re-read every document on each interval.
- [ ] Startup compatibility check between worker and `Farm.Data` schema version.

### Chore
- [ ] Farm: add favicon/apple-touch-icon links to `Home/Index.cshtml` head (root cause above).
- [ ] Farm UI: fix oversized padding across resolutions with the UI UX Pro Max skill (needs a restarted session so the skill is loaded). Tracks in internet-facing.
- [ ] Document the non-atomic two-context behaviour (decision 4) in ARCHITECTURE.md/README.
- [ ] Remove the hand-bumped `Farm.Data` pin (feed/automation).
- [ ] Per-page backup/restore (current script restores the whole workspace).
- [ ] Replace local trust auth and shell-exported secrets before any non-test use.

## Open questions

- (Answered, see decisions 5-6: MCP storage is MongoDB keyed by OIDC subject; styling is plain CSS.)
- MCP transport: Streamable HTTP (remote, needed for browser OAuth) - assumed; hosting decided (inside Farm).
- Tab 3 (gigs) and 3D Impala: same app or two? Which 3D library (three.js / react-three-fiber)?
- How is "navbar completely changes per tab" modelled: each app exports its own nav, or the shell renders a nav from a per-tab config?

- How to handle `Farm.Data` versioning (manual bump vs. startup schema check vs. floating range)? Not discussed yet; user will steer.
- Does one-way sync stay enough, or is write-back to AFFiNE needed?
- What should happen to rows whose AFFiNE page was deleted?

## Log

- 2026-10-10: Started the discussion; backlog seeded from the "points to rethink" in ARCHITECTURE.md.
- 2026-10-10: Decision 1 - keep direct DB reads, add `--check`.
- 2026-10-10: Decision 2 - Farm.Data is the authority; `--check` compares against it.
- 2026-10-10: Decision 3 - soft delete via DeletedAtUtc.
- 2026-10-10: Decision 4 - accept non-atomic contexts, document it.
- 2026-10-10: Working style agreed: user leads the discussion, senior dev takes notes and asks only when unclear.
- 2026-10-10: User review: favicon missing and page padding too large; root cause of favicon found, padding plan recorded.
- 2026-10-10: User described the product vision (four React apps, tabbed shell, themes, Gigs tab with 3D Impala, Copilot plugin with Fences auth and an MCP for persistent context); recorded above.
- 2026-10-10: Decision 5 - MCP documents in MongoDB behind a dedicated API. Decision 6 - plain CSS with custom-property tokens.
- 2026-10-10: Decision 7 - sign in/out in Farm UI via Fences, user-managed MCP tokens. Decision 8 - future Mongo -> AFFiNE pipe.
- 2026-10-10: Decision 7 revised - browser-based OAuth for MCP; PATs optional fallback. Decision 8 agreed.
- 2026-10-10: Decision 9 - MCP hosted inside Farm.
- 2026-10-10: Split the main domain into two areas; area 2 (vibe-coder functional) needs deep digging, questions listed.
- 2026-10-10: Purpose of area 2 clarified: consistent AI setup/behaviour/decisions across models and machines. Detail questions deferred by user.
- 2026-10-11: Drafted AI memory I/O model: handshake fingerprint, memory item, 3-tier retrieval (briefing/index/on-demand), MCP tools, indexes. Marked DRAFT.
- 2026-10-11: Split into Farm (portfolio, GET) and Farm.Mind (AI, read/write) sharing Farm.Auth; Farm UI got sign in/out, /connect page, favicon, padding fix. See docs/SYSTEM-OVERVIEW.md.
- 2026-10-11: Correction: production AFFiNE = note.eldervibe.dev (sub/note); local AFFiNE is dev only. Proposal under discussion: run the sync as a sidecar of the note AFFiNE deployment (see chat summary below).
- 2026-10-11: Decision 10 - sync worker moves into sub/note as a sidecar with ProjectReference to Farm.Data.
- 2026-10-11: Decision 8 expanded: two-way Mongo <-> AFFiNE sync; latest write wins; writes via AFFiNE API service token (spike first).
