# eldervibe.dev - system overview

How the pieces around eldervibe.dev connect. Status legend: **built** = running today, **partial** = skeleton or
wiring exists, **planned** = decided in `DISCUSSION-NOTES.md`, not started.

## 1. Big picture

```mermaid
flowchart LR
    subgraph Users
        V[Visitor browser]
        O[Owner / signed-in user]
        AI[AI client<br/>Copilot, Claude, ...]
    end

    subgraph Edge
        CADDY[Caddy ingress<br/>TLS + routing]
    end

    subgraph Main["eldervibe.dev (main branch)"]
        FARM["Farm<br/>portfolio API, GET only<br/>+ serves React UI<br/>(built)"]
        MIND["Farm.Mind<br/>AI memory API + MCP<br/>(partial: auth skeleton)"]
        AUTHLIB[["Farm.Auth<br/>shared auth library<br/>(built)"]]
        UI["React UI: HangingPost shell<br/>+ Microsoft / Brick / Winchester tabs<br/>(built)"]
    end

    subgraph Identity["identity.eldervibe.dev (sub/identity)"]
        FENCES["Fences<br/>GitHub login + OIDC provider<br/>(built)"]
    end

    subgraph Data
        PG[("PostgreSQL aurafarming<br/>showcase data")]
        MONGO[("MongoDB<br/>AI memory<br/>(planned)")]
    end

    subgraph Content["Content pipeline"]
        AFFINE["AFFiNE self-host<br/>source of truth<br/>production: note.eldervibe.dev (sub/note)<br/>development: local Docker"]
        SYNC["Farm.Sync (sub/note)<br/>reader + .NET sync worker"]
    end

    OTHER["note / storage / mail<br/>sub/* services"]

    V --> CADDY
    O --> CADDY
    AI -->|"/mcp, OAuth"| CADDY
    CADDY -->|"/ and /api/*"| FARM
    CADDY -->|"/mcp, /api/memory/*,<br/>/.well-known/oauth-protected-resource"| MIND
    CADDY --> FENCES
    CADDY --> OTHER

    FARM --- UI
    FARM -. uses .-> AUTHLIB
    MIND -. uses .-> AUTHLIB

    FARM --> PG
    MIND --> MONGO
    AFFINE --> SYNC --> PG

    FARM -- "OIDC code + PKCE" --> FENCES
    MIND -- "validate bearer token<br/>(JWKS)" --> FENCES
    OTHER -- "OIDC / shared SSO cookie" --> FENCES
```

## 2. Two projects, one auth flow

| Project | Role | Access | Data | Status |
|---|---|---|---|---|
| `Farm` | Introduction and showcase: profile, projects, skills, experiences, feeds, social links. Hosts the React UI. | Public, read-only (`GET`). Only `/auth/*` and `/api/me` involve sign-in. | PostgreSQL (`Farm.Data`) | built |
| `Farm.Mind` | The vibe-coder area: personal AI memory, MCP server, REST for the UI. | Authenticated, read/write. | MongoDB | skeleton |
| `Farm.Auth` | Shared library: session cookie, OIDC sign-in, bearer validation, resource metadata. | n/a | n/a | built |

Both services share the same session cookie name and Data Protection key ring (a shared volume), so a browser
session created through `Farm` is also accepted by `Farm.Mind`. MCP clients send a bearer token instead.

## 3. Content pipeline (AFFiNE to PostgreSQL)

Environments: **production AFFiNE is `note.eldervibe.dev`**, deployed from the `sub/note` branch (AFFiNE submodule
`Streams/DaHang`, compose from `.docker/selfhost`). The AFFiNE container stack run locally (`affine-local/affine`) is
the **development** instance only.

```mermaid
sequenceDiagram
    autonumber
    participant Me as Owner (edits in AFFiNE)
    participant A as AFFiNE (Postgres + Yjs blobs)
    participant R as Node reader
    participant W as .NET sync worker
    participant P as PostgreSQL aurafarming
    participant F as Farm API

    Me->>A: Edit page (properties = scalars, body = prose)
    loop every 30 s
        W->>R: run reader
        R->>A: read-only SQL (role affine_sync)
        R-->>W: snapshot JSON (profiles, projects, experiences, gigs, ...)
        W->>W: validate against Farm.Data model
        W->>P: upsert by SourceDocId (EF Core, 2 contexts)
    end
    F->>P: query
    F-->>Me: /api/portfolio/*, /api/feeds, /api/metadata
```

Rules: one AFFiNE page = one record, a `Kind` property selects the entity, the page body fills the description
(Bio, Description, Gig `Problem` / `Outcome` / `Steps`). `Farm.Data` owns the schema. Deletes will be soft
(`DeletedAtUtc`, planned).

## 4. Browser sign-in (Farm UI)

```mermaid
sequenceDiagram
    autonumber
    participant B as Browser (React shell)
    participant F as Farm
    participant ID as Fences (identity)
    participant GH as GitHub

    B->>F: GET /api/me
    F-->>B: isAuthenticated=false, signInAvailable=true
    B->>F: GET /auth/login?returnUrl=/tracker
    F-->>B: 302 to Fences /connect/authorize (code + PKCE)
    B->>ID: authorize
    ID->>GH: GitHub OAuth (if no SSO cookie)
    GH-->>ID: user
    ID-->>B: 302 to https://eldervibe.dev/signin-oidc?code=...
    B->>F: /signin-oidc
    F->>ID: POST /connect/token (code + verifier)
    ID-->>F: id_token (sub = github:id, name, picture, email)
    F-->>B: Set-Cookie eldervibe.farm, redirect to /tracker
    B->>F: POST /auth/logout
    F-->>B: clear cookie, redirect to Fences /auth/logout, back to the site
```

