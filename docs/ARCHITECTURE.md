# Architecture

Status: describes the system as built (Farm.Data `1.0.0-ci.6.1`). Section 7 lists the
points to rethink. Nothing here is a decision yet.

## 1. Purpose

AFFiNE (self-hosted, Docker) is the **source of truth** for the content of the personal
website. A worker mirrors it into the PostgreSQL database `aurafarming`, whose schema is
owned by the `Farm.Data` EF Core package (repo `internet-facing`). The website (`Farm`)
only reads that database.

```
 AFFiNE UI (people edit pages)
        |
        v
 affine_postgres  (Yjs blobs in `snapshots`)         <- read-only role affine_sync
        |   pg + yjs
        v
 reader/export.mjs  (Node)  -- one JSON snapshot on stdout -->  AffineSync worker (.NET)
                                                                      |  Farm.Data (EF Core)
                                                                      v
                                                          postgres `aurafarming`
                                                                      |
                                                                      v
                                                         Farm website  /api/*
```

## 2. Components

| Component | Repo | Responsibility |
| --- | --- | --- |
| AFFiNE (`affine_server`, `affine_postgres`, `affine_redis`) | external | Editing UI and storage. Documents are Yjs binary blobs in `snapshots` (`updates` is empty). |
| `reader/` (Node 22) | this | Reads blobs read-only in one REPEATABLE READ transaction, decodes Yjs, maps pages to rows, prints one JSON snapshot. |
| `Farm.Sync` (.NET 10) | sub/note (sidecar of production AFFiNE) | Runs the reader, validates the snapshot, decides what changed, upserts via EF. |
| `Farm.Data` (ProjectReference) | this repo (main, rebased in) | The only owner of schema, migrations and entity limits. |
| `Farm` (ASP.NET) | internet-facing | Serves `/api/metadata`, `/api/feeds`, `/api/portfolio/*` from the database. |
| `scripts/affine-collections.sh` | this | Backup/restore of the workspace documents (SQL INSERTs). |
| `reader/seed-structure.mjs` | this | Idempotent script that creates the page structure in AFFiNE (dry run by default). |

Two EF contexts live in one database and migrate independently:
`AuraFarming` (history `__EFMigrationsHistory`) and `PortfolioDbContext`
(history `__PortfolioMigrationsHistory`).

## 3. Data flow of one sync iteration

1. `SyncRunner` loads the checkpoint (`/state/checkpoint.json`).
2. The reader returns a `Snapshot`: one `SourceDocument` per AFFiNE page involved.
3. `Snapshot.Validate()` enforces types, lengths, enums, unique ids and slugs.
4. A document is "changed" when its `updatedAtUtc`, content hash or serialized SHA-256
   differ from the checkpoint.
5. `DatabaseWriter` writes changed documents: `WebsiteMetadata` and `PlatformBookmarks`
   by integer `Id`, then the portfolio rows through `PortfolioWriter`.
6. The checkpoint is saved only after both writes succeed. Writes are idempotent, so a
   retry replays safely. The two contexts are **not** atomic with each other.

The worker never deletes entity rows. It replaces only join rows (`ProjectSkill`,
`GigProblemSkill`) and `GigStep`s.

## 4. Content model: page = record

Each AFFiNE page in a collection is one record. Properties hold scalars; the page body
holds the prose people actually edit. Pages without a `Kind` property are ignored.

| Kind | Collection | Properties | Body |
| --- | --- | --- | --- |
| `Profile` | KaiPortfolio | DisplayName, Headline, Location, ContactEmail, ... | Bio |
| `Project` | KaiPortfolio | Title, Slug, Summary, DemoUrl, SourceUrl, Role, Skills, ... | Description |
| `Experience` | KaiPortfolio | Company, EmploymentType, WorkMode, StartDate, EndDate, IsCurrent | Description |
| `Gig` | Gigs | Title, Slug, Category, Summary, SolvedOn, Skills | `## Problem`, `## Outcome`, `## Steps` (numbered list; nested paragraphs = step description) |
| `Skills`, `SocialLinks` | KaiPortfolio | none | a database block (rows) |
| Meta | StaingDataSet | DeveloperName, Title, Description, RepositoryUrl, SpecifiedIssueUrl | not used |
| Feeds | StaingDataSet | none | a database block (rows) |

