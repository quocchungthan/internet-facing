#!/usr/bin/env bash
# ==============================================================================
# Server Provisioning & Toolchain Installer
# Target OS: Ubuntu 24.04 LTS (Noble Numbat)
#
# Requirements Installed:
#   - Core system packages & build tools (curl, wget, jq, git, gcc, make, etc.)
#   - Docker Engine Community + Docker Compose v5 / plugin + containerd
#   - Caddy v2 (replacing Nginx with automated ACME HTTPS reverse proxy)
#   - .NET 10 SDK & Runtimes (dotnet-sdk-10.0)
#   - Node.js 20 LTS + npm + pnpm
#   - GitHub CLI (gh)
#   - Python 3 + pip + venv
#   - VPS Performance Tuning & Emergency Swapfile (4GB)
#   - UFW Firewall (SSH: 22, HTTP: 80, HTTPS: 443)
# ==============================================================================

set -Eeuo pipefail

# ── Color Palette ─────────────────────────────────────────────────────────────
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
BOLD='\033[1m'
NC='\033[0m' # No Color

log_step() {
    printf "\n${CYAN}${BOLD}==> %s${NC}\n" "$1"
}

log_info() {
    printf "  ${BLUE}[INFO]${NC} %s\n" "$1"
}

log_success() {
    printf "  ${GREEN}[✓]${NC} %s\n" "$1"
}

log_warn() {
    printf "  ${YELLOW}[WARN]${NC} %s\n" "$1"
}

log_error() {
    printf "  ${RED}[ERROR]${NC} %s\n" "$1" >&2
}

# ── Configuration & Defaults ──────────────────────────────────────────────────
ACME_EMAIL="${ACME_EMAIL:-}"
CONFIGURE_UFW="${CONFIGURE_UFW:-true}"
CREATE_SWAP="${CREATE_SWAP:-true}"
APPLY_SYSCTL="${APPLY_SYSCTL:-true}"
SKIP_SYSTEM_UPGRADE="${SKIP_SYSTEM_UPGRADE:-false}"
REMOVE_NGINX="${REMOVE_NGINX:-true}"

show_help() {
    cat <<EOF
Usage: sudo ./setup-server.sh [options]

Options:
  --email <email>       Email address for Caddy ACME SSL certificate registration.
  --no-swap             Skip creating 4GB emergency swapfile.
  --no-ufw              Skip configuring UFW firewall rules.
  --no-sysctl           Skip applying VPS sysctl performance tweaks.
  --skip-upgrade        Skip running 'apt-get upgrade' (runs 'apt-get update' only).
  --keep-nginx          Do not purge Nginx if present (NOT recommended when using Caddy).
  -h, --help            Show this help message.

Environment Variables:
  ACME_EMAIL            Email address for Caddy Let's Encrypt / ZeroSSL.
  CONFIGURE_UFW         'true' (default) or 'false'
  CREATE_SWAP           'true' (default) or 'false'
  APPLY_SYSCTL          'true' (default) or 'false'
  SKIP_SYSTEM_UPGRADE   'false' (default) or 'true'
  REMOVE_NGINX          'true' (default) or 'false'
EOF
}

# Parse CLI arguments
while [[ $# -gt 0 ]]; do
    case "$1" in
        --email)
            ACME_EMAIL="$2"
            shift 2
            ;;
        --no-swap)
            CREATE_SWAP="false"
            shift
            ;;
        --no-ufw)
            CONFIGURE_UFW="false"
            shift
            ;;
        --no-sysctl)
            APPLY_SYSCTL="false"
            shift
            ;;
        --skip-upgrade)
            SKIP_SYSTEM_UPGRADE="true"
            shift
            ;;
        --keep-nginx)
            REMOVE_NGINX="false"
            shift
            ;;
        -h|--help)
            show_help
            exit 0
            ;;
        *)
            log_error "Unknown argument: $1"
            show_help
            exit 1
            ;;
    esac
done

# ── Pre-flight Checks ─────────────────────────────────────────────────────────
if [[ "$EUID" -ne 0 ]]; then
    log_error "This script must be run as root. Please run with sudo or as root user."
    exit 1
fi

export DEBIAN_FRONTEND=noninteractive
export NEEDRESTART_MODE=a

# Disable interactive prompts from needrestart during apt execution
mkdir -p /etc/needrestart/conf.d
cat > /etc/needrestart/conf.d/99-autorestart.conf <<'EOF'
$nrconf{restart} = 'a';
EOF

log_step "Detecting Operating System"
if [[ -f /etc/os-release ]]; then
    . /etc/os-release
    log_info "Detected OS: $PRETTY_NAME ($ID $VERSION_ID)"
else
    log_warn "/etc/os-release not found. Assuming Ubuntu/Debian compatible."
fi

# ── 1. Base Repositories & Essential Tools ────────────────────────────────────
log_step "1/9: Updating Base Packages & Installing Essentials"
apt-get update -y

if [[ "$SKIP_SYSTEM_UPGRADE" != "true" ]]; then
    log_info "Performing apt-get upgrade..."
    apt-get upgrade -y -o Dpkg::Options::="--force-confdef" -o Dpkg::Options::="--force-confold"
