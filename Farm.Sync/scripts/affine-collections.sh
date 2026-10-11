#!/usr/bin/env bash
# Backup / restore the AFFiNE workspace documents (snapshots + updates) of a
# self-hosted test instance running in the affine_postgres container.
#
#   scripts/affine-collections.sh backup  [file.sql]
#   scripts/affine-collections.sh restore  file.sql   # replaces the workspace's documents
#
# Restore deletes and re-inserts every snapshots/updates row of the workspace
# in one transaction. Stop the AFFiNE server first so it cannot overwrite the
# restored documents from its in-memory state:  docker stop affine_server
# and start it afterwards:                      docker start affine_server
set -euo pipefail

CONTAINER="${AFFINE_POSTGRES_CONTAINER:-affine_postgres}"
WORKSPACE="${AFFINE_WORKSPACE_ID:-da7da136-e099-4224-b64d-4fccd115eb89}"
BACKUP_DIR="${AFFINE_BACKUP_DIR:-$HOME/Documents/affine-backups}"
psql_affine() { docker exec -i "$CONTAINER" psql -U affine -d affine -v ON_ERROR_STOP=1 "$@"; }

backup() {
  mkdir -p "$BACKUP_DIR"
  local out="${1:-$BACKUP_DIR/affine-$(date -u +%Y%m%dT%H%M%SZ).sql}"
  {
    echo "-- AFFiNE workspace $WORKSPACE backup $(date -u +%FT%TZ)"
    echo "BEGIN;"
    echo "DELETE FROM updates WHERE workspace_id = '$WORKSPACE';"
    echo "DELETE FROM snapshots WHERE workspace_id = '$WORKSPACE';"
    psql_affine -At -c "
      SELECT format('INSERT INTO snapshots (guid, workspace_id, blob, created_at, updated_at, state, created_by, updated_by) VALUES (%L, %L, decode(%L, ''hex''), %L, %L, %s, %L, %L);',
        guid, workspace_id, encode(blob, 'hex'), created_at, updated_at,
        CASE WHEN state IS NULL THEN 'NULL' ELSE format('decode(%L, ''hex'')', encode(state, 'hex')) END,
        created_by, updated_by)
      FROM snapshots WHERE workspace_id = '$WORKSPACE' ORDER BY guid;"
    psql_affine -At -c "
      SELECT format('INSERT INTO updates (guid, workspace_id, blob, created_at, created_by) VALUES (%L, %L, decode(%L, ''hex''), %L, %L);',
        guid, workspace_id, encode(blob, 'hex'), created_at, created_by)
      FROM updates WHERE workspace_id = '$WORKSPACE' ORDER BY created_at;"
    echo "COMMIT;"
  } > "$out"
  echo "Backup written: $out ($(grep -c '^INSERT INTO snapshots' "$out") snapshots, $(grep -c '^INSERT INTO updates' "$out") updates)"
}

restore() {
  local file="${1:?usage: restore <file.sql>}"
  [ -f "$file" ] || { echo "No such file: $file" >&2; exit 1; }
  psql_affine < "$file"
  echo "Restored $file. Restart AFFiNE: docker restart affine_server"
}

case "${1:-}" in
  backup) backup "${2:-}" ;;
  restore) restore "${2:-}" ;;
  *) echo "usage: $0 backup [file] | restore file" >&2; exit 2 ;;
esac