Identity: portfolio rows are keyed by the AFFiNE page id, stored as `SourceDocId`
(unique, nullable). A row without one is adopted by `Slug` the first time. `WebsiteMetadata`
and `PlatformBookmarks` still use integer `Id`s configured in AFFiNE.

## 5. AFFiNE storage facts the design depends on

- Collections live in the root doc, `setting.collections`, as plain JSON objects with an
  `allowList` of page ids.
- Page properties live in `db$<ws>$docProperties` (keyed `custom:<definitionId>`);
  definitions live in `db$<ws>$docCustomPropertyInfo`. Properties are matched **by name**.
- `doc.toJSON()` flattens link-attributed text to `" "`, so the reader reads Y.Text deltas
  directly (page references resolve to the page title).
- Database blocks keep rows as paragraph blocks and cells in `prop:cells`.
- Writing to AFFiNE directly requires stopping `affine_server` (it holds docs in memory).

## 6. Deployment

`compose.yml` profiles: `sync` (worker) and `farm` (website built from `../internet-facing`),
sharing a `postgres` service. The worker joins the external `affine_default` network and
connects as the read-only role `affine_sync` (local trust auth; not for remote use).
All configuration is environment variables (see README). The package is restored from
GitHub Packages with a `read:packages` token passed as a BuildKit secret.

## 7. Points to rethink

1. **Coupling to AFFiNE internals.** Reading Yjs blobs straight from AFFiNE's database ties
   the reader to private storage formats that can change between AFFiNE releases (this
   already broke once when titles became page links). Alternatives: AFFiNE API/GraphQL
   export, or a thin export step that AFFiNE-side automation controls.
2. **Two languages in one worker.** .NET orchestrates a Node child process over stdout JSON.
   Option: one runtime (decode Yjs in .NET via a port, or do the whole worker in Node and
   keep EF only for migrations).
3. **Schema ownership and version skew.** The worker is pinned to a `Farm.Data` CI version
   and must be bumped by hand; the website bundles its own copy. Option: one source of
   migrations run by one component, with a compatibility check at startup.
4. **No delete/rename semantics.** Removing a page in AFFiNE leaves its row in PostgreSQL;
   renaming a property silently stops a field from syncing. Needs a tombstone/soft-delete
   rule or an explicit "orphan" report.
5. **Two non-atomic contexts.** `AuraFarming` and `PortfolioDbContext` commit separately.
   Option: a single context/migration history.
6. **Convention-based mapping.** Property names, `Kind` values, heading names and the
   comma-separated `Skills` list are conventions with no schema file. A declarative
   mapping (page type -> table/columns -> source) would make mismatches checkable before
   a sync (a `--check` mode that validates AFFiNE against the EF model and reports gaps).
7. **Change detection.** Per-document hashes are cheap but whole-snapshot reads happen every
   interval. Option: query only snapshots newer than the checkpoint, or use LISTEN/NOTIFY.
8. **Bidirectional editing.** Today the flow is one-way; edits made directly in PostgreSQL
   are overwritten on the next sync of that page.
9. **Security.** Trust-only local auth, a PAT used for package restore, and a password in
   the compose shell environment are acceptable for a test environment only.
10. **Backups.** `affine-collections.sh` restores the whole workspace, not single pages.

## 8. Open questions

- Should the website own the content model (and AFFiNE adapt), or should AFFiNE stay the
  authority with Farm.Data generated from a mapping file?
- Is direct database access acceptable long term, or is an export API required?
- Is one-way sync enough, or is write-back to AFFiNE needed?
- What should happen to rows whose AFFiNE page was deleted?
