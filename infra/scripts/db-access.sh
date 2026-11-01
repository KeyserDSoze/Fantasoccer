#!/usr/bin/env bash
# Shared helpers for the database backup and restore workflows. Source it after azure/login.
# The runner reaches PostgreSQL through the single "backup-runner" firewall rule: its address is
# updated (never created or deleted, which the resource group lock would refuse) before use and
# parked on an unroutable address afterwards.
set -euo pipefail

RESOURCE_GROUP="${RESOURCE_GROUP:-fcv2}"
PARKED_IP="192.0.2.1"
WORK="${WORK:-$PWD/db-work}"
mkdir -p "$WORK"

db_server() { az postgres flexible-server list -g "$RESOURCE_GROUP" --query "[0].name" -o tsv; }

set_runner_ip() {
  local ip="$1" server subscription
  server="$(db_server)"; subscription="$(az account show --query id -o tsv)"
  az rest --method put --only-show-errors \
    --url "https://management.azure.com/subscriptions/$subscription/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.DBforPostgreSQL/flexibleServers/$server/firewallRules/backup-runner?api-version=2024-08-01" \
    --body "{\"properties\":{\"startIpAddress\":\"$ip\",\"endIpAddress\":\"$ip\"}}" --output none
}
open_firewall()  { set_runner_ip "$(curl -fsS https://api.ipify.org)"; }
close_firewall() { set_runner_ip "$PARKED_IP"; }

# Exports PGHOST, PGPORT, PGUSER, PGPASSWORD, PGDATABASE from the connection string in Key Vault.
load_connection() {
  local vault connection
  vault="$(az keyvault list -g "$RESOURCE_GROUP" --query "[0].name" -o tsv)"
  connection="$(az keyvault secret show --vault-name "$vault" --name postgres-connection-string --query value -o tsv)"
  eval "$(CONNECTION="$connection" python3 - <<'PY'
import os, shlex
parts = dict(item.split("=", 1) for item in os.environ["CONNECTION"].split(";") if "=" in item)
for env, key in (("PGHOST", "Host"), ("PGPORT", "Port"), ("PGUSER", "Username"), ("PGPASSWORD", "Password"), ("PGDATABASE", "Database")):
    print(f"export {env}={shlex.quote(parts[key])}")
PY
)"
  echo "::add-mask::$PGPASSWORD"
}

# PostgreSQL 17 client tools (the server is 17; the runner image ships an older client).
# TLS is required but the server certificate is not verified: the Azure chain is not in the image's CA store.
pg() {
  docker run --rm -i -e PGHOST -e PGPORT -e PGUSER -e PGPASSWORD -e PGDATABASE \
    -e PGSSLMODE=require -v "$WORK:/work" postgres:17 "$@"
}

wait_for_database() {
  local attempt
  for attempt in $(seq 1 24); do
    if pg psql -d postgres -Atc "select 1" >/dev/null 2>&1; then return 0; fi
    sleep 10
  done
  pg psql -d postgres -Atc "select 1" 2>&1 | tail -3 || true
  echo "::error::PostgreSQL did not accept the connection from this runner."
  return 1
}
