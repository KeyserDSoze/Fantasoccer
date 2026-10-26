#!/usr/bin/env bash
set -euo pipefail

root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)"
umask 077
# Credentials are read by the tool from FANTASOCCER_EXPORT_STORAGE, never passed as command-line arguments.
dotnet run --project "$root/tools/Fantasoccer.StorageMigration/Fantasoccer.StorageMigration.csproj" -- export "$@"
