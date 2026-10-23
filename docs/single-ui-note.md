# AFFiNE skin on `sub/note`

The native application remains the pinned `Streams/DaHang` submodule.
`Streams/DaHangCustom.sh` still owns its original Compose deployment and Caddy
path. No Angular app replaces AFFiNE.

Run **Apply Note Style** on `sub/note` after configuring `hostkey-server`:
`SINGLE_UI_TOKEN` with producer Contents:read and Actions:read, and variables
`SINGLE_UI_RUN_ID`, `SINGLE_UI_REVISION`, and the existing `AFFINE_DEPLOY_DIR`.
Select a successful trusted main-branch SingleUI run; its `single-ui-css`
artifact supplies hashed CSS, font licenses, a pinned AFFiNE base digest,
the derived-image Dockerfile, installer and `INSTALL.md`.

The workflow verifies every artifact file, builds the original AFFiNE image
with one same-origin stylesheet link in its six HTML entries, and transfers
that image over SSH. `Streams/ApplyNoteStyle.sh` refuses to change the running
backend version: the deployed native image (or previous skinned image) must
match the artifact's base digest. A mismatch requires validating the adapter
against the actual deployed version, not silently upgrading AFFiNE or its
database.

Docker may record a multi-platform index while the artifact pins its exact
platform manifest. In that case the guard pulls the immutable pinned reference
without starting it and compares canonical image IDs. Only identical native
image bytes pass; genuinely different versions are still refused.

Only the `affine` service is recreated, with its existing volumes/environment.
The script does not run migrations, rebuild application bundles, change CSP,
rewrite proxy HTML, change authentication or restart the database. It verifies
the served CSS hash and restores the previous image on readiness failure.
`single-ui.compose.yml.previous` is the rollback override. The normal
**Deploy Note** path preserves the installed override on later deployments.
Inspect light/dark desktop/mobile screenshots and native editing on staging
before production promotion; source integration alone is not a live install.