fi

apt-get install -y --no-install-recommends \
    apt-transport-https \
    ca-certificates \
    curl \
    gnupg \
    lsb-release \
    wget \
    jq \
    unzip \
    tar \
    rsync \
    build-essential \
    software-properties-common \
    git \
    tmux \
    htop \
    ufw

log_success "Base packages installed"

# ── 2. Nginx Conflict Prevention (Caddy Preferred) ───────────────────────────
log_step "2/9: Ensuring Nginx Is Disabled & Removed (Caddy Required)"
if [[ "$REMOVE_NGINX" == "true" ]]; then
    if command -v nginx >/dev/null 2>&1 || systemctl list-unit-files | grep -q "nginx.service"; then
        log_info "Nginx detected. Stopping, disabling, and purging Nginx packages..."
        systemctl stop nginx 2>/dev/null || true
        systemctl disable nginx 2>/dev/null || true
        apt-get remove --purge -y nginx nginx-common nginx-core 2>/dev/null || true
        apt-get autoremove -y 2>/dev/null || true
        log_success "Nginx successfully removed to free ports 80 and 443 for Caddy"
    else
        log_info "Nginx is not installed. Ports 80 and 443 are free for Caddy."
    fi
else
    log_warn "Nginx removal skipped by user. Note: Caddy requires ports 80 and 443."
fi

# ── 3. Docker Engine & Docker Compose ─────────────────────────────────────────
log_step "3/9: Installing Docker Engine & Docker Compose Plugin"
install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
chmod a+r /etc/apt/keyrings/docker.asc

echo \
  "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu \
  $(. /etc/os-release && echo "$VERSION_CODENAME") stable" | \
  tee /etc/apt/sources.list.d/docker.list > /dev/null

apt-get update -y
apt-get install -y \
    docker-ce \
    docker-ce-cli \
    containerd.io \
    docker-buildx-plugin \
    docker-compose-plugin

systemctl enable docker
systemctl enable containerd
systemctl start docker
systemctl start containerd

# Add invoking user to docker group if run through sudo
if [[ -n "${SUDO_USER:-}" && "$SUDO_USER" != "root" ]]; then
    usermod -aG docker "$SUDO_USER"
    log_info "Added user '$SUDO_USER' to 'docker' group"
fi
log_success "Docker Engine and Docker Compose installed and running"

# ── 4. Caddy Web Server (Automated HTTPS) ─────────────────────────────────────
log_step "4/9: Installing Caddy Web Server"
apt-get install -y debian-keyring debian-archive-keyring
curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/gpg.key' | gpg --dearmor --yes -o /usr/share/keyrings/caddy-stable-archive-keyring.gpg
curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt' | tee /etc/apt/sources.list.d/caddy-stable.list > /dev/null

apt-get update -y
apt-get install -y caddy

# Set up sites directory and base Caddyfile
mkdir -p /etc/caddy/sites

caddyfile_email_directive=""
if [[ -n "$ACME_EMAIL" ]]; then
    caddyfile_email_directive="    email $ACME_EMAIL"
fi

cat > /etc/caddy/Caddyfile <<EOF
{
$caddyfile_email_directive
}

