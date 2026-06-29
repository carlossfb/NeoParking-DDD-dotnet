#!/bin/bash
set -e

HOST="${MYSQL_HOST:-mysql}"
PORT="${MYSQL_PORT:-3306}"

echo "⏳ Waiting for MySQL at ${HOST}:${PORT}..."

until (echo > /dev/tcp/${HOST}/${PORT}) 2>/dev/null; do
  echo "  not ready yet, retrying in 2s..."
  sleep 2
done

echo "✅ MySQL is ready."
echo "🚀 Starting NeoParking API (migrations run on startup)..."

exec dotnet NeoParking.Api.dll
