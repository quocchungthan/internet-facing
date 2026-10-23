# Farm static frontend boundary

Farm keeps its existing controllers, database, API diagnostics/rate limits,
Fences auth, `/api/*`, `/auth/*` and `/signin-oidc`. The owned frontend is now
the verified **app-farm** bundle from `quocchungthan/EMFc-experiments`, not a
new build of Farm.Microsoft/Farm.HangingPost. Original source projects remain
available for reference, but are not part of the web image build.

`/`, `/Home/Index`, `/connect`, `/winchester`, `/tracker`, `/brick` and `/bricks`
keep their URLs and serve the same shared-system app; Winchester is no longer
an independent visual theme. `/connect` retains its dedicated account flow.
Existing privacy/error pages and all backend routes remain native.
Static bundle files live in `Farm/frontend/`, outside old `wwwroot` assets.
Publish includes that folder; missing bundles fail publishing explicitly.
API-only development can still start, returning a clear 503 for frontend routes.

Set repository/environment variables `SINGLE_UI_RUN_ID` and `SINGLE_UI_REVISION`
to a successful main-branch **SingleUI apps and skins** run and its full SHA.
Set server-only secret `SINGLE_UI_TOKEN` with Actions:read and Contents:read
on the producer. CI downloads **single-ui-apps**, checks run provenance and
app/revision/file hashes, and stages `farm` before tests, publish and Docker.
No GitHub token is included in the browser or Docker build.

For local import, check out EMFc at the pinned SHA and run its dependency-free
`SingleUI/deployment/artifacts/import-app.mjs farm EMPTY_ABSOLUTE_FARM_FRONTEND`
using Node 22 and authenticated `gh`, with the same run/SHA environment variables.
Artifact retention is 90 days; save a verified operator copy for longer rollback.
Promote the backend/frontend image pair only after staging real login, data,
click tracking, account/key workflows and API-console headers. Unit/browser
fixtures do not prove production integration. Roll back the previous image and
pin, never the database or persistent key ring.