# Import all individual site configurations from /etc/caddy/sites
import /etc/caddy/sites/*.caddy
EOF

systemctl enable caddy
systemctl restart caddy
log_success "Caddy installed and enabled with sites directory /etc/caddy/sites"

# ── 5. .NET 10 SDK ────────────────────────────────────────────────────────────
log_step "5/9: Installing .NET 10 SDK"
if apt-cache show dotnet-sdk-10.0 >/dev/null 2>&1; then
    log_info "Installing dotnet-sdk-10.0 from Ubuntu package repositories..."
    apt-get install -y dotnet-sdk-10.0
else
    log_info "Native package dotnet-sdk-10.0 not found in current apt index. Installing via Microsoft dotnet-install script..."
    tmp_dotnet_install="/tmp/dotnet-install.sh"
    curl -fsSL https://dot.net/v1/dotnet-install.sh -o "$tmp_dotnet_install"
    chmod +x "$tmp_dotnet_install"
    "$tmp_dotnet_install" --channel 10.0 --install-dir /usr/share/dotnet
    ln -sf /usr/share/dotnet/dotnet /usr/bin/dotnet
    rm -f "$tmp_dotnet_install"
fi
log_success ".NET 10 SDK installed: $(dotnet --version)"

# ── 6. Node.js 20 LTS, npm, and pnpm ──────────────────────────────────────────
log_step "6/9: Installing Node.js 20 LTS, npm, and pnpm"
curl -fsSL https://deb.nodesource.com/setup_20.x | bash -
apt-get install -y nodejs
npm install -g pnpm
log_success "Node.js $(node -v), npm $(npm -v), and pnpm $(pnpm -v) installed"

# ── 7. GitHub CLI (gh) ────────────────────────────────────────────────────────
log_step "7/9: Installing GitHub CLI (gh)"
mkdir -p -m 755 /etc/apt/keyrings
wget -qO- https://cli.github.com/packages/githubcli-archive-keyring.gpg | tee /etc/apt/keyrings/githubcli-archive-keyring.gpg > /dev/null
chmod go+r /etc/apt/keyrings/githubcli-archive-keyring.gpg
echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/githubcli-archive-keyring.gpg] https://cli.github.com/packages stable main" | tee /etc/apt/sources.list.d/github-cli.list > /dev/null
apt-get update -y
apt-get install -y gh
log_success "GitHub CLI installed: $(gh --version | head -n 1)"

# ── 8. Python 3 & Pip ─────────────────────────────────────────────────────────
log_step "8/9: Installing Python 3, Pip, and venv"
apt-get install -y python3 python3-pip python3-venv
log_success "Python installed: $(python3 --version)"

# ── 9. System Tuning, Swap & Firewall (UFW) ───────────────────────────────────
log_step "9/9: VPS Performance Tuning, Swap & Firewall Configuration"

# Emergency Swapfile
if [[ "$CREATE_SWAP" == "true" ]]; then
    if [[ -z "$(swapon --show)" ]]; then
        log_info "No active swap detected. Allocating 4GB emergency swapfile at /swapfile..."
        fallocate -l 4G /swapfile || dd if=/dev/zero of=/swapfile bs=1M count=4096
        chmod 600 /swapfile
        mkswap /swapfile
        swapon /swapfile
        if ! grep -q "/swapfile" /etc/fstab; then
            echo "/swapfile none swap sw 0 0" >> /etc/fstab
        fi
        log_success "4GB swapfile created and enabled in /etc/fstab"
    else
        log_info "Active swapfile/partition already exists ($(swapon --show --noheadings | awk '{print $1, $3}')). Skipping."
    fi
fi

# Sysctl Tuning
if [[ "$APPLY_SYSCTL" == "true" ]]; then
    log_info "Applying VPS sysctl tuning (swappiness=10, max_map_count=1048576)..."
    cat > /etc/sysctl.d/99-vps-performance.conf <<'EOF'
# Conservative VPS tuning: keep memory in RAM, retain swap for emergency bursts
vm.swappiness = 10
vm.vfs_cache_pressure = 75
vm.dirty_background_ratio = 5
vm.dirty_ratio = 15
vm.max_map_count = 1048576
EOF
    sysctl --system >/dev/null 2>&1 || true
    log_success "Sysctl performance rules applied"
fi

# UFW Firewall
if [[ "$CONFIGURE_UFW" == "true" ]]; then
    log_info "Configuring UFW (allow 22/SSH, 80/HTTP, 443/HTTPS)..."
    ufw default deny incoming
    ufw default allow outgoing
    ufw allow 22/tcp comment 'SSH'
    ufw allow 80/tcp comment 'HTTP (Caddy)'
    ufw allow 443/tcp comment 'HTTPS (Caddy)'
    ufw --force enable
    log_success "UFW firewall enabled and ports 22, 80, 443 opened"
fi

# ── Post-Installation Verification Report ─────────────────────────────────────
printf "\n${GREEN}${BOLD}================================================================${NC}\n"
printf "${GREEN}${BOLD}             SERVER REQUIREMENTS INSTALLATION COMPLETE           ${NC}\n"
printf "${GREEN}${BOLD}================================================================${NC}\n\n"

printf "%-22s %-12s %s\n" "TOOL / SERVICE" "STATUS" "VERSION / INFO"
printf "%-22s %-12s %s\n" "----------------------" "------------" "--------------------------------"

check_status() {
    local name="$1"
    local cmd="$2"
    if eval "$cmd" >/dev/null 2>&1; then
        local ver
        ver=$(eval "$cmd" 2>&1 | head -n 1)
        printf "%-22s ${GREEN}%-12s${NC} %s\n" "$name" "INSTALLED" "$ver"
    else
        printf "%-22s ${RED}%-12s${NC} %s\n" "$name" "FAILED" "Not found or exited with error"
    fi
}

check_service() {
    local name="$1"
    local svc="$2"
    if systemctl is-active --quiet "$svc" 2>/dev/null; then
        printf "%-22s ${GREEN}%-12s${NC} %s\n" "$name" "ACTIVE" "Running (systemd)"
    else
        printf "%-22s ${YELLOW}%-12s${NC} %s\n" "$name" "INACTIVE" "Not active"
    fi
}

check_status "Docker Engine" "docker --version"
check_status "Docker Compose" "docker compose version"
check_service "Docker Service" "docker"
check_status "Caddy Server" "caddy version"
check_service "Caddy Service" "caddy"
check_status ".NET SDK" "dotnet --version"
check_status "Node.js" "node --version"
check_status "npm" "npm --version"
check_status "pnpm" "pnpm --version"
check_status "GitHub CLI" "gh --version"
check_status "Python 3" "python3 --version"
check_status "Git" "git --version"
check_service "UFW Firewall" "ufw"

printf "\n${CYAN}Caddy configuration: /etc/caddy/Caddyfile${NC}\n"
printf "${CYAN}Drop site configurations in: /etc/caddy/sites/<domain>.caddy${NC}\n\n"
