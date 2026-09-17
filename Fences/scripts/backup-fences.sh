#!/usr/bin/env bash
set -Eeuo pipefail

: "${FENCES_BACKUP_KEEP:=7}"
: "${FENCES_BACKUP_DIR:=/var/backups/fences}"

if [[ "${EUID}" -ne 0 ]]; then
  echo "Fences backup must run as root." >&2
  exit 1
fi
if [[ ! "$FENCES_BACKUP_KEEP" =~ ^[0-9]+$ ]] || [[ "$FENCES_BACKUP_KEEP" -lt 1 ]]; then
  echo "FENCES_BACKUP_KEEP must be a positive integer, got '$FENCES_BACKUP_KEEP'." >&2
  exit 1
fi
for path in /etc/shuneo/fences.env /etc/shuneo/fences-secrets /var/lib/fences; do
  [[ -e "$path" ]] || { echo "Required Fences path is missing: $path" >&2; exit 1; }
done
command -v docker >/dev/null 2>&1 || { echo "docker is required for a consistent Fences backup." >&2; exit 1; }

install -d -o root -g root -m 700 -- "$FENCES_BACKUP_DIR"
archive_path="$FENCES_BACKUP_DIR/fences-$(date -u +%Y%m%dT%H%M%SZ).tar.gz"
container_was_running=false
if [[ "$(docker inspect --format '{{.State.Running}}' shuneo-fences 2>/dev/null || true)" == "true" ]]; then
  container_was_running=true
  docker stop shuneo-fences >/dev/null
fi
restart_container() {
  if [[ "$container_was_running" == "true" ]]; then
    docker start shuneo-fences >/dev/null
  fi
}
trap restart_container EXIT

tar -czf "$archive_path" -C / etc/shuneo/fences.env etc/shuneo/fences-secrets var/lib/fences
chmod 600 "$archive_path"

size="$(du -h "$archive_path" | cut -f1)"
checksum="$(sha256sum "$archive_path" | cut -d' ' -f1)"
mapfile -t all < <(ls -1t "$FENCES_BACKUP_DIR"/fences-*.tar.gz 2>/dev/null || true)
pruned=0
if [[ "${#all[@]}" -gt "$FENCES_BACKUP_KEEP" ]]; then
  for old in "${all[@]:$FENCES_BACKUP_KEEP}"; do
    rm -f -- "$old"
    pruned=$((pruned + 1))
  done
fi

echo "FENCES_BACKUP_ARCHIVE=$archive_path"
echo "FENCES_BACKUP_NAME=$(basename "$archive_path")"
echo "FENCES_BACKUP_SIZE=$size"
echo "FENCES_BACKUP_SHA256=$checksum"
echo "FENCES_BACKUP_PRUNED=$pruned"
echo "FENCES_BACKUP_KEPT=$(ls -1 "$FENCES_BACKUP_DIR"/fences-*.tar.gz 2>/dev/null | wc -l)"
