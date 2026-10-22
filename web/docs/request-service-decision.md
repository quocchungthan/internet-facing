# Minimal shared-request service decision packet (REQ01)

**Design recommendation, not a deployed service or provisioning approval.**
2026-10-22. The existing product is a root-only static React/Vite app served by
Caddy; local Docker serves only Vite. There is no approved request database,
hosting account, service URL, operator, cost budget, maintainer identity provider
or retention policy. Do not turn examples below into a fake live queue.

## Alternatives and recommendation

| Option | Fit and tradeoff under actual known constraints |
| --- | --- |
| Small server API plus durable database | Fits the existing static/Caddy boundary without changing frontend hosting. One server owns validation, atomic duplicates/idempotency, authorization, moderation and bounded list results. An operator must own deployment, private backups, restore, updates, abuse and costs; none is appointed. |
| Hosted request-store adapter | Can reduce infrastructure operation if an approved provider actually supplies transactions/uniqueness, protected server-side writes, auth, retention/export/deletion and tested backup/restore. Provider account, region, cost, limits and policy are unknown. A browser SDK/public write token cannot substitute for this server boundary. |
| Browser storage or GitHub issue links alone | Not the specified shared queue: browser data is not shared/durable service evidence; raw issue creation does not by itself provide the required canonical atomicity, payload-bound replay, private audit and conditional availability transition. Do not claim parity. |

Recommend a **small single-instance API behind the existing reverse-proxy
boundary, with transactional SQLite on durable operator-owned storage for an
initial bounded MVP**, subject to explicit operator/storage/backup/cost approval.
This is a recommendation, not a claim about provisioned capacity. Do not put the
database in an ephemeral container layer or the web source/dependency volume.
If approved hosting is multi-instance or lacks durable local volumes, choose a
transactional hosted database/adapter instead and re-prove the same guarantees;
do not bolt distributed file writes onto the single-instance design.

Keep runtime choice bounded at REQ02 intake: reuse the repository's existing
server ecosystem where appropriate, but no server project/package/dependency is
created by this packet. Confirm actual deployment ports/base paths and backup
facility before choosing implementation files. No invented hostname, connection
string, public token or cost estimate is supplied.

## Boundaries and proposed contracts

Proposed seam: `car-requests` domain contracts -> application operations ->
durable store and HTTP/auth/abuse adapters. Follow the existing
`defineOperation` / `runOperation` input/output/failure boundary rather than
putting persistence/authorization in a React component. Existing static catalog
URLs and CAT01 identities remain compatible; no UI dependency on SQL tables.

| Operation | Proposed input | Proposed public output / semantics |
| --- | --- | --- |
| create v1 | `schemaVersion:1`, versioned requested configuration with explicit unknowns, requested capability enum; opaque idempotency key in request context/header | `created` or `existing` plus public request projection. Server validation occurs before a transaction; acknowledged output only after commit. |
| list v1 | bounded limit (1-50), opaque cursor, optional supported status filter | public projections and next cursor; no private moderation/audit/owner metadata. No unbounded table dump. |
| updateStatus v1 | request ID, expected positive revision, allowed target status, optional public bounded reason code; authenticated maintainer context | new revision/public projection, or explicit conflict/forbidden/unavailable. Private actor/audit recorded transactionally, never in public projection. |

Proposed public projection:
`{id, schemaVersion:1, revision, requestedConfiguration, requestedCapability,
status, submittedAt, updatedAt, publishedCoverageId?}`.
No requester identity, VIN, plate, location, contact, email, arbitrary complaint,
private observation, payment or support account is requested by default.

Configuration fields align with CAT01 (make/model/generation/model years/market/
powertrain/variant), but a request is **not** a supported configuration and must
not be assigned a catalog ID or approved coverage. Final persisted schema/version
and exact capability enum need agreement before REQ02. Inputs use strict runtime
schemas, bounded text lengths/body sizes and canonical enums; reject unknown
fields/unsupported versions with explicit errors. Do not interpret arbitrary
HTML, URLs or uploaded data. Any future optional free text needs separate
moderation/privacy scope.

## Canonical duplicates and idempotency

These solve different problems and require database transactions/constraints.

1. Normalize configuration text by CAT01's NFC/case/whitespace rule only. Preserve
   distinct markets/variants/powertrains and requested capabilities.
