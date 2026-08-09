#!/usr/bin/env bash
# Collect Cobertura coverage for DataGateMonitor.Tests (excludes nothing by default;
# interpret Main API % separately from DataGateMonitor.DataBase/Migrations).
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="${1:-/tmp/datagatemonitor-coverage}"
mkdir -p "$OUT"
dotnet test "$ROOT/DataGateMonitor.Tests/DataGateMonitor.Tests.csproj" \
  --collect:"XPlat Code Coverage" \
  --results-directory "$OUT" \
  -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura
echo "Coverage results under: $OUT"
find "$OUT" -name 'coverage.cobertura.xml' -print
