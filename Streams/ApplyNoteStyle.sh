#!/usr/bin/env bash
set -Eeuo pipefail

: "${AFFINE_DEPLOY_DIR:?Set the existing AFFiNE deployment directory}"
: "${SINGLE_UI_STYLE_DIR:?Set the verified artifact directory}"
: "${SINGLE_UI_REVISION:?Set the pinned producer revision}"
: "${SINGLE_UI_IMAGE_ARCHIVE:?Set the candidate image archive}"
: "${SINGLE_UI_CSS_SHA:?Set the verified affine.css hash}"
: "${AFFINE_UPSTREAM_PORT:=3010}"
[[ "$AFFINE_DEPLOY_DIR" == /* && "$AFFINE_DEPLOY_DIR" != / ]] || exit 2
[[ "$SINGLE_UI_REVISION" =~ ^[a-f0-9]{40}$ && "$AFFINE_UPSTREAM_PORT" =~ ^[0-9]+$ ]] || exit 2
[[ "$SINGLE_UI_CSS_SHA" =~ ^[a-f0-9]{64}$ ]] || exit 2
test -f "$AFFINE_DEPLOY_DIR/compose.yml"
base="$(tr -d '\r\n' < "$SINGLE_UI_STYLE_DIR/affine-image.txt")"
[[ "$base" =~ ^ghcr.io/toeverything/affine@sha256:[a-f0-9]{64}$ ]] || {
  echo "The verified artifact must pin an inspected AFFiNE image digest." >&2; exit 1;
}
cd "$AFFINE_DEPLOY_DIR"
exec 8>single-ui.deploy.lock
flock -n 8 || { echo "Another AFFiNE style deployment is running." >&2; exit 1; }
compose=(docker compose -f compose.yml)
override=single-ui.compose.yml
if [[ -f "$override" ]]; then compose+=(-f "$override"); fi
container="$("${compose[@]}" ps -q affine)"
test -n "$container" || { echo "Start the native AFFiNE deployment before applying its skin." >&2; exit 1; }
current="$(docker inspect --format '{{.Image}}' "$container")"
installed_base="$(docker image inspect --format '{{index .Config.Labels "dev.eldervibe.single-ui.affine-base"}}' "$current")"
if [[ "$installed_base" != "$base" ]] &&
  ! docker image inspect --format '{{range .RepoDigests}}{{println .}}{{end}}' "$current" | grep -Fxq "$base"; then
  echo "AFFiNE version mismatch. This style operation will not upgrade the backend or migrate its database." >&2
  echo "Validate a skin against the deployed AFFiNE digest before retrying." >&2
  exit 1
fi
image="eldervibe-affine-ui:$SINGLE_UI_REVISION"
docker load --input "$SINGLE_UI_IMAGE_ARCHIVE"
test "$(docker image inspect --format '{{index .Config.Labels "dev.eldervibe.single-ui.affine-base"}}' "$image")" = "$base"
test "$(docker image inspect --format '{{index .Config.Labels "dev.eldervibe.single-ui.revision"}}' "$image")" = "$SINGLE_UI_REVISION"
expected="$SINGLE_UI_CSS_SHA"
previous="$override.previous"
if [[ -f "$override" ]]; then cp -- "$override" "$previous"; else
  printf 'services:\n  affine:\n    image: "%s"\n' "$current" > "$previous"
fi
printf 'services:\n  affine:\n    image: "%s"\n  affine_migration:\n    image: "%s"\n' "$image" "$base" > "$override"
compose=(docker compose -f compose.yml -f "$override")
rollback() {
  cp -- "$previous" "$override"
  "${compose[@]}" up -d --no-deps affine
}
if ! "${compose[@]}" up -d --no-deps affine; then rollback; exit 1; fi
for attempt in {1..60}; do
  if actual="$(curl --fail --silent --show-error --max-time 5 "http://127.0.0.1:$AFFINE_UPSTREAM_PORT/single-ui.css" | sha256sum | cut -d ' ' -f 1)" &&
    [[ "$actual" == "$expected" ]]; then
    echo "Verified AFFiNE stylesheet $expected. Native volumes, database, CSP and bundles are unchanged."
    echo "Rollback: cp $previous $override && docker compose -f compose.yml -f $override up -d --no-deps affine"
    exit 0
  fi
  sleep 2
done
echo "AFFiNE skin readiness failed; restoring the previous image." >&2
rollback
exit 1
