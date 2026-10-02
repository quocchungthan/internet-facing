#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"
FARM_DIR="$ROOT_DIR/Farm"

echo "Compiling Farm SCSS design suite..."
npx -y sass \
  --silence-deprecation=import \
  --no-source-map \
  "$FARM_DIR/scss/shared/variables.scss:$FARM_DIR/wwwroot/css/shared/variables.css" \
  "$FARM_DIR/scss/shared/base.scss:$FARM_DIR/wwwroot/css/shared/base.css" \
  "$FARM_DIR/scss/shared/tokens.scss:$FARM_DIR/wwwroot/css/shared/tokens.css" \
  "$FARM_DIR/scss/shared/identity.scss:$FARM_DIR/wwwroot/css/shared/identity.css" \
  "$FARM_DIR/scss/shared/mail.scss:$FARM_DIR/wwwroot/css/shared/mail.css" \
  "$FARM_DIR/scss/site.scss:$FARM_DIR/wwwroot/css/site.css"

echo "SCSS compilation complete."
