# 30s component workshop feature plan

## Orientation and current baseline

30s means being in your thirties: practical car troubleshooting and maintenance
guidance, not a timed challenge or CAD tool. The 3D supports understanding and
performing a task; it is not itself an authoritative repair manual.
The root-only React/Three.js frontend
explains a 2019 Japanese Toyota Raize configuration and a 1967 US Chevrolet Impala
configuration. Sources, variant caveats, real reference photographs and original
field books already accompany the schematic viewer.

The starting baseline had 12 flat selectable part categories per car, exterior/X-ray/
exploded views, opening panels, guided lessons, URL/history selection and PNG
capture. It had no assembly hierarchy or standalone-part viewing; its camera
targeted the whole car and could not zoom closer than 3.5 scene units.

The feature implementation adds validated car/system/assembly/occurrence
hierarchies, engine children, subtree isolation, mesh picking and subject-relative
focus/orbit/zoom/reset. Engine isolation retains intake and lubrication. All four
wheels, brakes and struts have separate occurrence IDs with shared specifications.
Rounded edges, curved wheel arches, smoother tires/tubes/cylinders and engine
fasteners improve close-up clarity. Geometry remains educational and schematic.

The existing geometry is procedural and approximate. Increasing canvas resolution
alone will not produce accurate detail. Curvature needs appropriate profiles,
segments, normals and geometry; OEM accuracy is not an acceptance claim.

## Branch, environment and integration

- Worktree: `C:\Users\727124\src\30s`.
- Feature branch: `feat/30s-component-workshop`, created from freshly fetched
  `origin/sub/30s` at `b5fbbe9`.
- Docker development: `docker compose -f compose.dev.yml up --build -d --wait`
  from `web`; open http://localhost:5183/. This is the feature worktree, with
  live source mounts, not a deployed production build.
- The user authorized publication of this feature and its architecture document
  for PR review into `sub/30s`. Merge/integration and production deployment
  remain human decisions. The main-owned deployment workflow remains unchanged.

This feature branch is prepared for the authorized review PR. No merge,
branch-protection configuration or production deployment is performed here.

## Architecture direction

Use business features as vertical slices with explicit technical responsibility
boundaries. The maintained [architecture matrix](ARCHITECTURE-MATRIX.md) uses
hierarchical business capabilities as columns and technical practices as rows,
with actual components and responsibilities in each cell. Implementation tiers
such as domain/application/presentation/rendering are not that matrix's axes.
Do not manufacture a file or abstraction for every intersection. Preserve
current catalog IDs, share links, books and source claims.

### Independent feature composition

Build-time `VITE_FEATURE_EXPLORATION`, `VITE_FEATURE_REPAIR`,
`VITE_FEATURE_LEARNING` and `VITE_FEATURE_ONBOARDING` default to `true` and accept only exact `true`/`false`.
`App` selects the exploration shell or the HTML-only `ContentWorkshop`.
Repair's optional location adapter and learning's standalone part selector
remove their runtime dependency on exploration. Disabled feature components
are not mounted/loaded; flags are not authorization or output-asset removal.
See the README for build configuration and the architecture matrix for ownership.
`npm run test:features` exercises all sixteen real builds sequentially, including
an all-disabled state, and is wired into the feature-quality workflow.

## First issue-to-resolution slice

The wiper-streak story implements safe preparation, inspection, cleaning, wet-glass
verification, replacement fitment guidance and service/unsafe outcomes. It is
general maintenance, explicitly not a documented vehicle-specific recurring fault.
AAA's cited wiper-care article supports the general checks. Variant-specific access,
blade sizes and connector removal are deliberately deferred to exact manuals and
supplier instructions. The 3D adds separate arms, blade carriers and rubber contact
edges, but remains a labeled generic schematic rather than verified service geometry.

