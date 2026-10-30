#!/usr/bin/env bash
set -euo pipefail

root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)"
compose="$root/ops/migration/compose.yml"

# Only local fixture credentials are used. Never inherit a configured production connection.
docker compose -f "$compose" up -d --wait postgres azurite
export FANTASOCCER_TEST_POSTGRES="Host=127.0.0.1;Port=${FANTASOCCER_POSTGRES_PORT:-15432};Database=fantasoccer_migration_tests;Username=fantasoccer_test;Password=local-test-only"
export FANTASOCCER_TEST_STORAGE="DefaultEndpointsProtocol=http;AccountName=migrationtest;AccountKey=bG9jYWwtdGVzdC1vbmx5;BlobEndpoint=http://127.0.0.1:${FANTASOCCER_BLOB_PORT:-11000}/migrationtest;TableEndpoint=http://127.0.0.1:${FANTASOCCER_TABLE_PORT:-11002}/migrationtest"
export FANTASOCCER_TEST_STORAGE_RESTORE="DefaultEndpointsProtocol=http;AccountName=migrationrestore;AccountKey=bG9jYWwtdGVzdC1vbmx5;BlobEndpoint=http://127.0.0.1:${FANTASOCCER_BLOB_PORT:-11000}/migrationrestore;TableEndpoint=http://127.0.0.1:${FANTASOCCER_TABLE_PORT:-11002}/migrationrestore"
dotnet test "$root/src/Fantasoccer.PostgreSql.Tests/Fantasoccer.PostgreSql.Tests.csproj" --nologo "$@"
dotnet test "$root/src/Fantasoccer.RelationalApi.Tests/Fantasoccer.RelationalApi.Tests.csproj" --nologo "$@"
dotnet test "$root/src/Fantasoccer.Tests/Fantasoccer.Tests.csproj" --nologo "$@"
