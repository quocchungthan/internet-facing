#!/usr/bin/env bash
set -Eeuo pipefail

: "${STORAGE_BASE_DIR:=/srv/storage}"
: "${BACKUP_DIR:=$STORAGE_BASE_DIR/backups}"
: "${RETENTION_DAYS:=14}"
: "${MYSQL_CONTAINER:=seafile-mysql}"
: "${SEAFILE_CONTAINER:=seafile-server}"

TIMESTAMP=$(date +"%Y%m%d_%H%M%S")
BACKUP_NAME="seafile_backup_${TIMESTAMP}"
TARGET_DIR="${BACKUP_DIR}/${BACKUP_NAME}"

echo "==> Starting Seafile storage backup: ${BACKUP_NAME}"
mkdir -p "${TARGET_DIR}"

# 1. Backup MariaDB Databases
echo "--> Dumping MariaDB databases..."
docker exec "${MYSQL_CONTAINER}" sh -c 'exec mariadb-dump --all-databases -u root -p"${MYSQL_ROOT_PASSWORD}"' > "${TARGET_DIR}/all_databases.sql"

# 2. Backup Seafile Configurations and Data
echo "--> Archiving Seafile configuration files..."
if [[ -d "${STORAGE_BASE_DIR}/data/seafile/conf" ]]; then
    tar -czf "${TARGET_DIR}/seafile_conf.tar.gz" -C "${STORAGE_BASE_DIR}/data/seafile" conf
elif [[ -d "${STORAGE_BASE_DIR}/data" ]]; then
    tar -czf "${TARGET_DIR}/seafile_conf.tar.gz" -C "${STORAGE_BASE_DIR}" data --exclude="data/seafile/seafile-data/storage" || true
fi

# 3. Create single compressed archive
echo "--> Compressing backup bundle..."
cd "${BACKUP_DIR}"
tar -czf "${BACKUP_NAME}.tar.gz" "${BACKUP_NAME}"
rm -rf "${TARGET_DIR}"

# 4. Clean up old backups based on retention policy
echo "--> Cleaning up backups older than ${RETENTION_DAYS} days..."
find "${BACKUP_DIR}" -name "seafile_backup_*.tar.gz" -type f -mtime +"${RETENTION_DAYS}" -exec rm -f {} +

BACKUP_FILE="${BACKUP_DIR}/${BACKUP_NAME}.tar.gz"
BACKUP_SIZE=$(du -h "${BACKUP_FILE}" | cut -f1)

echo "==> Backup completed successfully: ${BACKUP_FILE} (${BACKUP_SIZE})"
echo "BACKUP_FILE=${BACKUP_FILE}"
echo "BACKUP_SIZE=${BACKUP_SIZE}"
