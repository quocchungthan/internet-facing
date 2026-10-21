# 30s — the everyday machine, explained

A static, root-only React + Three.js car troubleshooting and maintenance workshop.
The name refers to being in your **thirties**, not thirty seconds.
The full-viewport scene is always the primary surface. Panels scroll internally.
The first two learning configurations are the 2019 Japanese Toyota Raize Z
1.0 turbo 2WD D-CVT and a 1967 US Chevrolet Impala Sport Sedan with a 283 V8 and
Powerglide. Real-world vehicle identity must be verified separately.

## Independent features (build-time)

The static app supports four independent environment flags:

| Flag | Default | Responsibility |
| --- | --- | --- |
| `VITE_FEATURE_EXPLORATION` | `true` | Interactive 3D, component hierarchy, focus/isolation and camera controls |
| `VITE_FEATURE_REPAIR` | `true` | Wiper symptom/check/remedy/verification guidance, outcomes and the tab-only observation log/mechanic handoff |
| `VITE_FEATURE_LEARNING` | `true` | Part explanations, learning tour, reference panels and field books |
| `VITE_FEATURE_ONBOARDING` | `true` | Skippable first-visit introduction for the enabled features (state kept in `localStorage` key `30s.onboarding.v1`) |

Set flags in the build environment, for example in Git Bash:

```bash
cd web
VITE_FEATURE_EXPLORATION=false VITE_FEATURE_REPAIR=true VITE_FEATURE_LEARNING=true npm run build
npm run preview
```

Only exact `true`/`false` values are accepted; omitted flags default to `true`.
Invalid values fail configuration instead of silently enabling a feature.
Rebuild after changing flags; setting variables on the static server or preview
does not change already-built JavaScript. Environment files are local and must
not be committed. Never put secrets in `VITE_*`: values are public client config.

With exploration disabled, repair and learning use a scrollable HTML workshop
with vehicle selection. Repair keeps its complete observation flow without any
3D-location controls; learning has an independent part selector and field books.
Disabling repair or learning removes their entry points and lazy component
mounts while leaving exploration usable. All sixteen combinations are supported;
all-disabled shows an explicit configuration message rather than a blank page.
The introduction never appears when the three business features are all off.

The observation log keeps entries only in memory for the current tab; nothing is
saved, uploaded or shared, and reloading discards it. The handoff is plain text
for copy or print. The wiper guide is labelled as generic guidance (not verified
for the model) and links official recall services externally; 30s performs no
recall lookup itself.

These flags control feature execution, not authorization or asset secrecy.
Build output may still include disabled-feature chunks, books and images; they
are not protected resources. Disabled feature components are not loaded by the
UI. The Vite manual vendor grouping was removed so shared React code does not
pull the Three.js renderer into builds running without exploration.

Run `npm run test:features -- --output=<external artifact directory>` to build
and exercise every combination sequentially against its own temporary preview.
The suite verifies remaining feature workflows, disabled component requests,
small-screen layouts and the all-disabled state. Previews stop after each case.
This suite is separate from the default all-enabled browser/visual regressions.

## Local development

### Docker development (feature worktree)

From this worktree's `web` directory:

```bash
docker compose -f compose.dev.yml up --build -d --wait
docker compose -f compose.dev.yml logs -f web
```

Open http://localhost:5183/. Source is bind-mounted with polling for
Windows/Docker Desktop live reload. Dependencies live in a Docker volume and
`npm ci` synchronizes them with the lockfile on each container start.
This is a development server only, bound to localhost; production remains static.

```bash
docker compose -f compose.dev.yml exec web npm test
docker compose -f compose.dev.yml exec web npm run build
docker compose -f compose.dev.yml down
```

To exercise the running Docker server with the existing browser suite, install
the native browser-test dependencies using the native-development steps below,
then run:

```bash
PLAYWRIGHT_BASE_URL=http://127.0.0.1:5183 npm run test:browser
```

The dedicated Compose project does not start or modify the monorepo's other apps.
See [FEATURE-PLAN.md](FEATURE-PLAN.md) for the architecture, implementation
increments and quality budgets.
The [architecture matrix](ARCHITECTURE-MATRIX.md) maps hierarchical business
capabilities to technical practices, responsible modules and current gaps.

