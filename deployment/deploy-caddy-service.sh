#!/usr/bin/env bash
# Shared by multiple deployment wrappers (e.g. Farm, Fences) across branches — keep the SERVICE_* contract backward compatible.
set -Eeuo pipefail

: "${SERVICE_NAME:?SERVICE_NAME must identify the service}"
: "${SERVICE_DOMAIN:?SERVICE_DOMAIN must identify the public domain}"
: "${SERVICE_UPSTREAM_PORT:?SERVICE_UPSTREAM_PORT must identify the container port}"
: "${SERVICE_CONTAINER:?SERVICE_CONTAINER must identify the container}"
: "${SERVICE_IMAGE:?SERVICE_IMAGE must name the image loaded on the VPS}"
: "${SERVICE_ENV_FILE:?SERVICE_ENV_FILE must identify the Docker environment file}"
: "${SERVICE_VOLUME:=}"
: "${CADDY_SITES_DIR:=/etc/caddy/sites}"
: "${CADDY_CONFIG:=/etc/caddy/Caddyfile}"
: "${CADDY_BIN:=caddy}"
: "${CADDY_SERVICE:=caddy}"
: "${DOCKER_BIN:=docker}"
: "${CADDY_LOCK_FILE:=${DEPLOY_LOCK_FILE:-/var/lock/deploy-caddy.lock}}"
: "${SERVICE_CANDIDATE_PORT:=$((SERVICE_UPSTREAM_PORT + 1))}"
: "${SERVICE_STATIC_CONTAINER_PATH:=}"
: "${SERVICE_STATIC_ROOT:=}"
: "${SERVICE_STATIC_PUBLIC_PREFIX:=}"
: "${CADDY_USER:=caddy}"
: "${CADDY_GROUP:=caddy}"

as_root() {
	if [[ "$EUID" -eq 0 ]]; then
		"$@"
	else
		sudo -n -- "$@"
	fi
}

if [[ "$EUID" -ne 0 ]]; then
	if ! command -v sudo >/dev/null 2>&1 || ! sudo -n -l >/dev/null 2>&1; then
		echo "Deployment requires passwordless sudo for Caddy configuration, Caddy static files, and Caddy reloads. Configure the VPS deploy-user sudoers policy before retrying." >&2
		exit 1
	fi
fi

docker_command=("$DOCKER_BIN")
if ! "${docker_command[@]}" info >/dev/null 2>&1; then
	if [[ "$EUID" -eq 0 ]]; then
		echo "Docker is unavailable to the deployment user" >&2
		exit 1
	fi
	docker_command=(sudo -n "$DOCKER_BIN")
	if ! "${docker_command[@]}" info >/dev/null 2>&1; then
		echo "Docker is unavailable. Add the deploy user to the docker group or allow passwordless sudo for $DOCKER_BIN." >&2
		exit 1
	fi
fi

docker() {
	# `command` bypasses this function's own name when docker_command=("docker"), avoiding infinite self-recursion.
	command "${docker_command[@]}" "$@"
}

if [[ ! "$SERVICE_DOMAIN" =~ ^[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?(\.[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?)+$ ]]; then
	echo "SERVICE_DOMAIN must be a lowercase DNS name: $SERVICE_DOMAIN" >&2
	exit 2
fi

if [[ -n "$SERVICE_STATIC_CONTAINER_PATH$SERVICE_STATIC_ROOT$SERVICE_STATIC_PUBLIC_PREFIX" \
	&& ( -z "$SERVICE_STATIC_CONTAINER_PATH" || -z "$SERVICE_STATIC_ROOT" || -z "$SERVICE_STATIC_PUBLIC_PREFIX" ) ]]; then
	echo "All SERVICE_STATIC_* values must be set when static files are enabled" >&2
	exit 2
fi

if [[ ! "$SERVICE_UPSTREAM_PORT" =~ ^[0-9]+$ || ! "$SERVICE_CANDIDATE_PORT" =~ ^[0-9]+$ ]]; then
	echo "SERVICE_UPSTREAM_PORT and SERVICE_CANDIDATE_PORT must be numeric" >&2
	exit 2
fi

fragment="$CADDY_SITES_DIR/$SERVICE_DOMAIN.caddy"
fragment_tmp="$CADDY_SITES_DIR/.$SERVICE_DOMAIN.caddy.$$"
fragment_backup="$CADDY_SITES_DIR/.$SERVICE_DOMAIN.caddy.previous.$$"
state=absent
candidate_container="$SERVICE_CONTAINER-candidate-$$"
candidate_started=false
deployment_committed=false
static_release=
static_releases_dir=
fragment_source=

umask 077
as_root install -d -m 0755 "$(dirname "$CADDY_LOCK_FILE")" "$CADDY_SITES_DIR"
as_root install -m 0600 -o "$(id --user)" -g "$(id --group)" /dev/null "$CADDY_LOCK_FILE"

ensure_caddy_path_access() {
	local directory="$1"
	as_root chmod a+rx "$directory"
}

ensure_caddy_path_access "$CADDY_SITES_DIR"
exec 9>"$CADDY_LOCK_FILE"
flock -n 9 || { echo "Another Caddy deployment is already running" >&2; exit 1; }

cleanup() {
	rm -f "$fragment_source"
	as_root rm -f "$fragment_tmp" "$fragment_backup"
	if [[ "$deployment_committed" != true ]]; then
		if [[ "$candidate_started" == true ]]; then
			docker rm --force "$candidate_container" >/dev/null 2>&1 || true
		fi
		if [[ -n "$static_release" ]]; then
			as_root rm -rf -- "$static_release"
		fi
	fi
}
trap cleanup EXIT

capture_fragment() {
	if [[ -f "$fragment" ]]; then
		state=file
		as_root cp -- "$fragment" "$fragment_backup"
	fi
}

restore_fragment() {
	case "$state" in
		file) as_root mv -f -- "$fragment_backup" "$fragment" ;;
		absent) as_root rm -f -- "$fragment" ;;
	esac
	if [[ -f "$fragment" ]]; then
		as_root chmod 0644 "$fragment"
	fi
}

