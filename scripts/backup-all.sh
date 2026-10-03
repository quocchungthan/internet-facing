#!/usr/bin/env bash
# Backs up every eldervibe service on the VPS into ONE archive with a RESTORE.md at its root.
# Run as root by .github/workflows/backup-all.yml. Progress goes to stderr; stdout carries only BACKUP_* result lines.
# Excluded on purpose: logs, mail content (Maddy message store/queue), caches, raw DB data dirs (dumped instead).
set -euo pipefail
set +x
umask 077

: "${BACKUP_ROOT:=/var/backups/eldervibe}"
: "${BACKUP_KEEP:=7}"
: "${BACKUP_GIT_SHA:=unknown}"
: "${MAILBOX_DEPLOY_DIR:=/opt/shuneo-mail-mvp}"
: "${STORAGE_DEPLOY_DIR:=/srv/storage}"
: "${AFFINE_DEPLOY_DIR:=/opt/affine-note}"

if [[ -n "${FENCES_DEPLOY_DIR:-}" ]]; then
	[[ "$FENCES_DEPLOY_DIR" =~ ^/[A-Za-z0-9_-][A-Za-z0-9._-]*(/[A-Za-z0-9_-][A-Za-z0-9._-]*)*$ ]] || { echo 'FENCES_DEPLOY_DIR must be an absolute path without dot or parent segments.' >&2; exit 1; }
	FENCES_CONFIG_DIR="$FENCES_DEPLOY_DIR"
	FENCES_DATA_DIR="$FENCES_DEPLOY_DIR/data"
else
	FENCES_CONFIG_DIR=/etc/shuneo
	FENCES_DATA_DIR=/var/lib/fences
fi
FENCES_CONTAINER=shuneo-fences
FARM_CONTAINER=eldervibe-farm
FARM_STATIC_ROOT=/var/lib/caddy/farm/hanging-post
SEAFILE_CONTAINER=seafile-server
SEAFILE_DB_CONTAINER=seafile-mysql
AFFINE_CONTAINER=affine_server
AFFINE_DB_CONTAINER=affine_postgres

log() { printf '%s\n' "$*" >&2; }
warn() { printf '::warning::%s\n' "$*" >&2; }
die() { printf '::error::%s\n' "$*" >&2; exit 1; }
have() { command -v "$1" >/dev/null 2>&1; }

if ! have sqlite3; then
	if have apt-get; then
		export DEBIAN_FRONTEND=noninteractive
		apt-get update -qq >/dev/null 2>&1 && apt-get install -y -qq --no-install-recommends sqlite3 >/dev/null 2>&1 || true
	fi
fi
if ! have sqlite3 && have python3; then
	sqlite3() {
		local db="$1" cmd="$2"
		python3 -c '
import os, re, sqlite3, sys
db_path, cmd = sys.argv[1], sys.argv[2]
m = re.match(r"^\.backup\s+[\x27\"]?([^\x27\"]+)[\x27\"]?$", cmd)
if not m:
    sys.exit(1)
dest_path = m.group(1)
src = sqlite3.connect(f"file:{os.path.abspath(db_path)}?mode=ro", uri=True)
dst = sqlite3.connect(dest_path)
src.backup(dst)
dst.close()
src.close()
' "$db" "$cmd"
	}
fi

[[ "$EUID" -eq 0 ]] || die "backup-all.sh must run as root (VPS_USER=root or passwordless sudo)."
[[ "$BACKUP_KEEP" =~ ^[1-9][0-9]*$ ]] || die "BACKUP_KEEP must be a positive integer."
for path_var in BACKUP_ROOT MAILBOX_DEPLOY_DIR STORAGE_DEPLOY_DIR AFFINE_DEPLOY_DIR; do
	[[ "${!path_var}" =~ ^/[A-Za-z0-9._/-]+$ && "${!path_var}" != "/" ]] || die "$path_var must be an absolute path."
done
if [[ -n "${FENCES_DEPLOY_DIR:-}" && ( "$FENCES_DEPLOY_DIR/" == "$BACKUP_ROOT/"* || "$BACKUP_ROOT/" == "$FENCES_DEPLOY_DIR/"* ) ]]; then
	die "FENCES_DEPLOY_DIR and BACKUP_ROOT must not overlap."
