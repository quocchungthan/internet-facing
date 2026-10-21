# 30s backlog handoff — resume prompt

Paste the **Prompt** section below into a new agent session opened in `C:\Users\727124\src\30s`.
Written at end of day; work was stopped mid-verification and everything was committed as **WIP**.

---

## Prompt

Continue the 30s product backlog on branch `feat/30s-component-workshop` in
`quocchungthan/internet-facing` (APP_DIR = current directory). Finish verifying and
closing backlog issues **#40, #41, #42, #43, #46**, whose implementation is already
committed (WIP commit, see "State"). Then report. Do not start blocked items.

### Settings

- Repo: `quocchungthan/internet-facing`, branch `feat/30s-component-workshop`
  (commit directly on this branch; do not create a new branch — the pre-push secret
  hook scans all history on a new branch push and blocks). Never `--no-verify`,
  never force-push, never hard-reset.
- Auth: map `GH_PAT` process-locally to `GH_TOKEN`; never print tokens.
  Push command (slow, ~90 s), then verify with `git ls-remote`:
  `export GH_TOKEN="$GH_PAT"; git -c credential.helper= -c "credential.helper=!gh auth git-credential" push origin feat/30s-component-workshop`
- Shell policy (user rule): no PowerShell syntax. Use Git Bash by full path:
  `& 'C:\Users\727124\AppData\Local\Programs\Git\bin\bash.exe' -c '<bash>'` with `/c/Users/...`
  paths. Beware: a single quote inside the bash script ends the `-c` string — write
  multi-line scripts to a `.sh` file or use file edit tools. No `rg`; use built-in grep/glob/view.
- Shared memory: eldervibe MCP — call `get_briefing` first.
- Docker (user tests here): `web/compose.dev.yml`, service `web`, container
  `thirty-s-workshop-dev-web-1`, `127.0.0.1:5183 -> 5173`, source bind-mounted (live edits).
  Rebuild/run: `cd web && docker compose -f compose.dev.yml up -d --build`, then `curl -sf http://127.0.0.1:5183`.
  **Leave it running for the user at the end.**
- GitHub board: Project 15 "Outsourcing the tools" (Todo / In Progress / Done only).
  Closing an issue moves its card to Done automatically. Issues don't auto-close
  from this non-default branch: close manually with an evidence comment.
  Do NOT add a "Blocked" status option via GraphQL (it would wipe all card statuses);
  a `blocked` label is used instead.

### Scope and decisions already made

