# Static release and future request-data recovery (OPS03)

Plan/tabletop only, 2026-10-22. No backup, restore, withdrawal or deployment is
executed. [QA02](integration-review.md) supplies per-candidate approval/evidence.
The next applicable release type is the existing static Vite app; the request
service is a separate future release type.

## Ownership and environments

| Responsibility | Required accountable decision / current limit |
| --- | --- |
| Release and static rollback | Repository owner must name/authorize the operator and immutable revision/artifact for each deployment; no standing production authority is assumed. |
| Claim correction / withdrawal | GOV01 must name the content steward and withdrawal authority. Missing authority is an operational gate, not filled by generated staffing. |
| Future service data and retention | Approved service owner/operator and privacy policy required before REQ08/REQ10. None is appointed by this plan. |
| Local verification | `web/compose.dev.yml` is the isolated localhost-only web project at port 5183. Source mounts and dependency volume are development conveniences, not backups. |
| Rehearsal / production | OPS04 needs a named disposable environment, synthetic data, bounded restore permission and cleanup authorization. Never point a rehearsal at production or a shared database. |

## Compatibility inventory

Current app is static with canonical `raize`/`impala` car URLs and stable occurrence
IDs. `dist` is a coherent build artifact: HTML, hashed JS/CSS, books and media
must come from the same revision. Rollback must not combine old HTML and
unrelated new chunks. Preserve previous asset availability during cache turnover
where the actual deployment mechanism supports it; verify the real serving/cache
behavior rather than assuming an atomic directory swap.

Current observations are memory-only in a tab and disappear on reload; there
is no durable observation recovery promise. Onboarding uses
`30s.onboarding.v1`; rollback must not clear user settings. Identity package
schema version 1 is a separate contract; older readers reject unsupported
versions explicitly. No current change migrates a database.

Future requests need an explicit API/schema-version compatibility matrix,
backward-readable records and an expand/migrate/contract order. During a mixed
version window the old frontend/server must either read the new records safely
or return an explicit unavailable/version error; never silently drop fields.
Irreversible destructive migrations require a separately approved data-preserving
recovery path. Restoring static files alone cannot revert them.

## Approved-per-release static rollback sequence

1. Record the incident, affected artifact SHA, symptoms, evidence and scope.
   Stop further release activity. Use the named authority to approve withdrawal
   or rollback; do not grant the investigating agent that authority implicitly.
2. Select the last known-good immutable artifact/revision with its original
   configuration and passing acceptance evidence. Confirm the needed assets and
   current URL/state compatibility. Do not rebuild an old SHA with unknown new
   dependency/configuration inputs and call it the original artifact.
3. In the authorized target, apply the existing deployment mechanism to that
   coherent static artifact. This document supplies no server credentials or
   production shell commands. Do not reset/force-push repository history.
4. Verify actual root/asset responses, both catalog IDs, child links and Back,
   camera/selection/isolation, affected repair/learning entry points and narrow/
   keyboard behavior. Record serving SHA and cache observations.
5. Monitor the specific incident symptom, retain audit/evidence and reopen
   affected acceptance. Do not call rollback successful from an HTTP 200 alone.

## Claim withdrawal sequence

Identify the exact affected source/claim and all lesson/guide entry points.
The accountable steward chooses correction versus withdrawal. Missing or
contradictory applicability remains unavailable, with an honest explanation;
do not substitute an older unreviewed procedure or erase the correction history.
Prepare a bounded source/content change, use QA02, and obtain separate deployment
approval. Preserve canonical subject identity when content is withdrawn so old
links report unavailable content rather than resolving to a different claim.

## Conditional future service backup and restore

Before a durable service goes live, its owner must approve backup location,
encryption/access, retention/deletion, schedule, measured recovery objectives,
database/provider-consistent snapshot method and verification. No target recovery
time or allowable data loss has been agreed; both remain explicit decisions.
Do not claim that a live SQLite file copy or frontend export is a safe backup.

A restore plan must name backup revision/time, integrity and migration version,
destination isolation, writes-paused behavior, replay/idempotency boundaries and
private audit handling. Writes after the backup boundary may be lost or require
reconciliation; distinguish acknowledged from unacknowledged submissions and
prevent idempotency keys being reused with different payloads after restoration.
Do not overwrite live data to discover whether a backup works.

Verify schema/version, record counts and checksums appropriate to the provider,
known synthetic records, canonical uniqueness, duplicate/idempotent replay,
status revision conflicts, access controls, privacy retention and API/frontend
compatibility. Restore rehearsal is OPS04, not a checkbox satisfied by this text.

## Recorded tabletop outcomes (no execution)

| Scenario | Expected decision sequence | Outcome / unresolved gate |
| --- | --- | --- |
| A generic lesson's cited source is withdrawn | Identify exact claim and linked subject; suspend claim availability; prepare a source correction/withdrawal preserving IDs; named steward approves; QA02 plus authorized static release; verify deep link reports unavailable rather than another lesson | Logical sequence reviewed against current static/identity behavior. Steward/withdrawal authority and deployed rollback remain unproved. |
| A future migration fails after new writes | Pause writes; identify committed schema/data state; prefer compatible forward repair if safe; restore only to an approved isolated destination; quantify lost/replayed submissions; validate uniqueness/revisions/auth before switching traffic | No service/provider exists, so no backup/restore evidence or data-loss bound is claimed. REQ08/REQ10 and OPS04 remain gates. |