fi
[[ "$BACKUP_GIT_SHA" =~ ^[A-Za-z0-9]+$ ]] || BACKUP_GIT_SHA=unknown

timestamp="$(date -u +%Y%m%dT%H%M%SZ)"
iso_time="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
host_name="$(hostname -f 2>/dev/null || hostname)"

install -d -o root -g root -m 700 "$BACKUP_ROOT"
# A killed run cannot clean up through the trap; drop staging dirs older than a day.
find "$BACKUP_ROOT" -mindepth 1 -maxdepth 1 -type d -name 'staging-*' -mmin +1440 -exec rm -rf {} + 2>/dev/null || true
staging="$BACKUP_ROOT/staging-$timestamp"
install -d -o root -g root -m 700 "$staging" "$staging/files" "$staging/dumps" "$staging/meta"
cleanup() { rm -rf -- "$staging"; }
trap cleanup EXIT

manifest="$staging/MANIFEST.tsv"
printf 'id\tservice\tstatus\tsource\tzip_path\towner\tmode\tbytes\tnote\n' > "$manifest"
included=0
missing=0
failed=0

docker_ok=false
if have docker && docker info >/dev/null 2>&1; then
	docker_ok=true
fi
container_exists() { [[ "$docker_ok" == true ]] && docker container inspect "$1" >/dev/null 2>&1; }
container_running() { [[ "$docker_ok" == true && "$(docker inspect -f '{{.State.Running}}' "$1" 2>/dev/null)" == true ]]; }
mount_source() {
	[[ "$docker_ok" == true ]] || return 0
	docker inspect -f "{{range .Mounts}}{{if eq .Destination \"$2\"}}{{.Source}}{{end}}{{end}}" "$1" 2>/dev/null || true
}

# record <id> <service> <status: included|missing|failed|absent> <source> <zip path or -> <note>
record() {
	local id=$1 service=$2 status=$3 src=$4 zpath=$5 note=${6:-}
	local owner=- mode=- bytes=-
	if [[ "$zpath" != - && -e "$staging/$zpath" ]]; then
		owner="$(stat -c '%U:%G' -- "$staging/$zpath")"
		mode="$(stat -c '%a' -- "$staging/$zpath")"
		bytes="$(du -sb -- "$staging/$zpath" | cut -f1)"
	fi
	printf '%s\t%s\t%s\t%s\t%s\t%s\t%s\t%s\t%s\n' "$id" "$service" "$status" "$src" "$zpath" "$owner" "$mode" "$bytes" "$note" >> "$manifest"
	case "$status" in
		included) included=$((included + 1)) ;;
		missing) missing=$((missing + 1)); warn "$service/$id missing: $src ($note)" ;;
		failed) failed=$((failed + 1)); warn "$service/$id FAILED: $src ($note)" ;;
	esac
}

free_bytes() { df -PB1 "$staging" | awk 'NR==2 {print $4}'; }

# copy_path <id> <service> <required|optional> <absolute path> [extra tar --exclude args...]
copy_path() {
	local id=$1 service=$2 need=$3 src=$4
	shift 4
	local rel="${src#/}"
	if [[ ! -e "$src" && ! -L "$src" ]]; then
		if [[ "$need" == optional ]]; then
			record "$id" "$service" absent "$src" - "optional, not present"
		else
			record "$id" "$service" missing "$src" - "path not present"
		fi
		return 0
	fi
	local src_bytes
	src_bytes="$(du -sb -- "$src" 2>/dev/null | cut -f1)"
	if (( ${src_bytes:-0} * 11 / 10 > $(free_bytes) )); then
		record "$id" "$service" failed "$src" - "not enough free disk for staging (${src_bytes} bytes)"
		return 0
	fi
	log "copy $service/$id <- $src"
	set +e
	tar -C / --numeric-owner -cpf - \
		--exclude='*.log' --exclude='*.log.[0-9]*' --exclude='logs' --exclude='*.pid' \
		"$@" "$rel" | tar -C "$staging/files" --numeric-owner --same-owner -xpf -
	local rc_src=${PIPESTATUS[0]} rc_dst=${PIPESTATUS[1]}
	set -e
	# GNU tar exits 1 when a live file changed while it was read; the copy is still usable.
	if (( rc_dst != 0 || rc_src > 1 )); then
		record "$id" "$service" failed "$src" "files/$rel" "tar exit $rc_src/$rc_dst"
	elif (( rc_src == 1 )); then
		record "$id" "$service" included "$src" "files/$rel" "some files changed while being copied (live service)"
	else
		record "$id" "$service" included "$src" "files/$rel" "${COPY_NOTE:-}"
	fi
}