- User: "I do approve all, just continue" — product approval for backlog items.
- **Blocked** (label `blocked` + comment, do not implement): #39, #44, #45, #47 —
  require a named qualified technical/safety reviewer (#47). Product approval does
  not replace that review.
- Done earlier and closed: R1 (merged), #38 orbit-drag zoom (`882c806`),
  #48 operation boundary (`dc8221e`), #49 catalog/wiper operation contracts (`7df1548`).
- #40/#43 list #39 as a dependency; they were implemented without a new safety gate,
  reusing the existing wiper safety copy — state this in the close comments.
- Recall links: GOV.UK and NHTSA only (no unverified Japanese MLIT URL).

### What is implemented (in the WIP commit)

- **#46 onboarding**: `web/src/features/onboarding/*` — `FirstVisitGuide` native
  `<dialog>`, storage key `30s.onboarding.v1` (new/completed/dismissed/unavailable),
  steps only for enabled features with present `[data-onboarding]` targets, Skip/Back/Next/
  Finish, Escape dismisses, failure notice when storage blocked, "Replay introduction"
  (help panel in 3D shell; header in `ContentWorkshop`). New flag
  `VITE_FEATURE_ONBOARDING` (default true) in `config/featureFlags.ts`.
- **#41 guide labels / applicability**: `features/repair/domain/guideReference.ts`
  (strict zod schema, `resolveApplicability`, `officialRecallLookups`); `wiperSource`
  renamed to `wiperReference`; `RepairGuide` shows `.guide-label[data-applicability]` and a
  "Guide source and applicability" region.
- **#43 wiper outcomes**: `wiperGuide.ts` adds outcomes improved/unresolved/worse/
  not-tested, "Worse…" and "Could not run…" choices, `notTested` terminal step,
  before/after observation comparison; `RepairGuide` rewritten (trail of `{step, choice}`,
  initial/result observation fieldsets, outcome group, "Add this check to the observation log").
- **#40/#42 observation log + mechanic handoff**: `features/observations/*` — strict
  record schema with unknowns and append-only corrections, tab-scoped in-memory store
  (nothing persisted), `ObservationLog` UI with forms, entries, section toggles, plain-text
  handoff preview, copy (explicit failure message) and print (`@media print` shows only preview).
  Entry points: "Observation log" button in both shells (when repair enabled).
- CSS appended at the end of `web/src/styles.css`. Docs updated: `web/README.md`,
  `web/FEATURE-PLAN.md`, `web/ARCHITECTURE-MATRIX.md`.
- Tests: unit (onboarding, guideReference, observationRecord, wiperGuide, featureFlags) —
  101/101 passing; lint clean; `tsc -b --noEmit` clean. New browser specs
  `tests/onboarding.spec.ts` (4) and `tests/observations.spec.ts` (4) pass against Docker.
  `tests/fixtures.ts` pre-dismisses onboarding; camera/components/visual/repair/workshop
  specs import from it. `tests/featureFlags.spec.ts` extended to 16 combinations (O/E/R/L)
  plus outcome/label assertions — **not yet run**.
- Fixed during verification: handoff "Model" line showed the catalog year (e.g. 1967)
  beside the owner's year; now shows the model name only.

### Remaining steps

1. Production build, lint and unit tests passed at handoff (`npm run build` OK with only the
   existing >500 kB chunk warning; 101/101 tests). Rerun only if code changes.
2. Full browser suite vs Docker, **1 worker** to avoid dev-server contention:
   `PLAYWRIGHT_BASE_URL=http://127.0.0.1:5183 npx playwright test --workers=1 --reporter=line`
   (~10+ min). Stopped run (2 workers) reached 18/49 with **one failure to classify**:
   `camera.spec.ts:55 › whole vehicle body zooms in and orbits during its initial fit`
   (#38 regression test). Rerun it alone first:
   `npx playwright test tests/camera.spec.ts --workers=1`. If it fails alone, investigate
   whether the new `.onboarding-*`/log changes or the fixture affect it; fix, don't skip.
3. Expected intentional snapshot change: `repair.spec.ts` `wiper-guidance.png`
   (guide now has label, observation fieldset, source section). Inspect the diff image,
   then update **only that snapshot** (`--update-snapshots` scoped to that test). Never
   bulk-accept.
4. `npm run test:features` (16 production builds, ~15 min).
5. Save screenshots/results to `C:\Users\727124\src\30s-backlog-40-46-evidence\` (outside repo).
6. Amend nothing; make a follow-up commit referencing #40 #41 #42 #43 #46 with any fixes
   and the snapshot, push, verify remote head.
7. Close #40, #41, #42, #43, #46 with evidence comments (tests run, commit SHA, limits);
   confirm cards show Done on Project 15.
8. Rebuild/restart Docker and verify 5183 responds; leave it running.
9. Report: commits, results, evidence path, and remaining blocked items #39 #44 #45 #47.

### Resources

- Backlog issues: `gh issue view <n> -R quocchungthan/internet-facing` (use `GH_TOKEN="$GH_PAT"`).
- Original R1 handoff (pinned, private): `quocchungthan/backup-prompts` @
  `543a0a595ee251481228262f00c57f10af8faf39`, dir `30s-build-readiness-11c9f683`
  (contracts: release, ux, source; build-plan.md).
- Prior session checkpoints: `C:\Users\727124\.copilot\session-state\60da3581-65a9-4e36-8d58-67c6941ef729\checkpoints\`.
- Partial full-suite log: `C:\Users\727124\src\30s-backlog-full.log`.
- Key files: `web/src/features/exploration/presentation/ExplorationWorkshop.tsx`,
  `web/src/components/ContentWorkshop.tsx`, `web/src/features/repair/presentation/RepairGuide.tsx`,
  `web/src/features/observations/**`, `web/src/features/onboarding/**`, `web/src/styles.css`,
  `web/tests/*.spec.ts`, `web/tests/fixtures.ts`.

## State at handoff

- All work committed on `feat/30s-component-workshop` as a WIP commit
  ("wip(#40 #41 #42 #43 #46): ...") and pushed. Issues #40–#43, #46 remain **open**.
- Docker container was left running at http://127.0.0.1:5183 (bind-mounted, so it
  already serves this code).
