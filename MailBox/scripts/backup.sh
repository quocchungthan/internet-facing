#!/usr/bin/env bash
#
# Backup the MailBox stack. Run on the VPS from the deploy directory:
#
#   cd /opt/shuneo-mail-mvp && bash scripts/backup.sh
#
# Environment:
#   BACKUP_KEEP        number of archives to keep in backups/ (default 7)
#   BACKUP_PASSPHRASE  when set, the archive is GPG symmetric-encrypted (AES256)
#                      and the plaintext .tar.gz is removed
#
# Cron (root crontab, keeps the passphrase out of the command line via a 0600 file):
#   23 4 * * * cd /opt/shuneo-mail-mvp && BACKUP_KEEP=7 BACKUP_PASSPHRASE="$(cat /root/.mailbox-backup-pass)" bash scripts/backup.sh >> /var/log/shuneo-mail-backup.log 2>&1
#
set -euo pipefail

BACKUP_KEEP="${BACKUP_KEEP:-7}"
BACKUP_PASSPHRASE="${BACKUP_PASSPHRASE:-}"

die() {
  echo "ERROR: $*" >&2
  exit 1
}

# 1. Must run inside a real deploy directory --------------------------------
if [[ ! -f compose.yaml || ! -f .env ]]; then
  die "run this from the deploy directory (the one containing compose.yaml and .env), e.g. 'cd /opt/shuneo-mail-mvp && bash scripts/backup.sh'. Current directory: $PWD"
fi
[[ -f scripts/manage.py ]] || die "scripts/manage.py not found in $PWD; the deploy directory is incomplete."
command -v python3 >/dev/null 2>&1 || die "python3 is not installed on this host."
if [[ ! "$BACKUP_KEEP" =~ ^[0-9]+$ ]] || [[ "$BACKUP_KEEP" -lt 1 ]]; then
  die "BACKUP_KEEP must be a positive integer, got '$BACKUP_KEEP'."
fi
if [[ -n "$BACKUP_PASSPHRASE" ]] && ! command -v gpg >/dev/null 2>&1; then
  die "BACKUP_PASSPHRASE is set but gpg is not installed. Install it ('apt-get install -y gnupg') or unset BACKUP_PASSPHRASE. Refusing to produce an unencrypted archive silently."
fi

mkdir -p backups

# 2. Create the archive ------------------------------------------------------
# manage.py owns the stop/tar/start sequence; do not duplicate it here.
echo "Creating backup (services stop briefly)..."
python3 scripts/manage.py backup

archive="$(ls -1t backups/*.tar.gz 2>/dev/null | head -n1 || true)"
[[ -n "$archive" ]] || die "manage.py backup did not produce an archive in backups/."

# 3. Optional encryption -----------------------------------------------------
encrypted=no
if [[ -n "$BACKUP_PASSPHRASE" ]]; then
  echo "Encrypting archive with GPG (AES256)..."
  if ! printf '%s' "$BACKUP_PASSPHRASE" | gpg --batch --yes --quiet \
      --symmetric --cipher-algo AES256 --passphrase-fd 0 \
      --output "${archive}.gpg" "$archive"; then
    die "gpg encryption failed; the plaintext archive '$archive' was left in place. Encrypt or remove it manually."
  fi
  chmod 600 "${archive}.gpg"
  rm -f "$archive"
  archive="${archive}.gpg"
  encrypted=yes
fi

size="$(du -h "$archive" | cut -f1)"
checksum="$(sha256sum "$archive" | cut -d' ' -f1)"

# 4. Retention pruning -------------------------------------------------------
pruned=0
mapfile -t all < <(ls -1t backups/*.tar.gz backups/*.tar.gz.gpg 2>/dev/null || true)
if [[ "${#all[@]}" -gt "$BACKUP_KEEP" ]]; then
  for old in "${all[@]:$BACKUP_KEEP}"; do
    rm -f -- "$old"
    echo "Pruned: $old"
    pruned=$((pruned + 1))
  done
fi

# 5. Report (machine-readable prefix: BACKUP_<key>=) -------------------------
echo "BACKUP_ARCHIVE=$archive"
echo "BACKUP_NAME=$(basename "$archive")"
echo "BACKUP_SIZE=$size"
echo "BACKUP_SHA256=$checksum"
echo "BACKUP_ENCRYPTED=$encrypted"
echo "BACKUP_PRUNED=$pruned"
echo "BACKUP_KEPT=$(ls -1 backups/*.tar.gz backups/*.tar.gz.gpg 2>/dev/null | wc -l)"
if [[ "$encrypted" == "no" ]]; then
  echo "WARNING: archive is NOT encrypted and contains DKIM/TLS private keys and account passwords. Do not copy it off-host unencrypted." >&2
fi