# sqlite_snapshot <id> <service> <db file>
sqlite_snapshot() {
	local id=$1 service=$2 db=$3
	local rel="${db#/}" dest="$staging/files/${db#/}"
	if [[ ! -f "$db" ]]; then
		record "$id" "$service" missing "$db" - "database file not present"
		return 0
	fi
	install -d -m 700 "$(dirname -- "$dest")"
	if have sqlite3; then
		if sqlite3 "$db" ".backup '$dest'" >/dev/null 2>&1; then
			chown --reference="$db" "$dest"
			chmod --reference="$db" "$dest"
			record "$id" "$service" included "$db" "files/$rel" "online sqlite3 .backup (consistent)"
		else
			rm -f -- "$dest"
			record "$id" "$service" failed "$db" - "sqlite3 .backup failed"
		fi
		return 0
	fi
	if [[ "$service" == fences ]]; then
		record "$id" "$service" failed "$db" - "sqlite3 is required for a consistent live Fences database backup"
		return 0
	fi
	cp -a -- "$db" "$dest"
	local suffix
	for suffix in -wal -shm; do
		if [[ -e "$db$suffix" ]]; then
			cp -a -- "$db$suffix" "$dest$suffix"
		fi
	done
	record "$id" "$service" included "$db" "files/$rel" "sqlite3 not installed: copied live with -wal/-shm while the service kept running"
}

# dump_to <id> <service> <container> <zip path> <completion marker> -- <command run inside the container>
dump_to() {
	local id=$1 service=$2 container=$3 zpath=$4 marker=$5
	shift 6
	local out="$staging/$zpath"
	if ! container_running "$container"; then
		record "$id" "$service" failed "docker:$container" - "container not running; no database dump taken"
		return 0
	fi
	log "dump $service/$id from $container"
	local err="$staging/meta/$id.stderr"
	if docker exec "$container" sh -c "$1" > "$out" 2> "$err" && [[ -s "$out" ]] && tail -n 5 "$out" | grep -q -- "$marker"; then
		rm -f -- "$err"
		record "$id" "$service" included "docker:$container" "$zpath" "logical dump"
	else
		# Dump tools never print credential values in their errors.
		tail -n 5 "$err" >&2 || true
		rm -f -- "$out" "$err"
		record "$id" "$service" failed "docker:$container" - "dump command failed or incomplete"
	fi
}

caddy_home="$(getent passwd caddy 2>/dev/null | cut -d: -f6 || true)"
caddy_home="${caddy_home:-/var/lib/caddy}"

# ---- Caddy -------------------------------------------------------------------
if [[ -d /etc/caddy ]]; then
	copy_path caddy-config caddy required /etc/caddy --exclude='.*.caddy.*'
	copy_path caddy-data caddy required "$caddy_home/.local/share/caddy" --exclude='locks'
	copy_path caddy-autosave caddy optional "$caddy_home/.config/caddy"
	copy_path caddy-env caddy optional /etc/default/caddy
	copy_path caddy-unit-override caddy optional /etc/systemd/system/caddy.service.d
else
	record caddy caddy missing /etc/caddy - "Caddy not installed"
fi

# ---- Farm (main, eldervibe.dev) ---------------------------------------------
if [[ -d "$FARM_STATIC_ROOT" ]] || container_exists "$FARM_CONTAINER"; then
	COPY_NOTE="HangingPost static release; container env file is /dev/null (stateless)" \
		copy_path farm-hanging-post farm required "$FARM_STATIC_ROOT"