## Start with an issue

Choose **Wipers leave streaks? Start a guided check** on the home view, or
**Troubleshoot wiper streaks** in the tool rail. The first repair-guidance slice
connects safe preparation, observations, cleaning, replacement-fitment guidance
and parked verification. Persistent or uncertain problems stop at a service
handoff rather than an invented diagnosis. Previous/restart controls let you
correct observations; the trail is in-memory, not a saved service record.

Each step isolates its relevant wiper assembly, blade carrier or rubber edge.
**Show windshield location on car** restores context. Silver arms, amber carriers
and dark contact strips are separate selectable Item occurrences in the body
hierarchy, with shared drawing specifications. Shapes and attachments are generic,
not verified vehicle-specific service geometry.

The AAA source linked inside the guide supports general wiper care. This is
**not a documented Raize or Impala recurring defect**. No blade sizes, adapter
release sequence or model-specific repair specifications are asserted; use the
correct vehicle manual and blade supplier instructions or professional fitting.
There is not yet a catalog of verified model-specific faults or repair procedures.

## Explore a component

Select a part label or open **Component index**, then expand
**Car / assembly / part hierarchy**. Systems, assemblies and individual wheel/
brake/strut occurrences have distinct identities. **Focus selection** fits the
camera to the subject; **Isolate selection** removes unrelated parts while
keeping descendants. An engine exposes its block, head, cover, intake and
lubrication children. Click geometry or a child button to drill down.

Orbit, zoom, reset, view modes and PNG capture use the same controls for the car
and isolated components. **Show whole car** restores the full vehicle.
Closing a part panel also returns to the whole car.
Share `/?car=raize&part=engine&isolate=1` or
`/?car=raize&part=wheels&item=wheel-front-left&isolate=1`.
Browser Back restores the selected occurrence and isolation state.

### Native development

Use Node 22 LTS (Node 20.19+ also works with the current Vite release).

```bash
cd web
npm ci
npm run dev
npm run lint
npm test
npm run build
npx playwright install chromium
npm run test:browser
bash scripts/test-caddy.sh
```

`dist/` is the only deployable application output. There is no Node server,
API, database or production runtime Docker container. Caddy serves files directly.
`/?car=raize&part=engine` is a shareable state, not a nested route.
The car, selected item and isolation participate in browser history; other view
settings are local UI state.
The optional Caddy integration check uses Docker and a temporary local listener
on port 4180; it stops its container when finished. It exercises the production
route configuration with TLS disabled only in the isolated test configuration.

## Responsibilities

| Directory | Responsibility |
| --- | --- |
| `src/domain` | Runtime-validated vehicle/part contracts, referential integrity and URL selection |
| `src/content` | Canonical JSON vehicle catalog, facts, source scopes, photo credits and part relationships |
| `src/hooks` | Shared reduced-motion preference |
| `src/features/exploration/domain` | Item/specification hierarchy, integrity, positions and shareable subject state; no React/Three.js |
| `src/features/exploration/application` | Workshop navigation, selection, isolation and tour state |
| `src/features/exploration/presentation` | Component hierarchy and selected-subject controls |
| `src/features/exploration/rendering` | Typed drawing factory, procedural drawings, subject bounds, camera and PNG capture |
| `src/features/repair/domain` | Sourced issue/check/remedy/verification graph, explicit stop conditions and transition validation |
| `src/features/repair/presentation` | Observation trail, accessible step guidance and links to exploration items |
| `src/components` | Accessible HTML panels, reusable controls, photographs and book rendering |
| `public/books` | Original Markdown companion books, with anchors matching part IDs |
| `public/media` | Committed, attributed real photographs; no runtime image-host dependency |
| `tests` | Browser interaction and full-viewport regression checks |

### Add a car or part

1. Add a catalog JSON file matching `vehicleSchema`, and import it in
   `src/content/index.ts`. Stable IDs connect URLs, books, geometry and related parts.
