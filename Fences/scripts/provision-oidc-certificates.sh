#!/usr/bin/env bash
set -euo pipefail

secrets_dir=/etc/shuneo/fences-secrets
runtime_env_file="/etc/shuneo/fences.env"
signing_pfx="$secrets_dir/oidc-signing.pfx"
encryption_pfx="$secrets_dir/oidc-encryption.pfx"
secrets_parent="$(dirname "$secrets_dir")"

staging_dir=""
secrets_staging_dir=""
secrets_installed=false
runtime_env_installed=false
signing_key=""
encryption_key=""
signing_certificate=""
encryption_certificate=""
signing_config=""
encryption_config=""
signing_password_file=""
encryption_password_file=""
signing_pfx_staging=""
encryption_pfx_staging=""
runtime_env_staging=""

cleanup() {
    if [[ -n "$staging_dir" ]]; then
        rm -rf -- "$staging_dir"
    fi
    if [[ "$runtime_env_installed" == true ]]; then
        rm -f -- "$runtime_env_file"
    fi
    if [[ "$secrets_installed" == true ]]; then
        rm -rf -- "$secrets_dir"
    fi
}
trap cleanup EXIT

usage() {
    echo "Usage: $0 --runtime-env-file PATH" >&2
}

if [[ "${EUID}" -ne 0 ]]; then
    echo "Run this script as root; it creates root-owned OIDC secret material." >&2
    exit 1
fi

if [[ "$#" -ne 2 || "$1" != "--runtime-env-file" || -z "$2" ]]; then
    usage
    exit 1
fi
input_runtime_env_file="$2"

if ! command -v openssl >/dev/null 2>&1; then
    echo "openssl is required to provision OIDC certificates." >&2
    exit 1
fi

if [[ ! -d "$secrets_parent" || -L "$secrets_parent" ]]; then
    echo "Expected non-symlink directory: $secrets_parent" >&2
    exit 1
