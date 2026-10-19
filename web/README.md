# 30s — the everyday machine, explained

A static, root-only React + Three.js car-maintenance learning workshop.
The name refers to being in your **thirties**, not thirty seconds.
The full-viewport scene is always the primary surface. Panels scroll internally.
The first two learning configurations are the 2019 Japanese Toyota Raize Z
1.0 turbo 2WD D-CVT and a 1967 US Chevrolet Impala Sport Sedan with a 283 V8 and
Powerglide. Real-world vehicle identity must be verified separately.

## Local development

Use Node 22 LTS (Node 20.19+ also works with the current Vite release).

```bash
cd web
npm ci
npm run dev
npm test
npm run build
npx playwright install chromium
npm run test:browser
bash scripts/test-caddy.sh
```

`dist/` is the only deployable application output. There is no Node server,
API, database or runtime Docker container. Caddy serves files directly.
`/?car=raize&part=engine` is a shareable state, not a nested route.
The car and part participate in browser history; view settings are local UI state.
The optional Caddy integration check uses Docker and a temporary local listener
on port 4180; it stops its container when finished. It exercises the production
route configuration with TLS disabled only in the isolated test configuration.

## Responsibilities

| Directory | Responsibility |
| --- | --- |
| `src/domain` | Runtime-validated vehicle/part contracts, referential integrity and URL selection |
| `src/content` | Canonical JSON vehicle catalog, facts, source scopes, photo credits and part relationships |
| `src/hooks` | Workshop navigation and interaction state; reduced-motion preference |
| `src/scene` | Procedural car geometry, Three.js rendering, orbit controls and PNG capture |
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
4. Supply geometry and map selectable groups to catalog IDs in the scene.
   Current `compact-suv` and `classic-sedan` geometry families are illustrative.
   Replace this renderer with a licensed GLTFLoader-backed scene when verified
   separable assets become available; keep domain IDs and UI contracts unchanged.
5. Run content and browser checks. Invalid or dangling references fail loudly.

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