Future slices should begin with a user symptom and credible applicability/source
evidence, then add the component detail needed for its checks and remedy. A complete
verified fault catalog, OEM-accurate assemblies and safety-critical repair procedures
are not delivered by this first story.

The implemented exploration slice lives in `src/features/exploration/`, with
`domain/`, `application/`, `presentation/` and `rendering/` horizontal layers.
Catalog contracts/content and existing learning panels stay in their established
locations; no unrelated monorepo application was reorganized.

`Item` represents a particular car, assembly or part occurrence: stable ID,
specification ID, parent ID and local placement. `ItemSpecification` describes
shared part identity and drawing kind/parameters. Multiple occurrences may share
a specification; they must not share selection identity.

Prefer a normalized list plus validated parent links and small selectors for
children/descendants rather than duplicated nested copies. Reject missing parents,
duplicate IDs, missing specifications and cycles explicitly. Existing category
IDs are not necessarily individual physical occurrences (e.g. four wheels); map
them deliberately rather than pretending each category is one physical part.

Keep React and Three.js out of domain contracts. The drawing factory belongs in
the rendering layer: typed drawing kinds select geometry builders. Unknown kinds
fail explicitly. A factory should replace growing conditional geometry dispatch,
not wrap every mesh in a new abstraction. Keep vehicle-specific dimensions and
educational disclaimers intact.

One subject contract should drive both a whole car and an isolated subtree:
visible item IDs, world-space bounds, focus target and camera-distance limits.
Focus fits those bounds; isolation removes unrelated geometry and labels but
retains descendants; restore returns to the complete car. Parts should not acquire
an unrelated second controls implementation.

## Independently verifiable increments

Each unit is at most two relative complexity points. Record the exact write scope
in session state before execution; discovered scope expansion must be explicit.
The broad implementation scope was approved in this CLI session.

Paths below are relative to `web` unless explicitly stated.

| Implemented unit | Points | Dependencies | Source | Acceptance |
| --- | --- | --- | --- | --- |
| Dev worktree and Docker | 2 | None | `Dockerfile.dev`, `compose.dev.yml`, `.dockerignore` | Healthy localhost container with live source |
| Browser baseline and plan | 2 | Docker | `playwright.config.ts`, this plan, `README.md` | Docker-targeted browser suite and external artifacts |
| Item/specification model | 2 | Baseline | `src/features/exploration/domain/items.ts` and tests | Distinct repeated occurrences; references/cycles validated |
| Drawing factory | 2 | Item model | `src/features/exploration/rendering/drawings/`, `CarModel.tsx` | Typed geometry dispatch; domain independent of Three.js |
| Hierarchical subject state | 2 | Item model | `domain/subject.ts`, `application/useWorkshop.ts` in exploration | Shareable selection/isolation and history; no stale subjects after car changes |
| Component tree and isolation | 2 | Factory, subject state | Exploration `presentation/`; `tests/components.spec.ts` | System/part/occurrence selection, engine descendants, isolation/restoration |
| Subject-relative camera | 2 | Isolation | Exploration `rendering/SubjectCamera.tsx`, `subjectBounds.ts` and tests | Bounds fitting, shared orbit/zoom/reset, close-up individual wheel |
| Curves and detail | 2 | Factory, camera | Exploration `rendering/drawings/`; `tests/visual.spec.ts` | Curved arches/rounded edges/smoother meshes and render budgets |
| Push quality checks | 2 | Browser baseline, viewer units | Repo `.github/workflows/verify-30s.yml`; `eslint.config.js`, package/lockfile, browser tests/snapshots | Lint/complexity, unit/build and visual/browser checks; uploaded evidence |

## Quality and first-story evidence

The feature quality workflow checks every `feat/30s-*` push and relevant PRs
targeting `sub/30s`. It runs build/type-check, ESLint, unit tests and browser tests.
SonarJS enforces per-function cognitive complexity <=15 across all source and
tests, with no legacy complexity exceptions. A typed factory avoids a growing
render switch; domain validation and camera geometry have focused unit coverage.