else
	record farm farm missing "$FARM_STATIC_ROOT" - "Farm not deployed"
fi

# ---- Fences (sub/identity) ----------------------------------------------------
if [[ -e "$FENCES_CONFIG_DIR/fences.env" || -d "$FENCES_DATA_DIR" ]] || container_exists "$FENCES_CONTAINER"; then
	# The data directory holds SQLite files; snapshot those separately rather than copying live DB files.
	if [[ -n "${FENCES_DEPLOY_DIR:-}" ]]; then
		copy_path fences-config fences required "$FENCES_CONFIG_DIR" --exclude='data'
	else
		copy_path fences-config fences required "$FENCES_CONFIG_DIR"
	fi
	copy_path fences-data fences required "$FENCES_DATA_DIR" \
		--exclude='*.db' --exclude='*.db-wal' --exclude='*.db-shm' --exclude='*.db-journal'
	if [[ -d "$FENCES_DATA_DIR" ]]; then
		while IFS= read -r -d '' fences_db; do
			sqlite_snapshot "fences-db-$(basename -- "$fences_db" .db)" fences "$fences_db"
		done < <(find "$FENCES_DATA_DIR" -type f -name '*.db' -print0)
	fi
else
	record fences fences missing "$FENCES_CONFIG_DIR" - "Fences not installed"
fi
if [[ -n "${FENCES_DEPLOY_DIR:-}" && ( -e /etc/shuneo/fences.env || -d /var/lib/fences ) ]]; then
	warn "Legacy Fences material remains; including it separately until migration is complete."
	copy_path fences-legacy-config fences required /etc/shuneo
	copy_path fences-legacy-data fences required /var/lib/fences \
		--exclude='*.db' --exclude='*.db-wal' --exclude='*.db-shm' --exclude='*.db-journal'
	if [[ -d /var/lib/fences ]]; then
		while IFS= read -r -d '' fences_db; do
			sqlite_snapshot "fences-legacy-db-$(basename -- "$fences_db" .db)" fences "$fences_db"
		done < <(find /var/lib/fences -type f -name '*.db' -print0)
	fi
fi

# ---- MailBox / Maddy (sub/mail) ----------------------------------------------
mail_dir="$MAILBOX_DEPLOY_DIR"
if [[ -f "$mail_dir/compose.yaml" ]]; then
	copy_path mail-env mail required "$mail_dir/.env"
	copy_path mail-compose mail required "$mail_dir/compose.yaml"
	copy_path mail-secrets mail required "$mail_dir/secrets"
	copy_path maddy-config mail required "$mail_dir/maddy"
	copy_path maddy-dkim mail required "$mail_dir/runtime/mail/dkim_keys"
	copy_path mail-tls mail required "$mail_dir/runtime/letsencrypt"
	copy_path mail-ca mail optional "$mail_dir/runtime/ca"
	maddy_conf="$mail_dir/maddy/maddy.conf"
	auth_dsn=""
	storage_dsn=""
	if [[ -f "$maddy_conf" ]]; then
		auth_dsn="$(awk '/^[[:space:]]*auth\.pass_table/ {f=1} f && $1 == "dsn" {print $2; exit}' "$maddy_conf")"
		storage_dsn="$(awk '/^[[:space:]]*storage\.imapsql/ {f=1} f && $1 == "dsn" {print $2; exit}' "$maddy_conf")"
	fi
	if [[ ! "$auth_dsn" =~ ^[A-Za-z0-9._-]+$ ]]; then
		record maddy-credentials mail missing "$mail_dir/runtime/mail" - "could not read auth.pass_table dsn from maddy.conf"
	elif [[ "$auth_dsn" == "$storage_dsn" ]]; then
		record maddy-credentials mail missing "$mail_dir/runtime/mail/$auth_dsn" - "EXCLUDED: credentials share one DB with the mail store"
	else
		sqlite_snapshot maddy-credentials mail "$mail_dir/runtime/mail/$auth_dsn"
	fi
else
	record mail mail missing "$mail_dir" - "MailBox not deployed"
fi