reload_caddy() {
	as_root systemctl reload "$CADDY_SERVICE"
}

rollback_caddy() {
	restore_fragment
	if ! "$CADDY_BIN" validate --config "$CADDY_CONFIG" --adapter caddyfile >/dev/null; then
		echo "Restored Caddy configuration is invalid" >&2
		return 1
	fi
	reload_caddy
}

active_port=
if docker container inspect "$SERVICE_CONTAINER" >/dev/null 2>&1; then
	active_mapping=$(docker port "$SERVICE_CONTAINER" 80)
	active_port="${active_mapping##*:}"
fi

candidate_port="$SERVICE_CANDIDATE_PORT"
if [[ "$active_port" == "$candidate_port" ]]; then
	candidate_port="$SERVICE_UPSTREAM_PORT"
fi

docker_volume_args=()
if [[ -n "$SERVICE_VOLUME" ]]; then
	docker_volume_args+=(--volume "$SERVICE_VOLUME")
fi

docker run --detach --name "$candidate_container" --restart no \
	--publish "127.0.0.1:$candidate_port:80" \
	"${docker_volume_args[@]}" \
	--env-file "$SERVICE_ENV_FILE" "$SERVICE_IMAGE" >/dev/null
candidate_started=true

health_status=
for attempt in {1..30}; do
	health_status=$(curl --silent --show-error --output /dev/null --write-out '%{http_code}' \
		--max-time 2 "http://127.0.0.1:$candidate_port/" || true)
	if [[ "$health_status" =~ ^[23][0-9][0-9]$ ]]; then
		break
	fi
	sleep 1
done

if [[ ! "$health_status" =~ ^[23][0-9][0-9]$ ]]; then
	echo "Candidate $candidate_container did not return a successful HTTP response" >&2
	docker logs "$candidate_container" >&2 || true
	exit 1
fi

docker update --restart unless-stopped "$candidate_container" >/dev/null

static_caddy_config=
if [[ -n "$SERVICE_STATIC_ROOT" ]]; then
	static_releases_dir="$SERVICE_STATIC_ROOT/releases"
	static_release="$static_releases_dir/$candidate_container"
	as_root install -d -m 0755 "$SERVICE_STATIC_ROOT" "$static_releases_dir" "$static_release"
	ensure_caddy_path_access "$static_release"
	as_root "$DOCKER_BIN" cp "$candidate_container:$SERVICE_STATIC_CONTAINER_PATH/." "$static_release"
	as_root find "$static_release" -type d -exec chmod 0755 {} +
	as_root find "$static_release" -type f -exec chmod 0644 {} +
	as_root chown -R "$CADDY_USER:$CADDY_GROUP" "$static_release"
	static_caddy_config=$(cat <<EOF
	@service_static_assets path $SERVICE_STATIC_PUBLIC_PREFIX/assets/*
	handle @service_static_assets {
		uri strip_prefix $SERVICE_STATIC_PUBLIC_PREFIX
		root * $static_release
		file_server
	}

	@service_static_favicon path $SERVICE_STATIC_PUBLIC_PREFIX/favicon.svg
	handle @service_static_favicon {
		uri strip_prefix $SERVICE_STATIC_PUBLIC_PREFIX
		root * $static_release
		file_server
	}

EOF
)
fi

capture_fragment
fragment_source=$(mktemp)
cat > "$fragment_source" <<EOF
$SERVICE_DOMAIN {
$static_caddy_config	handle {
		reverse_proxy 127.0.0.1:$candidate_port
	}
}
EOF
as_root install -m 0644 "$fragment_source" "$fragment_tmp"
as_root mv -f -- "$fragment_tmp" "$fragment"
as_root chmod 0644 "$fragment"
rm -f "$fragment_source"
fragment_source=

if ! "$CADDY_BIN" validate --config "$CADDY_CONFIG" --adapter caddyfile; then
	rollback_caddy
	echo "Caddy configuration validation failed; restored $fragment" >&2
	exit 1
fi

if ! reload_caddy; then
	rollback_caddy
	echo "Caddy reload failed; restored $fragment" >&2
	exit 1
fi

deployment_committed=true
if [[ -n "$active_port" ]]; then
	if ! docker stop "$SERVICE_CONTAINER" >/dev/null; then
		echo "Could not stop previous container $SERVICE_CONTAINER; candidate remains available as $candidate_container" >&2
	fi
	if ! docker rm "$SERVICE_CONTAINER" >/dev/null; then
		echo "Could not remove previous container $SERVICE_CONTAINER; candidate remains available as $candidate_container" >&2
	fi
fi
if ! docker rename "$candidate_container" "$SERVICE_CONTAINER"; then
	echo "Could not rename candidate $candidate_container; it remains available on port $candidate_port" >&2
fi
candidate_started=false

if [[ -n "$static_releases_dir" ]]; then
	as_root find "$static_releases_dir" -mindepth 1 -maxdepth 1 -type d ! -name "$candidate_container" -exec rm -rf {} +
fi

echo "$SERVICE_NAME deployed at https://$SERVICE_DOMAIN using Caddy automatic HTTPS"