#!/usr/bin/env bash
set -euo pipefail
root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)"
env_file="${1:-$root/.env}"
[[ "$env_file" = /* ]] || env_file="$root/$env_file"
[[ -f "$env_file" ]] || { echo 'Env file missing.' >&2; exit 1; }
[[ "${DUMP_NAME:-}" =~ ^[a-zA-Z0-9][a-zA-Z0-9_-]*$ ]] || { echo 'Set a valid DUMP_NAME.' >&2; exit 1; }
if [[ "${USE_EXISTING_DUMP:-false}" == true && ( "${RESUME:-false}" == true || "${SOURCE_QUIESCED:-false}" == true ) ]]; then
  echo 'An existing dump is not re-exported; do not combine USE_EXISTING_DUMP with RESUME/SOURCE_QUIESCED.' >&2; exit 1
fi
if [[ "${RESET_FIRST:-false}" == true ]]; then
  [[ "${APPLY:-false}" == true ]] || { echo 'RESET_FIRST requires APPLY=true.' >&2; exit 1; }
  [[ "${CONFIRM_DATABASE:-}" == fantasoccer_local ]] || { echo 'Refused. RESET_FIRST also needs CONFIRM_DATABASE=fantasoccer_local: ALL local application data will be backed up and removed before the import.' >&2; exit 1; }
fi
if [[ "${WHAT_IF:-false}" == true ]]; then
  echo "Plan: Blob export (unless USE_EXISTING_DUMP), verify and analyze migration-data/$DUMP_NAME. INCLUDE_TABLES=${INCLUDE_TABLES:-false}; APPLY=${APPLY:-false} adds explicit local SQL import and file restore, never a reset/restart. No commands executed."
  exit 0
fi
[[ -z "${DOCKER_HOST:-}" || "$DOCKER_HOST" == unix://* || "$DOCKER_HOST" == npipe://* ]] || { echo 'Remote DOCKER_HOST refused.' >&2; exit 1; }
endpoint="$(docker context inspect --format '{{.Endpoints.docker.Host}}')"
[[ "$endpoint" == unix://* || "$endpoint" == npipe://* ]] || { echo 'Only local Docker socket/named-pipe contexts are allowed.' >&2; exit 1; }
export DUMP_NAME
export INCLUDE_TABLES="${INCLUDE_TABLES:-false}"
if [[ "${USE_EXISTING_DUMP:-false}" != true ]]; then
  echo 'Reading source from private FANTASOCCER_EXPORT_STORAGE (never printed); Blob-only by default, Table requires INCLUDE_TABLES=true. No Azure writes.'
  bash "$root/scripts/local/local.sh" export "$env_file"
fi
bash "$root/scripts/local/local.sh" verify "$env_file"
bash "$root/scripts/local/local.sh" analyze-core "$env_file"
if [[ "${APPLY:-false}" != true ]]; then
  echo 'Snapshot analyzed only. Review pendingContainers/pendingFields/preservedArtifacts. USE_EXISTING_DUMP=true APPLY=true explicitly imports it.'
  exit 0
fi
if [[ "${RESET_FIRST:-false}" == true ]]; then
  bash "$root/scripts/local/reset-postgres.sh" "$env_file"
fi
echo 'Stop host debug processes. Live-source export is exploratory; imports after app writes are refused. No automatic reset.'
bash "$root/scripts/local/local.sh" import-core "$env_file"
bash "$root/scripts/local/local.sh" restore "$env_file"
echo 'Local SQL and Azurite updated. Apps remain stopped. Review reports before restarting; this is not continuous sync or production cutover.'