For viewer/UI changes, use deterministic Chromium, fixed viewports, reduced
motion, fixed camera/subject state, locally available fonts/assets and explicit
scene readiness. Keep screenshot artifacts in CI, not a capture for every commit
in source. Reviewed comparison baselines belong in version control only when
intentional and reproducible; never auto-approve changed snapshots in CI.
Five reviewed Windows Chromium baselines compare each car, isolated engine and
the issue-guidance view,
allowing at most 0.1% different pixels. Captures, render-budget evidence and
failure traces are collected as artifacts; CI never updates baselines.

Original desktop/mobile captures were retained outside the repository before
changing the drawings. The feature suite adds fixed car/engine comparison views.
The first story must demonstrate selection, focus, isolation with descendants,
orbit/zoom/reset, restoration, car changes and history navigation, with no browser
errors and no page overflow at 1440x900, 375x812 and 740x390.
Browser checks enforce fewer than 220,000 triangles / 320 draw calls for the
whole car and 20,000 triangles / 40 calls for the isolated engine. Tests expose
scene readiness and subject/visible-item/bounds metrics on the canvas, so they
assert geometry membership and camera behavior rather than only DOM visibility.
No hardware-independent FPS guarantee is claimed.

Rounded-box smoothness is capped at 2; wheel tires use 24 radial / 80 tubular
segments; pipes use 48 path / 16 radial segments. DPR is capped at 2 and shadows
remain 1024px. These balance close-up quality with bounded geometry cost.
The canvas renders on demand: selection, camera interactions and active hinges
invalidate frames, while a settled viewer does not continuously consume GPU/CPU.

Do not mistake HTML/canvas visibility or a screenshot file for correct rendering.
Interaction assertions and reviewed visual comparisons complement each other.
Branch-protection settings, paid assets and production deployment are outside
this local implementation scope.

## Known limitations

The initial container dependency install reports three audit findings (one
moderate, two critical). No forced dependency upgrades have been applied. Triage
the affected packages before treating this environment as a trusted release gate.
They originate in the existing Vitest 3 dependency chain, not production assets.
The lint additions use supported ESLint/SonarJS versions and add no audit findings.
The production build also reports a large Three.js chunk.

Software-renderer measurements are machine-specific: before enabling on-demand
idle rendering, after reducing rounded-box
subdivisions/material cost, the Raize/Impala whole cars measured 184,632/202,612
triangles and 254/287 draw calls. A 20-frame sample measured roughly 180/183ms
per frame in headless SwiftShader; engines measured 13,508/11,440 triangles and
25/30 calls, with roughly 82/94ms frame samples in that run. Earlier samples
varied substantially, so these are diagnostic observations, not GPU benchmarks.
The full-car scene remains expensive on software rendering; no claim of smooth
60fps on arbitrary hardware is made.

CI is authored and locally exercised; remote results must be checked on the
published review branch. Windows snapshots are platform-specific; Linux
contributors need separately reviewed baselines rather than silently updating
the Windows references.

## Local verification

The feature-flag review source passes `npm run lint`, `npm test` (34 tests),
`npm run build`, and `npm run test:browser` (20 scenarios against the production
preview, without updating snapshots), plus `npm run test:features` (all eight
production-build feature combinations). This includes real mesh picking,
engine/subtree isolation, occurrence selection, bounds-relative camera controls,
history/restoration, field-book compatibility, four visual comparisons and
portrait/landscape checks. The same browser runner supports the Docker dev URL.
Latest review screenshots and traces are retained outside the repo under
`C:\Users\727124\src\prompts\30s-feature-default-regressions` and
`C:\Users\727124\src\prompts\30s-feature-combinations-final`; earlier feature
evidence remains under `C:\Users\727124\src\prompts\30s-pr-review-results`
and `C:\Users\727124\src\prompts\30s-final-demand`.
