#!/usr/bin/env bash
# One documented command for the all-route server latency gate.
# Requires the disposable PostgreSQL fixture in MC_LATENCY_POSTGRES.
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

export DOTNET_ROOT="${DOTNET_ROOT:-/tmp/dotnet-10.0.403}"
export PATH="$DOTNET_ROOT:$PATH"
: "${MC_LATENCY_POSTGRES:?Set MC_LATENCY_POSTGRES to the disposable PostgreSQL fixture connection string.}"

export MC_WRITE_LATENCY_REPORT=1
exec dotnet test tests/MuscleCuties.Server.Tests/MuscleCuties.Server.Tests.csproj \
  --nologo -v q \
  --filter "FullyQualifiedName~EveryMappedRouteHasMeasuredColdAndWarmP95WithinBudget"
