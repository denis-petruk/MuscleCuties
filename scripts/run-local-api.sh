#!/usr/bin/env bash
# Run the API on loopback against a disposable local PostgreSQL, for simulator QA.
#
# Generates throwaway per-run keys (JWT signing, email-receipt RSA pair, email-challenge HMAC) in a temp folder that is
# deleted on exit; OTP codes are written to this terminal by the Development delivery ("[local-dev-email]").
# Never point MC_LOCAL_PG at Beta or production.
#
# Usage: bash scripts/run-local-api.sh [--migrate]
# Env:   MC_LOCAL_PG  (default Host=127.0.0.1;Port=55461;Database=mc_local;Username=postgres)
set -euo pipefail

cd "$(dirname "$0")/.."
PG="${MC_LOCAL_PG:-Host=127.0.0.1;Port=55461;Database=mc_local;Username=postgres}"
case "$PG" in
  *Host=127.0.0.1*|*Host=localhost*) ;;
  *) echo "Refusing non-loopback database: local QA uses a disposable local PostgreSQL only." >&2; exit 2 ;;
esac

KEYS="$(mktemp -d)"
trap 'rm -rf "$KEYS"' EXIT
openssl genrsa -out "$KEYS/receipt.pem" 2048 2>/dev/null
openssl rsa -in "$KEYS/receipt.pem" -pubout -out "$KEYS/receipt.pub" 2>/dev/null

export ConnectionStrings__PostgreSql="$PG"
if [[ "${1:-}" == "--migrate" ]]; then
  dotnet run --project src/MuscleCuties.DbMaintenance -- migrate
  dotnet run --project src/MuscleCuties.DbMaintenance -- seed
  dotnet run --project src/MuscleCuties.DbMaintenance -- dbcheck
fi

export ASPNETCORE_ENVIRONMENT=Development DOTNET_ENVIRONMENT=Development
export ASPNETCORE_URLS=http://127.0.0.1:5080
export Authentication__Issuer=MuscleCutiesServer Authentication__Audience=MuscleCutiesApp
export Authentication__SigningKey="$(openssl rand -hex 32)"
export EmailReceipts__ActiveKeyId=local EmailReceipts__Issuer=MuscleCutiesServer EmailReceipts__Audience=MuscleCutiesRegistration
export EmailReceipts__PrivateKeyPem="$(cat "$KEYS/receipt.pem")" EmailReceipts__PublicKeys__local="$(cat "$KEYS/receipt.pub")"
export EmailChallenges__ActiveHmacKeyId=local EmailChallenges__HmacKeys__local="$(openssl rand -hex 32)"

if [[ -f .env && -z "${FDC_API_KEY:-}" ]]; then
  FDC_KEY_FROM_ENV="$(grep -E '^[ \t]*FDC_API_KEY[ \t]*=' .env | head -n 1 | sed -E 's/^[ \t]*FDC_API_KEY[ \t]*=[ \t]*["'\'']?([^"'\'']*)["'\'']?/\1/' || true)"
  if [[ -n "$FDC_KEY_FROM_ENV" ]]; then
    export FDC_API_KEY="$FDC_KEY_FROM_ENV"
  fi
fi

# Run from the project folder so appsettings.Development.json is the content root.
cd services/MuscleCuties.Server
dotnet run --no-launch-profile
