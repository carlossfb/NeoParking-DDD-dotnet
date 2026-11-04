#!/usr/bin/env bash
# Lê a saída JSON de `dotnet list package --vulnerable --include-transitive --format json`.
# Falha em severidade alta ou crítica fora de tests/. Nos projetos de teste, só avisa.
set -euo pipefail

report="${1:?usage: check-vulnerable-packages.sh <report.json>}"

# Relatório inválido ou sem projetos não pode passar como "sem vulnerabilidades".
if ! jq -e '(.projects | type == "array" and length > 0) and ((.problems // []) | map(select(.level == "error")) | length == 0)' "$report" > /dev/null; then
  echo "::error::Vulnerability report is invalid, empty or has errors."
  exit 1
fi

findings() {
  jq -r '
    .projects[]
    | .path as $path
    | (.frameworks // [])[]
    | ((.topLevelPackages // []) + (.transitivePackages // []))[]
    | . as $package
    | (.vulnerabilities // [])[]
    | select(.severity == "High" or .severity == "Critical")
    | [(if ($path | test("/tests/NeoParking")) then "test" else "production" end),
       .severity, $package.id, $package.resolvedVersion, .advisoryurl]
    | @tsv
  ' "$report" | sort -u
}

all="$(findings)"
production="$(grep -P '^production\t' <<< "$all" || true)"
test_only="$(grep -P '^test\t' <<< "$all" || true)"

if [[ -n "$test_only" ]]; then
  while IFS=$'\t' read -r _ severity id version url; do
    echo "::warning::Test dependency $id $version: $severity $url"
  done <<< "$test_only"
fi

if [[ -n "$production" ]]; then
  while IFS=$'\t' read -r _ severity id version url; do
    echo "::error::Production dependency $id $version: $severity $url"
  done <<< "$production"
  exit 1
fi

echo "No high or critical vulnerability in production dependencies."
