#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
command -v docker >/dev/null || { printf 'Instalează Docker / Install Docker first.\n'; exit 1; }
if [ ! -f .env ]; then
  command -v openssl >/dev/null || { printf 'OpenSSL este necesar / OpenSSL is required.\n'; exit 1; }
  umask 077
  db_secret=$(openssl rand -hex 32)
  admin_secret="Aa1!$(openssl rand -base64 30)"
  printf 'DATABASE_PASSWORD=%s\nADMIN_USERNAME=administrator\nADMIN_EMAIL=admin@localhost.invalid\nADMIN_PASSWORD=%s\n' "$db_secret" "$admin_secret" > .env
fi
docker compose up -d db scanner
docker compose --profile setup run --rm init
docker compose up -d web
printf '\nDeschide / Open http://localhost:5080\nDatele contului sunt în / Credentials are in .env\nSchimbă parola la prima autentificare / Change password on first login.\n'