## 5. MCP web login (AI client)

The browser login is started by the AI client, not by the website. The website only shows the endpoint
(`/connect` page).

```mermaid
sequenceDiagram
    autonumber
    participant C as AI client (MCP)
    participant M as Farm.Mind /mcp
    participant ID as Fences
    participant U as User browser

    C->>M: POST /mcp (no token)
    M-->>C: 401 WWW-Authenticate: Bearer resource_metadata=".../oauth-protected-resource"
    C->>M: GET /.well-known/oauth-protected-resource
    M-->>C: { authorization_servers: [Fences] }
    C->>ID: discover (/.well-known/openid-configuration)
    C->>U: open browser to /connect/authorize (PKCE)
    U->>ID: sign in with GitHub, consent
    ID-->>C: authorization code (loopback redirect)
    C->>ID: POST /connect/token
    ID-->>C: access token (aud = farm-mind)
    C->>M: POST /mcp + Authorization: Bearer
    M->>M: validate signature, issuer, audience
    M-->>C: MCP session (tools: briefing, search, save, ...)
```

Done: protected resource metadata, 401 challenge with the metadata hint, bearer validation, and `/mcp` as a
placeholder (returns 501 once authenticated). Needed in Fences before this works end to end: see section 8.

## 6. AI memory model (draft)

```mermaid
flowchart TB
    subgraph Session["Each AI session"]
        FP["Handshake: client, model,<br/>machineId, repo / workspace hints, task"]
    end
    FP --> RES["Resolve workspace<br/>for ownerSub"]
    RES --> T1["1. Briefing<br/>alwaysOn rules, filtered by appliesTo,<br/>token budget, workspace over global"]
    RES --> T2["2. Index<br/>title + summary + id of the rest"]
    T2 --> T3["3. On demand<br/>search / get_memory"]
    subgraph Mongo
        MEM[("memories")]
        WS[("workspaces")]
        GR[("grants")]
    end
    T1 --> MEM
    T2 --> MEM
    T3 --> MEM
    RES --> WS
```

Future: a pipe from MongoDB to AFFiNE so the user can view and edit memories there (decision 8), then two-way sync.

## 7. Auth building blocks (`Farm.Auth`)

| Piece | Used by | Purpose |
|---|---|---|
| `AddFarmBrowserAuth` | Farm | Session cookie plus OIDC code + PKCE against Fences. Off when `FarmAuth:Authority` is blank. |
| `AddFarmApiAuth` | Farm.Mind | Cookie or bearer token; 401 instead of redirects; `FarmUser` and `FarmMcp` policies. |
| `MapFarmAuthEndpoints` | Farm | `GET /auth/login`, `POST /auth/logout`, `GET /api/me`. |
| `MapProtectedResourceMetadata` | Farm.Mind | RFC 9728 discovery for MCP clients. |
| `ReturnUrl.Sanitize` | both | Local paths only, so no open redirects. |

Configuration (`FarmAuth` section, env `FarmAuth__*`): `Authority`, `ClientId`, `ClientSecret`, `Audience`
(default `farm-mind`), `PublicBaseUrl`, `CookieName`, `CookieDomain`, `DataProtectionKeysPath`.

## 8. What is still needed

| Area | Item | Where |
|---|---|---|
| Fences | Register Farm as an OIDC client (redirect `https://eldervibe.dev/signin-oidc`) and an MCP public client | `FENCES_OIDC_CLIENTS_JSON` |
| Fences | Add `farm-mind` audience, disable access-token encryption (or add introspection) so `Farm.Mind` can validate | `sub/identity` |
| Fences | Client registration for MCP clients (dynamic registration, or pre-registered clients first), refresh tokens | `sub/identity` |
| Fences | Allow `eldervibe.dev` in `AllowedReturnHosts` for the logout return | `sub/identity` |
| Farm.Mind | MongoDB, memory model, REST, real MCP tools | new |
| Deployment | Dockerfile, CI and Caddy routes for `Farm.Mind`; shared key-ring volume | `deployment/`, workflows |
| Farm.Data | `DeletedAtUtc`, then a package bump | `Farm.Data` |
| UI | Padding polish with the UX plugin, per-tab navbar, Gigs tab and 3D Impala | React apps |
| Pipeline | `--check` mode against the EF model | `Farm.Sync` (sub/note) |

## 9. Repositories and branches

```mermaid
flowchart LR
    subgraph IF["internet-facing"]
        MAIN["main: Farm, Farm.Auth, Farm.Mind,<br/>Farm.Data, Farm.HangingPost, Farm.Microsoft"]
        SUBID["sub/identity: Fences"]
        SUBN["sub/note, sub/storage,<br/>sub/mail, ..."]
    end
    subgraph AG["sub/note: Farm.Sync"]
        RD["reader (Node)"]
        WK["sync worker (.NET)"]
    end
    FD[("Farm.Data<br/>ProjectReference")]
    MAIN -- "package-farm-data workflow" --> FD
    FD -- "pinned version" --> WK
    MAIN -. "sub branches rebase from main" .-> SUBID
    MAIN -.-> SUBN
```

See also: [`ARCHITECTURE.md`](ARCHITECTURE.md) (as-built pipeline) and [`DISCUSSION-NOTES.md`](DISCUSSION-NOTES.md)
(decisions and open questions).
