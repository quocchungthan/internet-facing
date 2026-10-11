# Farm.Sync: AFFiNE to AuraFarming sync (sidecar of the note AFFiNE)

.NET 10 worker invoking the Node reader in `reader`, then upserting PostgreSQL
`WebsiteMetadata` and `PlatformBookmark` rows by integer `Id`. The database context,
schema, and startup migrations come exclusively from the `Farm.Data` project in this
repository (a `ProjectReference`, not a package), so the worker always compiles against the
schema on this branch. `sub/note` rebases from `main`, which keeps it current without waiting
for a package build. Migrations may seed rows; the worker never deletes rows.

It also syncs the portfolio schema (`PortfolioDbContext`, history table
`__PortfolioMigrationsHistory`) from AFFiNE pages, see
[Portfolio and Gigs pages](#portfolio-and-gigs-pages).

## Configuration

Provide your own database password and reader configuration. `.env` and `sync.env`
must never be committed. The worker itself reads process environment variables, not
env files. Production values live in `sync.env` on the VPS
(template: [`deploy/sync.env.example`](deploy/sync.env.example)).

| Variable | Default / purpose |
| --- | --- |
| `DATABASE_CONNECTION_STRING` | Required Npgsql connection string |
| `SYNC_INTERVAL_SECONDS` | `60`; delay after an iteration completes |
| `SYNC_STATE_PATH` | `/state/checkpoint.json`; persist independently of the container |
| `AFFINE_READER_PATH` | `reader/export.mjs`; resolved from the working directory |
| `AFFINE_DATABASE_URL` | Required; `postgresql://affine_sync@affine_postgres:5432/affine` on the local trusted network |
| `AFFINE_DOCKER_NETWORK` | Existing external AFFiNE network; default `affine_default` |
| `AFFINE_WORKSPACE_ID` | Reader workspace; `da7da136-e099-4224-b64d-4fccd115eb89` |
| `AFFINE_COLLECTION_ID` | Reader collection; `LvAkRBBiuqEjMLy4QhWqE`; selected pages must be members |
| `AFFINE_META_DOC_ID` | Metadata document; `H5y_LGDY5d0uP9TLHN2BS` |
| `AFFINE_FEEDS_DOC_ID` | Feeds document; `e0cfaCrD_RsruYoDSwFgE` |
| `AFFINE_FEEDS_BLOCK_ID` | Feeds block; `FPh-Fksvis` |
| `AFFINE_PORTFOLIO_COLLECTION_ID` | Optional; collection holding Profile/Project/Experience/Skills/SocialLinks pages (`e9yeGpEb0hV3KCsdd-wWM`) |
| `AFFINE_GIGS_COLLECTION_ID` | Optional; collection holding Gig pages (`4XdLJej6V8iFFZ6BuPAQk`) |
| `AFFINE_META_ENTITY_ID` | `1`; source Meta fields have no integer entity ID |
| `AFFINE_DEFAULT_IS_ACTIVE` | `true`; source Feeds lacks `IsActive` |

The reader inherits the worker environment and reads the local AFFiNE PostgreSQL
database directly with `pg` and `yjs`; API cookies and Docker CLI are not required.
The configuration table above covers the reader. Reader stdout must contain
only one JSON snapshot; diagnostics go to stderr.
The source `affine_sync` role must have LOGIN and SELECT privileges on AFFiNE
`snapshots` and `updates`; it should have no write privileges. The reader uses a
read-only source transaction. The passwordless URL above assumes the existing
local AFFiNE `trust` authentication policy; do not expose that database publicly.
Provision the local read-only role using the reader-owned script:

```powershell
Get-Content .\reader\provision-source-role.sql |
    docker exec -i affine_postgres psql -U affine -d affine -v ON_ERROR_STOP=1
```

The local source database uses trust-only authentication without a password.
This is unsuitable for remote deployments. In production, have the DBA grant a
dedicated read-only login with a password and require TLS with certificate
verification in `AFFINE_DATABASE_URL`.

## Portfolio and Gigs pages

AFFiNE is the source of truth. One page is one record; pages without a `Kind`
property are ignored. Property values hold scalars and the **page body holds the
prose people edit**:

| `Kind` | Properties | Body |
| --- | --- | --- |
| `Profile` | DisplayName, Headline, Location, ContactEmail, ... | Bio |
| `Project` | Title, Slug, Summary, DemoUrl, SourceUrl, Role, Skills (comma list), ... | Description |
| `Experience` | Company, JobTitle, EmploymentType, WorkMode, StartDate, ... | Description |
| `Gig` | Title, Slug, Category, Summary, SolvedOn, Skills | `## Problem`, `## Outcome`, `## Steps` (numbered list; nested paragraphs are step descriptions) |
| `Skills` / `SocialLinks` | none | a database block (rows = skills / links) |

Rows are upserted by AFFiNE page id (`SourceDocId`); an existing row with no
`SourceDocId` is adopted by `Slug`. Join rows and gig steps mirror the page; entity
rows are never deleted.

```bash
node reader/seed-structure.mjs           # dry run; --apply creates the sample pages (stop affine_server first)
scripts/affine-collections.sh backup FILE.sql   # back up AFFiNE collection documents
scripts/affine-collections.sh restore FILE.sql  # restore them (see script header)
```

See [docs/ARCHITECTURE.md](../docs/ARCHITECTURE.md) for the components, data flow, content
model and the open design questions.

## Where it runs

| Environment | AFFiNE | Worker | How |
| --- | --- | --- | --- |
| Production | `note.eldervibe.dev` (this branch, `Streams/DaHang`) | sidecar `affine-sync` in the AFFiNE compose project | [`deploy/compose.sync.yml`](deploy/compose.sync.yml), workflow **Deploy Note Sync** |
| Development | local AFFiNE stack (`affine_default`) + main's Farm database (`farm-local_default`) | `sync` service | [`compose.dev.yml`](compose.dev.yml) |

### Production sidecar

The worker joins the AFFiNE compose project, so it reaches AFFiNE's database by the service name
`postgres`, and starts and restarts with the stack. The Farm database is published on the host and is
reached through `host.docker.internal`.

1. On the VPS, in `AFFINE_DEPLOY_DIR`, create `sync.env` from [`deploy/sync.env.example`](deploy/sync.env.example).
2. Provision the read-only AFFiNE role once with `reader/provision-source-role.sql` (and give it a password).
3. Run the **Deploy Note Sync** workflow (manual). It builds the image from `Farm.Sync/Dockerfile`, loads it on
   the VPS and runs `docker compose -f compose.yml -f compose.sync.yml up -d affine-sync`. The existing
   **Deploy Note** workflow is untouched.

Run only one worker per checkpoint and database; it locks a sibling `.lock` file to reject a second worker.

### Development stack

The Farm database and website belong to `main`; this branch only runs the worker and attaches to them.

```bash
# 1. In main (internet-facing): Farm database, website, Farm.Mind
POSTGRES_PASSWORD=... docker compose -f deployment/local/compose.yml up -d --build

# 2. Here: the worker joins the `farm-local_default` and `affine_default` networks
cd Farm.Sync
# export DATABASE_CONNECTION_STRING (Host=postgres;Port=5432;Database=aurafarming;...) and the AFFINE_* variables above
docker compose -f compose.dev.yml up -d --build
```

Set `FARM_DOCKER_NETWORK` if main's compose project is named differently.
`Meta.SpecifiedIssueUrl` (optional) must be a GitHub issue link such as
`https://github.com/owner/repo/issues/1`.

### Without Docker

Requires the .NET 10 SDK, Node 22 and PostgreSQL. No package feed or token is needed.

```bash
dotnet test PiggyFarm.slnx
npm ci --prefix Farm.Sync/reader && npm test --prefix Farm.Sync/reader
export DATABASE_CONNECTION_STRING='Host=localhost;Port=4554;Database=aurafarming;Username=postgres;Password=...'
export SYNC_STATE_PATH="$PWD/state/checkpoint.json"   # plus the reader's AFFINE_* variables
dotnet run --project Farm.Sync -- --once
dotnet run --project Farm.Sync
```

`--once` applies migrations and runs one iteration; startup/reader/validation/database/checkpoint
failures exit nonzero. Continuous mode logs iteration failures and retries after the configured interval;
startup failures exit nonzero so a supervisor can restart. SIGTERM/Ctrl+C cancels cleanly. The reader has a
two-minute timeout and its process tree is killed on timeout/cancellation.

## Snapshot and checkpoint guarantees

Reader contract:

```json
{
  "capturedAtUtc": "2026-01-02T03:04:06Z",
  "documents": [{
    "id": "document-id",
    "updatedAtUtc": "2026-01-02T03:04:05Z",
    "contentHash": "optional-reader-dependency-hash",
    "metadata": [],
    "bookmarks": []
  }]
}
```

Rows use all PascalCase properties of their Farm.Data entity, including required
integer `Id`, UTC timestamps, and the bookmark's `DisplayOrder` and `IsActive`.
Missing fields, null arrays/strings, invalid timestamps, oversized strings,
nonpositive entity IDs, and duplicate document/entity IDs fail the whole iteration
before writes. Empty strings are permitted where the database permits them.

Missing checkpoint means **1990-01-01T00:00:00Z**. Corrupt state fails explicitly
rather than silently resetting it. Only documents with `updatedAtUtc` strictly
greater than their own persisted timestamp, or with a different SHA-256 hash of
the exported document content, are applied. New document IDs strictly newer than
the baseline are applied independently of other documents' timestamps.
All changed documents are saved in one
transaction. Existing `CreatedAtUtc` values are retained; absent rows remain.
The writer locks the two target tables during its transaction and advances
their identity sequences past explicit source IDs, preserving later
database-generated inserts by other applications.
After commit, a flushed staging file on the same filesystem atomically replaces
the checkpoint. Failures before commit never advance it. A crash or checkpoint
write failure after commit replays idempotent upserts on retry.

Checkpoint JSON persists `documents` keyed by source ID, each with `updatedAtUtc`
and `sha256`, plus optional reader `contentHash`, alongside an informational
`capturedAtUtc` watermark. The watermark
never decreases, is bounded on advancement by the maximum observed source time
and capture boundary, and does not gate document writes. An empty/unchanged
snapshot does not advance it. The reader provides `capturedAtUtc` as the maximum
observed source timestamp, not its wall clock, and should read source rows in one
consistent PostgreSQL transaction. Documents newer than the supplied capture
boundary fail validation.

The reader can provide optional `contentHash` for each document, derived from
its complete source dependencies. Changes to that hash independently trigger
replay, including dependency changes whose timestamps do not increase. Meta's
source dependencies include page/workspace metadata plus CRDT properties
(`db$workspace$docProperties`) and custom property definitions
(`db$workspace$docCustomPropertyInfo`). The reader merges snapshots and pending
updates in a read-only repeatable-read transaction, deriving Meta's timestamp
from the maximum of its dependency timestamps.

Per-document checkpoints avoid skipping a late transaction below another
document's timestamp. Content hashes additionally detect same-timestamp or
older-timestamp changes, as long as the exported payload changes. Hashes use
worker-serialized document JSON, so exports should keep row ordering stable.
Legacy timestamp-only checkpoints are accepted and their documents replay once
to populate hashes. Missing documents retain their saved checkpoints; deletions
never delete target rows. The worker cannot repair source exports that hide
documents or omit changed content.

Do not reset or remove a checkpoint without intending a baseline replay.
Database commit and filesystem checkpoint are intentionally not a distributed
transaction; replay after an interrupted checkpoint update is expected.

## View the target database

```bash
docker compose -f compose.sync.yml exec affine-sync dotnet AffineSync.dll --show-data   # production sidecar
dotnet run --project Farm.Sync -- --show-data                                            # local
```

This only queries the target database. It does not apply migrations, read AFFiNE, sync rows, or touch the
checkpoint, and it can run while the scheduler is active. `exec` reuses the running worker's connection
configuration.