2. Add a Markdown book and a chapter anchor for every part. Link with
   `/?car=<id>&part=<id>`. Sources must identify what they actually support.
3. Add licensed imagery with author, license URL, source URL and an honest variant
   note. Do not pass off another engine or market as an exact match.
4. Define occurrence/parent/specification mappings in the exploration domain;
   register the typed drawing in `rendering/drawings/DrawingFactory.tsx`.
   Supply geometry and map selectable groups to item IDs in the scene.
   Current `compact-suv` and `classic-sedan` geometry families are illustrative.
   Replace this renderer with a licensed GLTFLoader-backed scene when verified
   separable assets become available; keep domain IDs and UI contracts unchanged.
5. Run content and browser checks. Invalid or dangling references fail loudly.

## Feature quality

`.github/workflows/verify-30s.yml` runs lint (including cognitive complexity <=15),
unit tests, type-check/build, browser tests and independent-feature build checks
on every `feat/30s-*` push, and on
web changes in PRs targeting `sub/30s`. It does not publish or deploy.
Browser evidence and failure traces are uploaded even when checks fail.
This workflow takes effect on matching published feature branches.
Required branch-protection checks must be configured separately.

The reviewed screenshot baselines use Windows Chromium/SwiftShader, fixed views,
reduced motion and deterministic Arial fonts. The Windows CI job matches them.
To intentionally update baselines, run `npm run test:browser -- tests/visual.spec.ts
--update-snapshots`, inspect the images, then rerun without that flag. CI never
updates them. Use `--output` to keep local captures outside the repository;
only reviewed comparison baselines live in `tests/visual.spec.ts-snapshots`.
Rendering budgets and known dependency/performance limitations are in the plan.

## Accuracy and rights

The user approved clearly labeled schematic 3D cars and cutaways for version 1.
They are **not OEM models, dimensional blueprints, option decoders or repair guides**.
Engine internals, panel hinges, part positions and brake drawings are approximate.
The page labels this permanently, and the help panel explains the limitations.

Books are original educational writing, not copied manufacturer manuals.
Model-specific design facts cite Toyota's Japanese launch release and GM's
historical range information kit. General checks are not OEM service procedures.
Exact intervals, fluids, torque values and pressures are deliberately not guessed.

Photograph metadata and license links live in each vehicle JSON and are displayed
beside every reference image and in the books. Toyota photos show Indonesian
2021 models, not the selected Japanese 2019 Z. The Impala engine-family photo is
a marinized 327, **not a 283**; its limitations are explicitly stated. The real
Impala exterior photo does not establish its installed engine or transmission.
Images are retained without cropping; the small-block reference is a
Wikimedia-generated 1280px thumbnail. Other photographs are original downloads.
`scripts/fetch-media.sh` documents reproducible origin downloads.

Freely licensed Sketchfab exterior candidates were researched, but authenticated
downloads, geometric fidelity and separable maintenance parts were not established.
No Sketchfab assets, manufacturer press photos or scanned manuals are bundled.
Fonts request Google Fonts when available; local sans-serif fallbacks keep the
interface functional offline.

## Deployment ownership

Application, content, Caddy fragment and static deployment script belong to
`sub/30s`. `.github/workflows/deploy-30s.yml` belongs to **main**, where it can be
manually triggered. It explicitly checks out and deploys `sub/30s`, builds with
`npm ci`, validates, then transfers the built frontend and this branch's deploy
script to the existing `hostkey-server` environment.

The script installs versioned static releases under `/srv/30s/releases`, switches
`/srv/30s/current` atomically, validates and reloads Caddy, and restores the
previous site and symlink on failure. `/` serves the SPA. Actual assets and books
are file-served. Unknown paths return 404, never the application shell.

Required existing environment secrets: `VPS_HOST`, `VPS_USER`, `VPS_SSH_KEY`,
`VPS_KNOWN_HOSTS`; `VPS_PORT` defaults to 22. The deploy user must have
passwordless sudo for the deployment operations. The shared Caddyfile must import
`/etc/caddy/sites/*.caddy`; this deployment never rewrites other sites.
DNS for `30s.eldervibe.dev` must point at that VPS for HTTPS provisioning.