# ---- Storage / Seafile (sub/storage) -------------------------------------------
storage_dir="$STORAGE_DEPLOY_DIR"
if [[ -f "$storage_dir/docker-compose.yml" ]] || container_exists "$SEAFILE_CONTAINER"; then
	copy_path storage-env storage required "$storage_dir/.env"
	copy_path storage-compose storage required "$storage_dir/docker-compose.yml"
	storage_shared="$(mount_source "$SEAFILE_CONTAINER" /shared)"
	storage_shared="${storage_shared:-$storage_dir/data}"
	COPY_NOTE="Seafile /shared: seafile/conf (seahub_settings.py, ccnet/seafile conf), seafile-data, seahub-data" \
		copy_path storage-shared storage required "$storage_shared"
	dump_to storage-mysql storage "$SEAFILE_DB_CONTAINER" dumps/seafile-mysql-all.sql 'Dump completed' -- \
		'set -- "$MYSQL_ROOT_PASSWORD"; d=mariadb-dump; command -v "$d" >/dev/null 2>&1 || d=mysqldump; MYSQL_PWD="$1" exec "$d" -uroot --all-databases --single-transaction --routines --triggers --events'
else
	record storage storage missing "$storage_dir" - "Seafile not deployed"
fi

# ---- Note / AFFiNE (sub/note) --------------------------------------------------
note_dir="$AFFINE_DEPLOY_DIR"
if [[ -f "$note_dir/compose.yml" ]] || container_exists "$AFFINE_CONTAINER"; then
	copy_path note-env note optional "$note_dir/.env"
	copy_path note-compose note required "$note_dir/compose.yml"
	note_config="$(mount_source "$AFFINE_CONTAINER" /root/.affine/config)"
	copy_path note-config note required "${note_config:-$note_dir/config}"
	note_blobs="$(mount_source "$AFFINE_CONTAINER" /root/.affine/storage)"
	COPY_NOTE="AFFiNE uploaded blobs" copy_path note-blobs note required "${note_blobs:-$note_dir/data/storage}"
	dump_to note-postgres note "$AFFINE_DB_CONTAINER" dumps/affine-postgres.sql 'PostgreSQL database dump complete' -- \
		'set -- "${POSTGRES_PASSWORD:-}"; PGPASSWORD="$1" exec pg_dump -U "${POSTGRES_USER:-affine}" -d "${POSTGRES_DB:-affine}" --clean --if-exists'
else
	record note note missing "$note_dir" - "AFFiNE not deployed"
fi

# ---- Metadata (no secrets: names, images and states only) ---------------------
if [[ "$docker_ok" == true ]]; then
	docker ps -a --format '{{.Names}}\t{{.Image}}\t{{.State}}' > "$staging/meta/containers.tsv" 2>/dev/null || true
fi

legacy_cron=0
legacy_pattern='scripts/backup\.sh|backup-fences\.sh|BackupNote\.sh|backup-storage\.sh'
if have crontab; then
	crontab_matches=$( (crontab -l -u root 2>/dev/null || true) | (grep -cE "$legacy_pattern" || true) )
	legacy_cron=$(( legacy_cron + ${crontab_matches:-0} ))