fi
secrets_parent_metadata="$(stat -c '%u:%g:%a' -- "$secrets_parent")"
IFS=: read -r secrets_parent_owner secrets_parent_group secrets_parent_mode <<< "$secrets_parent_metadata"
if [[ "$secrets_parent_owner:$secrets_parent_group" != "0:0" ]] || (( (8#$secrets_parent_mode & 022) != 0 )); then
    echo "Expected root-owned directory with no group or other write permission: $secrets_parent" >&2
    exit 1
fi

if [[ -e "$secrets_dir" || -L "$secrets_dir" || -e "$runtime_env_file" || -L "$runtime_env_file" ]]; then
    echo "Refusing to provision because Fences secret material or runtime environment already exists." >&2
    exit 1
fi

if [[ ! -f "$input_runtime_env_file" || -L "$input_runtime_env_file" ]]; then
    echo "The runtime environment input must be a regular file." >&2
    exit 1
fi
if [[ "$(stat -c '%u:%a' -- "$input_runtime_env_file")" != "0:600" ]]; then
    echo "The runtime environment input must be root-owned with mode 600." >&2
    exit 1
fi

required_base_keys=(
    ASPNETCORE_ENVIRONMENT
    Authentication__GitHub__ClientId
    Authentication__GitHub__ClientSecret
    IdentityApp__BrandName
    IdentityApp__CookieDomain
    IdentityApp__OidcIssuer
    IdentityApp__PersistentLoginDays
    IdentityApp__AllowedReturnHosts__0
    IdentityApp__AllowedCorsOrigins__0
)
managed_values=(
    'ASPNETCORE_ENVIRONMENT=Production'
    'IdentityApp__OidcSigningCertificatePath=/run/secrets/oidc-signing.pfx'
    'IdentityApp__OidcEncryptionCertificatePath=/run/secrets/oidc-encryption.pfx'
    'IdentityApp__IdentityDatabasePath=/var/lib/fences/identity.db'
    'IdentityApp__DataProtectionKeysPath=/var/lib/fences/keys'
)

declare -A seen_keys=()
declare -A dotenv_values=()
while IFS= read -r line || [[ -n "$line" ]]; do
    [[ -z "$line" || "$line" =~ ^[[:space:]]*# ]] && continue
    if [[ ! "$line" =~ ^[A-Za-z_][A-Za-z0-9_]*= ]]; then
        echo "Runtime environment input contains an invalid dotenv line." >&2
        exit 1
    fi
    key="${line%%=*}"
    if [[ -n "${seen_keys[$key]:-}" ]]; then
        echo "Runtime environment input contains a duplicate variable: $key" >&2
        exit 1
    fi
    seen_keys[$key]=1
    dotenv_values[$key]="${line#*=}"
done < "$input_runtime_env_file"

for required_key in "${required_base_keys[@]}"; do
    if [[ -z "${seen_keys[$required_key]:-}" ]]; then
        echo "Runtime environment input is missing required variable: $required_key" >&2
        exit 1
    fi
    if [[ ! "${dotenv_values[$required_key]}" =~ [^[:space:]] ]]; then
        echo "Runtime environment input has blank required variable: $required_key" >&2
        exit 1
    fi
done
complete_oidc_client=false
for key in "${!seen_keys[@]}"; do
    if [[ "$key" =~ ^IdentityApp__OidcClients__([0-9]+)__ClientId$ ]]; then
        client_index="${BASH_REMATCH[1]}"
        redirect_key="IdentityApp__OidcClients__${client_index}__RedirectUris__0"
        if [[ ! "${dotenv_values[$key]}" =~ [^[:space:]] || ! "${dotenv_values[$redirect_key]:-}" =~ [^[:space:]] ]]; then
            echo "OIDC client $client_index requires nonblank ClientId and RedirectUris__0." >&2
            exit 1
        fi
        complete_oidc_client=true
    elif [[ "$key" =~ ^IdentityApp__OidcClients__([0-9]+)__RedirectUris__0$ ]]; then
        client_index="${BASH_REMATCH[1]}"
        client_key="IdentityApp__OidcClients__${client_index}__ClientId"
        if [[ ! "${dotenv_values[$key]}" =~ [^[:space:]] || ! "${dotenv_values[$client_key]:-}" =~ [^[:space:]] ]]; then
            echo "OIDC client $client_index requires nonblank ClientId and RedirectUris__0." >&2
            exit 1
        fi
        complete_oidc_client=true
    fi
done
if [[ "$complete_oidc_client" != true ]]; then
    echo "Runtime environment input requires at least one OIDC client with ClientId and RedirectUris__0." >&2
    exit 1
fi
for managed_value in "${managed_values[@]}"; do
    managed_key="${managed_value%%=*}"
    if [[ -n "${seen_keys[$managed_key]:-}" ]] && ! grep -qxF -- "$managed_value" "$input_runtime_env_file"; then
        echo "Runtime environment input conflicts with managed variable: $managed_key" >&2
        exit 1
    fi
done
for generated_key in IdentityApp__OidcSigningCertificatePassword IdentityApp__OidcEncryptionCertificatePassword; do
    if [[ -n "${seen_keys[$generated_key]:-}" ]]; then
        echo "Runtime environment input must not define generated variable: $generated_key" >&2
        exit 1
    fi
done

umask 077
staging_dir="$(mktemp -d "$secrets_parent/.${secrets_dir##*/}.tmp.XXXXXX")"
chown root:root -- "$staging_dir"
chmod 700 -- "$staging_dir"
secrets_staging_dir="$staging_dir/${secrets_dir##*/}"
mkdir -- "$secrets_staging_dir"
chown root:root -- "$secrets_staging_dir"
chmod 700 -- "$secrets_staging_dir"

signing_key="$secrets_staging_dir/.oidc-signing-key"
encryption_key="$secrets_staging_dir/.oidc-encryption-key"
signing_certificate="$secrets_staging_dir/.oidc-signing-cert"
encryption_certificate="$secrets_staging_dir/.oidc-encryption-cert"
signing_config="$secrets_staging_dir/.oidc-signing-config"
encryption_config="$secrets_staging_dir/.oidc-encryption-config"
signing_password_file="$secrets_staging_dir/.oidc-signing-password"
encryption_password_file="$secrets_staging_dir/.oidc-encryption-password"
signing_pfx_staging="$secrets_staging_dir/oidc-signing.pfx"
encryption_pfx_staging="$secrets_staging_dir/oidc-encryption.pfx"
runtime_env_staging="$staging_dir/fences.env"

openssl rand -hex 32 > "$signing_password_file"
openssl rand -hex 32 > "$encryption_password_file"
while cmp -s "$signing_password_file" "$encryption_password_file"; do
    openssl rand -hex 32 > "$encryption_password_file"
done

openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:3072 -out "$signing_key"
openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:3072 -out "$encryption_key"

cat > "$signing_config" <<'EOF'
[req]
prompt = no
distinguished_name = subject
x509_extensions = extensions

[subject]
CN = Fences OIDC Signing

[extensions]
basicConstraints = critical,CA:FALSE
keyUsage = critical,digitalSignature
EOF
cat > "$encryption_config" <<'EOF'
[req]
prompt = no
distinguished_name = subject
x509_extensions = extensions

[subject]
CN = Fences OIDC Encryption

[extensions]
basicConstraints = critical,CA:FALSE
keyUsage = critical,keyEncipherment,dataEncipherment
EOF

openssl req -x509 -new -sha256 -days 3650 -key "$signing_key" \
    -config "$signing_config" -extensions extensions -out "$signing_certificate"
openssl req -x509 -new -sha256 -days 3650 -key "$encryption_key" \
    -config "$encryption_config" -extensions extensions -out "$encryption_certificate"

openssl pkcs12 -export -out "$signing_pfx_staging" -inkey "$signing_key" \
    -in "$signing_certificate" -passout "file:$signing_password_file" -name 'Fences OIDC Signing'
openssl pkcs12 -export -out "$encryption_pfx_staging" -inkey "$encryption_key" \
    -in "$encryption_certificate" -passout "file:$encryption_password_file" -name 'Fences OIDC Encryption'

openssl pkcs12 -in "$signing_pfx_staging" -passin "file:$signing_password_file" -noout
openssl pkcs12 -in "$encryption_pfx_staging" -passin "file:$encryption_password_file" -noout

cat "$input_runtime_env_file" > "$runtime_env_staging"
for managed_value in "${managed_values[@]}"; do
    managed_key="${managed_value%%=*}"
    if [[ -z "${seen_keys[$managed_key]:-}" ]]; then
        printf '%s\n' "$managed_value" >> "$runtime_env_staging"
    fi
done
printf 'IdentityApp__OidcSigningCertificatePassword=%s\n' "$(<"$signing_password_file")" >> "$runtime_env_staging"
printf 'IdentityApp__OidcEncryptionCertificatePassword=%s\n' "$(<"$encryption_password_file")" >> "$runtime_env_staging"

for secret_file in "$signing_pfx_staging" "$encryption_pfx_staging" "$runtime_env_staging"; do
    test -s "$secret_file"
    chown root:root -- "$secret_file"
    chmod 600 -- "$secret_file"
done

grep -qxE 'IdentityApp__OidcSigningCertificatePassword=[[:xdigit:]]{64}' "$runtime_env_staging"
grep -qxE 'IdentityApp__OidcEncryptionCertificatePassword=[[:xdigit:]]{64}' "$runtime_env_staging"

rm -f -- "$signing_key" "$encryption_key" \
    "$signing_certificate" "$encryption_certificate" \
    "$signing_config" "$encryption_config" \
    "$signing_password_file" "$encryption_password_file"

mv -T -- "$secrets_staging_dir" "$secrets_dir"
secrets_installed=true
mv -- "$runtime_env_staging" "$runtime_env_file"
runtime_env_installed=true
rm -f -- "$signing_key" "$encryption_key" "$signing_certificate" "$encryption_certificate" "$signing_config" "$encryption_config" "$signing_password_file" "$encryption_password_file"
rm -rf -- "$staging_dir"
staging_dir=""
secrets_installed=false
runtime_env_installed=false

echo "Created root-only Fences OIDC certificate files and runtime environment."