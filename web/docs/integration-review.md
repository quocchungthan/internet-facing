# 30s integration review packet (QA02)

Use this checklist per candidate, not as a claim that any release is approved.
Existing QA01 automation and focused checks supply evidence, not qualified
content approval or permission to merge/deploy. Design reference:
[DES01](design-contract.md). Content boundary:
[owner-check draft pack](owner-check-review-pack.md).

## Candidate and acceptance

- [ ] Record repository, branch, exact candidate SHA, parent/base SHA, story/card
  links and acceptance criteria. List unsupported criteria as blockers.
- [ ] Identify changed domain subjects separately from experience capabilities.
  Update the relevant architecture-matrix cells; do not count shared outcomes
  twice or introduce an unused abstraction.
- [ ] Link actual lint/unit/type/build results, focused browser cases, relevant
  real-build feature combinations, screenshots/diffs/traces and runner versions.
  A command definition or old passing run is not current execution evidence.
- [ ] Record failed, skipped and flaky checks without hiding them behind retries.
  Identify required checks or the explicit manual integration gate.

## Design, content and compatibility

- [ ] Compare before/after views against DES01 at desktop, portrait and landscape.
  Name intentional placement/style changes and obtain explicit product/design
  acceptance; do not label redesign as cleanup. Record keyboard/focus, native
  semantics, touch, overflow, contrast and reduced-motion evidence/limits.
- [ ] Preserve legacy car/item URLs, canonical occurrence IDs, Back/history,
  camera/selection/isolation and independently disabled features.
- [ ] For schema/content changes, record versions, unknown/missing/error handling,
  references and legacy fixtures. Distinguish supported configuration from actual
  vehicle applicability. Unknown metadata is not verified fitment.
- [ ] Review source locators, date, rights, wording and claim-level applicability
  separately from code correctness. Identify the accountable review decision.
  Qualified executable-guidance approval cannot be replaced by AI/product review.
- [ ] State frontend/API/storage compatibility, mixed-version behavior and migration
  order where applicable. For UI-only static changes explicitly record that no
  API/database migration occurs; do not invent a backend check.

## Authorized integration and deployment

- [ ] Obtain merge/integration authorization for the named revision and destination.
  Do not modify protection rules or bypass hooks/checks.
- [ ] After an independently authorized merge, record the real merged SHA and its
  actual check outcomes; candidate/PR checks do not prove the merged result.
- [ ] Run focused merged-result smoke paths: raw root HTML, both catalog IDs,
  child deep link/Back, selection/isolation/camera and affected owner workflows.
  Record actual source/serving revisions and explicit manual gates.
- [ ] Obtain separate deployment authorization. Do not treat an integration
  request or a green build as production-deployment approval.
- [ ] If authorized to deploy, record staged/production target, deployed SHA,
  root/assets/cache checks, no-JavaScript fallback metadata, narrow/keyboard
  smoke, independent-feature configuration and error/rollback triggers.

## Rollback and withdrawal

- [ ] Record the previous known-good revision/artifact and the authorized actor.
  Revert a bounded code change normally; no force push or destructive reset.
  Deploy a prior immutable static artifact only with separate approval.
- [ ] For content corrections/withdrawal, identify affected claims and entry
  points, preserve the audit trail and disclose unavailable content rather than
  silently substituting an older unreviewed instruction.
- [ ] Record browser-local compatibility: this increment must not overwrite
  onboarding/preferences/observation state. Tab-only observations are not a
  durable store or a backup promise.
- [ ] Where future database/request services exist, include forward/backward
  schema compatibility, data-preserving rollback, restore ordering and verified
  recovery in an approved disposable environment. Rolling back frontend code
  does not undo a database migration or recover deleted records.
- [ ] Stop rollout for failed checks, broken deep links/controls, newly unsupported
  applicability or missing safety review; record the owner-visible impact and
  escalation route. Unassigned release/withdrawal authority remains an open
  GOV01 decision, not an assumed duty.

## Observed integration baseline (CI01, 2026-10-22)

#37 was already merged by its authorized actor on 2026-10-21, before this work.
Observed head `88ededaa23003189ee490d3fe57a12a8df576b57`, actual merge
`c7f51768a80e99948e7d6c43e89eb7335d50af5b`. Its PR rollup reported successful
`quality` checks:

- [Run 39629809977](https://github.com/quocchungthan/internet-facing/actions/runs/39629809977),
  completed 2026-10-21 16:12:50 UTC. Non-expired artifact
  `30s-browser-6540a1566d540efc4e1bf18106c960ecd76eab37`
  (artifact ID 12359212950); the artifact name is a PR-check revision, not the
  merge SHA. Do not label this merged-result quality evidence.
- [Run 39629802077](https://github.com/quocchungthan/internet-facing/actions/runs/39629802077),
  completed 2026-10-21 16:12:36 UTC.

The merge SHA's check-runs endpoint reported only successful `build` and
`Build sub/30s and deploy static frontend` jobs, not `quality`. This records
previous activity; this task did not trigger a deployment.

Classic protection lookup for `sub/30s` returned **Branch not protected (404)**;
the effective branch-rules endpoint returned `[]`. No configured required
quality check was observed and no settings were changed. The workflow currently
triggers for `feat/30s-*` pushes and PRs into `sub/30s`, not direct `sub/30s`
pushes. Therefore **manual quality evidence/review is an explicit integration
gate on this work**, and deployment/build success is not substituted for a
merged-result quality run. Any settings/trigger change needs its own bounded
decision; workflow presence is not enforcement.

This document is the protocol and actual historical inspection record, not
evidence that every future candidate passed or that unassigned human gates
were resolved.