fi
if [[ -d /etc/cron.d ]]; then
	cron_d_matches=$( (grep -lE "$legacy_pattern" /etc/cron.d/* 2>/dev/null || true) | wc -l )
	legacy_cron=$(( legacy_cron + ${cron_d_matches:-0} ))
fi
if (( legacy_cron > 0 )); then
	warn "Found $legacy_cron cron entr(y/ies) that still call a removed backup script. Remove with: sudo crontab -l | grep -vE '$legacy_pattern' | sudo crontab -"
fi

# ---- RESTORE.md ----------------------------------------------------------------
restore_cmd() { # <zip path> <original path>
	if [[ -d "$staging/$1" && ! -L "$staging/$1" ]]; then
		printf 'rsync -a %s/ %s/' "$1" "$2"
	else
		printf 'cp -a %s %s' "$1" "$2"
	fi
}

{
	printf '# eldervibe backup - restore guide\n\n'
	printf '| Field | Value |\n|---|---|\n'
	printf '| Host | `%s` |\n' "$host_name"
	printf '| Created (UTC) | `%s` |\n' "$iso_time"
	printf '| main git SHA | `%s` |\n' "$BACKUP_GIT_SHA"
	printf '| Items included / missing / failed | %s / %s / %s |\n\n' "$included" "$missing" "$failed"
	printf 'This archive holds configuration, credentials, certificates and service data. It contains NO logs and NO mail content.\n'
	printf 'It contains private keys and passwords: keep it root-only and encrypt it before it leaves the VPS.\n\n'
	printf '## Layout\n\n'
	printf -- '- `files/<absolute path>`: copies of VPS paths, e.g. `files/etc/caddy/Caddyfile` came from `/etc/caddy/Caddyfile`.\n'
	printf -- '- `dumps/`: logical database dumps (Seafile MariaDB, AFFiNE Postgres).\n'
	printf -- '- `meta/containers.tsv`: container names, images and states at backup time.\n'
	printf -- '- `MANIFEST.tsv`: the table below in machine-readable form.\n\n'
	printf '## Unpack\n\n```bash\nsudo -i\nmkdir -p /root/restore && cd /root/restore\nunzip -X /var/backups/eldervibe/eldervibe-backup-%s.zip   # -X restores uid/gid; for .tar.gz use: tar --numeric-owner -xzpf <file>\n```\n\n' "$timestamp"
	printf '## Items\n\n'
	printf '| Service | Item | Status | Original VPS path | Path in archive | Owner | Mode | Bytes | Restore copy | Note |\n'
	printf '|---|---|---|---|---|---|---|---|---|---|\n'
	while IFS=$'\t' read -r id service status src zpath owner mode bytes note; do
		[[ "$id" == id ]] && continue
		copy_cmd=-
		if [[ "$status" == included && "$zpath" == files/* ]]; then
			copy_cmd="\`$(restore_cmd "$zpath" "$src")\`"
		elif [[ "$status" == included ]]; then
			copy_cmd="see $service section"
		fi
		printf '| %s | %s | %s | `%s` | `%s` | %s | %s | %s | %s | %s |\n' "$service" "$id" "$status" "$src" "$zpath" "$owner" "$mode" "$bytes" "$copy_cmd" "$note"
	done < "$manifest"
	printf '\nAfter copying an item back, fix its top-level owner and mode to the values above (`chown <owner> <path>; chmod <mode> <path>`). `cp -a`/`rsync -a` keep the owners of nested files.\n\n'

	printf '## Skipped or missing items\n\n'
	if awk -F'\t' 'NR > 1 && $3 != "included" {found=1} END {exit !found}' "$manifest"; then
		awk -F'\t' 'NR > 1 && $3 != "included" {printf "- %s/%s (%s): `%s` - %s\n", $2, $1, $3, $4, $9}' "$manifest"
	else
		printf 'None.\n'
	fi
	printf '\nAlways excluded: `*.log`, `logs/`, `/var/log/**`, journal, Maddy message store (`imapsql.db*`, `messages/`), mail queue (`remote_queue/`), `mtasts_cache/`, ACME webroot, raw MariaDB/Postgres data dirs (restored from the dumps), older per-service `backups/` folders, caches, docker images.\n\n'

	printf '## Restore order per service\n\n'
	printf 'Always: stop the service, copy the items back, fix owner/mode, restore the database dump, start the service.\n\n'
	printf '### Caddy (ingress, all domains)\n\n```bash\nsystemctl stop caddy\n'
	printf 'rsync -a files/etc/caddy/ /etc/caddy/\nrsync -a files%s/.local/share/caddy/ %s/.local/share/caddy/\n' "$caddy_home" "$caddy_home"
	printf '[ -d files%s/.config/caddy ] && rsync -a files%s/.config/caddy/ %s/.config/caddy/\n' "$caddy_home" "$caddy_home" "$caddy_home"
	printf '[ -e files/etc/default/caddy ] && cp -a files/etc/default/caddy /etc/default/caddy\n'
	printf '[ -d files/etc/systemd/system/caddy.service.d ] && rsync -a files/etc/systemd/system/caddy.service.d/ /etc/systemd/system/caddy.service.d/ && systemctl daemon-reload\n'
	printf 'chown -R caddy:caddy %s/.local/share/caddy\ncaddy validate --config /etc/caddy/Caddyfile --adapter caddyfile\nsystemctl start caddy\n```\n\n' "$caddy_home"
	printf '### Farm (main, eldervibe.dev)\n\nRedeploy with the `build-deploy-farm.yml` workflow (the image is rebuilt, the container is stateless). Then, if needed:\n\n'
	printf '```bash\nrsync -a files%s/ %s/\nchown -R caddy:caddy %s\n```\n\n' "$FARM_STATIC_ROOT" "$FARM_STATIC_ROOT" "$FARM_STATIC_ROOT"
	printf '### Fences (identity)\n\n```bash\ndocker stop %s\n' "$FENCES_CONTAINER"
	if [[ -n "${FENCES_DEPLOY_DIR:-}" ]]; then
		printf '# Restore to FENCES_DEPLOY_DIR=%s; set the same GitHub variable before re-deploying.\n' "$FENCES_DEPLOY_DIR"
		printf 'mkdir -p %s\n' "$FENCES_DATA_DIR"
	fi
	printf 'rsync -a files%s/ %s/\nrsync -a files%s/ %s/\n' "$FENCES_CONFIG_DIR" "$FENCES_CONFIG_DIR" "$FENCES_DATA_DIR" "$FENCES_DATA_DIR"
	printf '# identity.db came from `sqlite3 .backup` unless the note says otherwise: drop stale WAL files first\nrm -f %s/*.db-wal %s/*.db-shm\n' "$FENCES_DATA_DIR" "$FENCES_DATA_DIR"
	printf 'chown root:root %s %s/fences.env; chmod 700 %s/fences-secrets; chmod 600 %s/fences.env %s/fences-secrets/*\n' "$FENCES_CONFIG_DIR" "$FENCES_CONFIG_DIR" "$FENCES_CONFIG_DIR" "$FENCES_CONFIG_DIR" "$FENCES_CONFIG_DIR"
	printf 'chmod 700 %s\n' "$FENCES_DATA_DIR"
	if [[ -n "${FENCES_DEPLOY_DIR:-}" ]]; then
		printf '# Legacy Fences files, when present, are also in files/etc/shuneo/ and files/var/lib/fences/. Do not merge them with the configured installation; reconcile them offline before restarting.\n'
	fi
	printf 'docker start %s   # or re-run deploy-fences.yml\n```\n\n' "$FENCES_CONTAINER"
	printf '### MailBox / Maddy (mail)\n\n```bash\ncd %s\ndocker compose stop web maddy\n' "$mail_dir"
	printf 'for p in .env compose.yaml secrets maddy runtime/mail/dkim_keys runtime/letsencrypt runtime/ca; do [ -e "/root/restore/files%s/$p" ] && cp -a "/root/restore/files%s/$p" "$(dirname "%s/$p")/"; done\n' "$mail_dir" "$mail_dir" "$mail_dir"
	printf 'cp -a /root/restore/files%s/runtime/mail/%s runtime/mail/   # account passwords only\n' "$mail_dir" "${auth_dsn:-credentials.db}"
	printf 'rm -f runtime/mail/%s-wal runtime/mail/%s-shm\n' "${auth_dsn:-credentials.db}" "${auth_dsn:-credentials.db}"
	printf 'docker compose up -d\n'
	printf '# Mailboxes were NOT backed up (mail content excluded). Recreate an empty IMAP mailbox for each address in secrets/accounts.json:\n'
	printf 'docker compose exec maddy maddy imap-acct create <address>\n'
	printf '# Then re-run deploy-mailbox.yml so configure-caddy-web.sh re-grants Caddy read access to the certificates.\n```\n\n'
	printf '### Storage / Seafile\n\n```bash\ncd %s\ndocker compose down\n' "$storage_dir"
	printf 'cp -a /root/restore/files%s/.env /root/restore/files%s/docker-compose.yml ./\n' "$storage_dir" "$storage_dir"
	printf 'rsync -a /root/restore/files%s/ %s/\n' "${storage_shared:-$storage_dir/data}" "${storage_shared:-$storage_dir/data}"
	printf 'docker compose up -d db && sleep 20\n'
	printf 'docker exec -i %s sh -c '\''set -- "$MYSQL_ROOT_PASSWORD"; MYSQL_PWD="$1" exec mariadb -uroot'\'' < /root/restore/dumps/seafile-mysql-all.sql\n' "$SEAFILE_DB_CONTAINER"
	printf 'docker compose up -d\n```\n\n'
	printf '### Note / AFFiNE\n\n```bash\ncd %s\ndocker compose -f compose.yml down\n' "$note_dir"
	printf 'for p in .env compose.yml; do [ -e "/root/restore/files%s/$p" ] && cp -a "/root/restore/files%s/$p" ./; done\n' "$note_dir" "$note_dir"
	printf 'rsync -a /root/restore/files%s/ %s/\n' "${note_config:-$note_dir/config}" "${note_config:-$note_dir/config}"
	printf 'rsync -a /root/restore/files%s/ %s/\n' "${note_blobs:-$note_dir/data/storage}" "${note_blobs:-$note_dir/data/storage}"
	printf 'docker compose -f compose.yml up -d postgres && sleep 15\n'
	printf 'docker exec -i %s sh -c '\''set -- "${POSTGRES_PASSWORD:-}"; PGPASSWORD="$1" exec psql -U "${POSTGRES_USER:-affine}" -d "${POSTGRES_DB:-affine}"'\'' < /root/restore/dumps/affine-postgres.sql\n' "$AFFINE_DB_CONTAINER"
	printf 'docker compose -f compose.yml up -d\n```\n\n'
	printf '### banhve\n\n`sub/banhve` has no service of its own on the VPS; any `banhve` Caddy fragment is restored with the Caddy item.\n'
} > "$staging/RESTORE.md"
chmod 600 "$staging/RESTORE.md" "$manifest"

# ---- Archive -------------------------------------------------------------------
staged_bytes="$(du -sb -- "$staging" | cut -f1)"
if (( staged_bytes > $(free_bytes) )); then
	die "Not enough free disk to write the archive (${staged_bytes} bytes staged)."
fi

if ! have zip && have apt-get; then
	log "zip not found; installing it with apt-get"
	export DEBIAN_FRONTEND=noninteractive
	apt-get install -y --no-install-recommends zip >/dev/null 2>&1 \
		|| { apt-get update -y >/dev/null 2>&1 && apt-get install -y --no-install-recommends zip >/dev/null 2>&1; } \
		|| true
fi

archive_base="$BACKUP_ROOT/eldervibe-backup-$timestamp"
if have zip; then
	archive="$archive_base.zip"
	(cd "$staging" && zip -q -r -y "$archive" .)
else
	archive="$archive_base.tar.gz"
	warn "zip is unavailable and could not be installed; wrote a .tar.gz instead."
	tar -C "$staging" --numeric-owner -czpf "$archive" .
fi
chown root:root "$archive"
chmod 600 "$archive"
cleanup
trap - EXIT

mapfile -t archives < <(ls -1t "$BACKUP_ROOT"/eldervibe-backup-*.zip "$BACKUP_ROOT"/eldervibe-backup-*.tar.gz 2>/dev/null || true)
if (( ${#archives[@]} > BACKUP_KEEP )); then
	for old in "${archives[@]:BACKUP_KEEP}"; do
		rm -f -- "$old"
		log "pruned $old"
	done
fi

printf 'BACKUP_PATH=%s\n' "$archive"
printf 'BACKUP_SIZE=%s (%s bytes)\n' "$(du -h -- "$archive" | cut -f1)" "$(stat -c %s -- "$archive")"
printf 'BACKUP_SHA256=%s\n' "$(sha256sum -- "$archive" | cut -d' ' -f1)"
printf 'BACKUP_INCLUDED=%s\n' "$included"
printf 'BACKUP_MISSING=%s\n' "$missing"
printf 'BACKUP_FAILED=%s\n' "$failed"

if (( failed > 0 )); then
	exit 2
fi
