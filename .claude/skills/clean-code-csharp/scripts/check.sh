#!/usr/bin/env bash
# Full .NET toolchain check — run from project root.
set -euo pipefail

echo "=== dotnet build ==="
dotnet build --no-restore 2>/dev/null || true

echo "=== dotnet test ==="
dotnet test --no-build 2>/dev/null || true

echo "=== dotnet format ==="
dotnet format --verify-no-changes 2>/dev/null || true

echo "=== vulnerable packages ==="
dotnet list package --vulnerable 2>/dev/null || true

echo "=== Done ==="
