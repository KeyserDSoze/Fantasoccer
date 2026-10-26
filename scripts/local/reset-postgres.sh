#!/usr/bin/env bash
set -euo pipefail
root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)"
env_file="${1:-$root/.env}"
[[ "$env_file" = /* ]] || env_file="$root/$env_file"
[[ -f "$env_file" ]] || { echo 'Env file missing.' >&2; exit 1; }
[[ "${CONFIRM_DATABASE:-}" == fantasoccer_local ]] || { echo 'Refused. Set CONFIRM_DATABASE=fantasoccer_local explicitly. ALL local application rows will be removed.' >&2; exit 1; }
if [[ "${WHAT_IF:-false}" == true ]]; then
  echo 'Plan: stop API/frontend; back up local Compose postgres/fantasoccer_local; clear app rows, preserving schema and EF history. No Docker commands executed.'
  exit 0
fi
[[ -z "${DOCKER_HOST:-}" || "$DOCKER_HOST" == unix://* || "$DOCKER_HOST" == npipe://* ]] || { echo 'Remote DOCKER_HOST refused.' >&2; exit 1; }
endpoint="$(docker context inspect --format '{{.Endpoints.docker.Host}}')"
[[ "$endpoint" == unix://* || "$endpoint" == npipe://* ]] || { echo 'Only local Docker socket/named-pipe contexts are allowed.' >&2; exit 1; }
compose=(docker compose --env-file "$env_file" -f "$root/compose.yml")
[[ ! -f "$root/local-certificates/localhost.pfx" ]] || compose+=(-f "$root/compose.https.yml")
echo 'Target: local Compose postgres/fantasoccer_local. Stop any host dotnet/debug process first.'
"${compose[@]}" stop frontend api
"${compose[@]}" up -d --wait postgres
running="$("${compose[@]}" ps --status running --services)"
if grep -Eq '^(migrate|import-core|storage-restore)$' <<<"$running"; then echo 'Finish active migration/restore tools first.' >&2; exit 1; fi
umask 077
backup_dir="$root/migration-data/postgres-backups"
mkdir -p "$backup_dir"
chmod 700 "$backup_dir"
file="before-reset-$(date -u +%Y%m%d-%H%M%S)-$(od -An -N8 -tx1 /dev/urandom | tr -d ' \n').dump"
temporary="/tmp/$file"
"${compose[@]}" exec -T postgres pg_dump -U fantasoccer_local -d fantasoccer_local -Fc -f "$temporary"
"${compose[@]}" cp "postgres:$temporary" "$backup_dir/$file"
[[ -s "$backup_dir/$file" ]] || { echo 'Backup absent/empty; no reset performed.' >&2; exit 1; }
chmod 600 "$backup_dir/$file"
"${compose[@]}" exec -T postgres pg_restore --list "$temporary" > /dev/null
"${compose[@]}" exec -T postgres psql -X --set ON_ERROR_STOP=1 -U fantasoccer_local -d fantasoccer_local < "$root/scripts/local/reset-postgres.sql"
"${compose[@]}" exec -T postgres rm -- "$temporary"
echo "Local application data cleared. Backup: $backup_dir/$file"
echo 'Schema/EF history, Azurite, certificates, key rings, telemetry and dumps are preserved. API/frontend remain stopped. Import before enabling logins/MCP/jobs.'