2. Only an agreed fully known exact configuration/capability may receive a
   canonical unique key. Unknown/ambiguous requests can be suggested as related,
   but must not be auto-merged as duplicates or assigned guessed values.
3. Compute the canonical key and normalized payload digest on the server.
   One transaction resolves/inserts the canonical record under a unique
   constraint and records the idempotency key, digest and committed response.
   Race losers read the existing canonical record; they do not create a second.
4. Same idempotency key + same payload returns the committed response after
   a timeout/retry. Same key + different payload is an explicit conflict, not
   silent reuse. A crash before commit produces no success; after commit the
   replay row and request must both exist. REQ09 proves concurrent/crash cases.
5. Idempotency retention and key scope/expiry require explicit owner policy and
   backup/restore treatment. Client retries preserve the user's input/key;
   frontend-only duplicate detection is not the guarantee.

## Proposed lifecycle and testable examples

Proposed states: `submitted -> triaged -> in-progress -> available`, with
`declined` from submitted/triaged/in-progress. No direct submitted-to-available.
Reopening/withdrawal needs a separately specified transition; do not silently
mutate an available request when its coverage is withdrawn.

| Example | Expected response / reason |
| --- | --- |
| Valid complete configuration + capability, new key | `created`, revision 1, submitted; no promise of dates or fulfillment. |
| Concurrent exact same configuration/capability, different keys | One canonical request; other response `existing` with its public projection. Atomic unique constraint, not check-then-insert alone. |
| Retry same key/payload after network timeout | Same committed response; no duplicate and no lost browser input. |
| Same key with changed market/capability | `conflict` / payload mismatch, not an existing success. |
| Unknown market or generation | Valid ambiguous submission if schema permits it; no canonical exact-match merge and no inferred market. |
| Malformed/oversized/unknown-field input | `invalid_argument`; no persisted record or success acknowledgement. |
| List request | Bounded public projections only; no requester identity or private audit leaks. |
| Unauthenticated/unauthorized status write | `unauthorized` / `forbidden` at HTTP/auth boundary before application mutation, no status/revision/audit success. |
| Stale expected revision or illegal transition | `conflict`, return safe current revision/state as approved; no last-write-wins overwrite. |
| Attempt available without reviewed matching published coverage | `conflict` / coverage unavailable. A screenshot, donation or draft package cannot make it available (REQ07/CAT04). |
| Store unavailable / failed commit | Explicit `unavailable`/appropriate failure; preserve browser input, never an invented queue item. |

Private status audit binds actor, time, old/new state and revision in the same
transaction. Authorization is server-side, using an approved maintainer
OIDC/session identity and explicit role, not a browser-stored secret or hidden UI.
No identity issuer or administrator account is selected/created here; REQ11
must verify it before protected writes are enabled.

## Abuse, privacy and implementation gates

Recommend server-side request/body limits, rate/concurrency controls, bounded
pagination and moderation state/reason codes. Do not claim frontend throttling
prevents abuse. Evaluate the approved proxy/provider's actual trusted-client-IP
boundary; minimize/security-limit any transient abuse logs and never publish IPs.
No tracking, contact identity or general analytics is required.

REQ10 needs the named owner to approve a clear notice, finite retention/deletion,
private audit/access and idempotency-retention interaction before live collection.
This packet cannot invent lawful assurances, data residency or an actual
retention duration. REQ08 needs provider, operator, cost/storage and disposable
test-environment approval. OPS03/OPS04 own data-safe recovery/rehearsal.

Proposed later write scopes, subject to bounded approval:
REQ02/REQ09: strict `car-requests` contracts, transactional store/migrations and
concurrency/crash tests; REQ03: public validation/list/create HTTP adapter and
server abuse handling; REQ06/REQ11: legal transitions, revision/audit and protected
maintainer adapter; REQ04/REQ05: existing workshop UI with explicit loading/error/
unavailable/retry states and synthetic tests, never a fake deployed list.

Design examples above were checked against duplicate-versus-unknown identity,
commit/replay semantics, public/private output separation and transition gates.
They are a reviewable contract proposal, not executed provider transactions.
No database, API, credentials, hosted cost, payment integration or live service
has been introduced.
