#!/usr/bin/env bash
set -euo pipefail

SECRETS=/secrets
mkdir -p "$SECRETS"/{ca,jwt,services,clients}

# --- Dev Root CA (10y) ---
if [[ ! -f "$SECRETS/ca/ca.key" ]]; then
  openssl genrsa -out "$SECRETS/ca/ca.key" 4096
  openssl req -x509 -new -nodes -key "$SECRETS/ca/ca.key" \
    -sha256 -days 3650 -subj "/CN=eshop-dev-ca" -out "$SECRETS/ca/ca.crt"
fi

gen_server() {
  local name="$1" dir="$SECRETS/services/$1"
  mkdir -p "$dir"
  [[ -f "$dir/server.key" ]] || openssl genrsa -out "$dir/server.key" 2048
  cat > "$dir/openssl.cnf" <<EOF
basicConstraints=CA:FALSE
keyUsage = digitalSignature, keyEncipherment
extendedKeyUsage = serverAuth
subjectAltName = @alt_names
[alt_names]
DNS.1 = ${name}
DNS.2 = ${name}.local
EOF
  openssl req -new -key "$dir/server.key" -subj "/CN=${name}" -out "$dir/server.csr"
  openssl x509 -req -in "$dir/server.csr" -CA "$SECRETS/ca/ca.crt" -CAkey "$SECRETS/ca/ca.key" \
    -CAcreateserial -out "$dir/server.crt" -days 365 -sha256 -extfile "$dir/openssl.cnf"
}

gen_client() {
  local name="$1" dir="$SECRETS/clients/$1"
  mkdir -p "$dir"
  [[ -f "$dir/client.key" ]] || openssl genrsa -out "$dir/client.key" 2048
  cat > "$dir/openssl.cnf" <<EOF
basicConstraints=CA:FALSE
keyUsage = digitalSignature, keyEncipherment
extendedKeyUsage = clientAuth
EOF
  openssl req -new -key "$dir/client.key" -subj "/CN=${name}" -out "$dir/client.csr"
  openssl x509 -req -in "$dir/client.csr" -CA "$SECRETS/ca/ca.crt" -CAkey "$SECRETS/ca/ca.key" \
    -CAcreateserial -out "$dir/client.crt" -days 365 -sha256 -extfile "$dir/openssl.cnf"
}

# --- Per-service server certs (names must match service names)
for svc in orders catalog; do
  gen_server "$svc"
done

# --- Gateway client cert (for mTLS to services)
gen_client gateway

# --- App secret key ---
if [[ ! -f "$SECRETS/jwt/secret.key" ]]; then
  openssl rand -base64 32 > "$SECRETS/jwt/secret.key"
fi


chmod 600 "$SECRETS"/**/*.key || true
echo "✅ dev secrets ready in $SECRETS"
