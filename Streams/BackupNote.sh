#!/usr/bin/env bash
set -Eeuo pipefail

: "${AFFINE_DEPLOY_DIR:=/opt/affine-note}"
: "${AFFINE_BACKUP_KEEP:=7}"

if [[ ! -d "$AFFINE_DEPLOY_DIR" ]]; then
  echo "AFFINE_DEPLOY_DIR does not exist: $AFFINE_DEPLOY_DIR" >&2
  exit 1
fi
if [[ ! "$AFFINE_BACKUP_KEEP" =~ ^[0-9]+$ ]] || [[ "$AFFINE_BACKUP_KEEP" -lt 1 ]]; then
  echo "AFFINE_BACKUP_KEEP must be a positive integer, got '$AFFINE_BACKUP_KEEP'" >&2
  exit 1
fi

mkdir -p "$AFFINE_DEPLOY_DIR/backups"
backup_base="$AFFINE_DEPLOY_DIR/backups/affine-$(date -u +%Y%m%dT%H%M%SZ)"
archive_path="${backup_base}.tar.gz"

cd "$AFFINE_DEPLOY_DIR"

tar -czf "$archive_path" \
  --exclude='backups' \
  --exclude='.git' \
  .

size="$(du -h "$archive_path" | cut -f1)"
checksum="$(sha256sum "$archive_path" | cut -d' ' -f1)"

mapfile -t all < <(ls -1t "$AFFINE_DEPLOY_DIR"/backups/*.tar.gz 2>/dev/null || true)
pruned=0
if [[ "${#all[@]}" -gt "$AFFINE_BACKUP_KEEP" ]]; then
  for old in "${all[@]:$AFFINE_BACKUP_KEEP}"; do
    rm -f -- "$old"
    pruned=$((pruned + 1))
  done
fi

echo "AFFINE_BACKUP_ARCHIVE=$archive_path"
echo "AFFINE_BACKUP_NAME=$(basename "$archive_path")"
echo "AFFINE_BACKUP_SIZE=$size"
echo "AFFINE_BACKUP_SHA256=$checksum"
echo "AFFINE_BACKUP_PRUNED=$pruned"
echo "AFFINE_BACKUP_KEPT=$(ls -1 "$AFFINE_DEPLOY_DIR"/backups/*.tar.gz 2>/dev/null | wc -l)"
