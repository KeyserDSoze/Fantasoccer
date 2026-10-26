#!/usr/bin/env bash
set -euo pipefail

root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)"
action="${1:-help}"
env_file="${2:-$root/.env}"
[[ "$env_file" = /* ]] || env_file="$root/$env_file"
if [[ "$action" == init ]]; then
  [[ ! -e "$env_file" ]] || { echo 'Env file exists; not overwritten.' >&2; exit 1; }
  umask 077
  python3 - "$root/.env.test" "$env_file" <<'PY'
import base64, pathlib, re, secrets, sys
text = pathlib.Path(sys.argv[1]).read_text()
for name, length in [('POSTGRES_PASSWORD', 32), ('DASHBOARD_TOKEN', 32), ('ENCRYPTION_KEY', 32), ('ENCRYPTION_IV', 16), ('LOCAL_HTTPS_CERT_PASSWORD', 32)]:
    value = base64.b64encode(secrets.token_bytes(length)).decode()
    text = re.sub(rf'(?m)^{name}=.*$', f'{name}={value}', text)
with open(sys.argv[2], 'x') as file:
    file.write(text)
print('Private env file created with generated local keys. Never put real secrets in .env.test.')
PY
  exit 0
fi
[[ -f "$env_file" ]] || { echo 'Env file missing: run init or explicitly select .env.test.' >&2; exit 1; }
compose=(docker compose --env-file "$env_file" -f "$root/compose.yml")
has_certificate=false
if [[ -f "$root/local-certificates/localhost.pfx" && "$action" != certificate ]]; then
  compose+=(-f "$root/compose.https.yml")
  has_certificate=true
fi
dump_name="${DUMP_NAME:-current}"
[[ "$dump_name" =~ ^[a-zA-Z0-9][a-zA-Z0-9_-]*$ ]] || { echo 'Invalid DUMP_NAME.' >&2; exit 1; }
case "$action" in
  up) "${compose[@]}" up -d --build --wait frontend api postgres azurite dashboard ;;
  up-https)
    [[ "$has_certificate" == true ]] || { echo 'Run certificate first, then explicitly trust the public localhost.cer/localhost.pem on your own machine.' >&2; exit 1; }
    "${compose[@]}" up -d --build --wait frontend api postgres azurite dashboard
    ;;
  certificate)
    umask 077
    mkdir -p "$root/local-certificates"
    "${compose[@]}" --profile tools run --rm --build --no-deps certificates
    ;;
  down) "${compose[@]}" --profile test --profile tools down ;;
  test)
    "${compose[@]}" --profile test run --rm --build tests
    "${compose[@]}" --profile test run --rm --build frontend-tests
    ;;
  export)
    flags=()
    [[ "${RESUME:-false}" != true ]] || flags+=(--resume)
    [[ "${SOURCE_QUIESCED:-false}" != true ]] || flags+=(--source-quiesced)
    "${compose[@]}" --profile tools run --rm --build storage-export export --directory "/dumps/$dump_name" --allow-remote-source "${flags[@]}"
    ;;
  verify) "${compose[@]}" --profile tools run --rm --build storage-verify verify --directory "/dumps/$dump_name" ;;
  restore)
    "${compose[@]}" stop frontend api
    "${compose[@]}" --profile tools run --rm --build storage-restore restore-local --directory "/dumps/$dump_name" --public-local-files
    echo 'Local legacy storage restored. Use analyze-core/import-core for the PARTIAL relational import; the game migration is not complete.'
    ;;
  migrate) "${compose[@]}" --profile tools run --rm --build migrate ;;
  analyze-core) "${compose[@]}" --profile tools run --rm --build --no-deps import-core analyze-core --directory "/dumps/$dump_name" ;;
  import-core)
    "${compose[@]}" stop frontend api
    "${compose[@]}" --profile tools run --rm --build import-core import-core --directory "/dumps/$dump_name"
    echo 'Core structure imported. Check pendingContainers/pendingFields: this is NOT a complete migration of the game.'
    ;;
  logs) "${compose[@]}" logs --tail 100 api ;;
  smoke)
    if [[ "$has_certificate" == true ]]; then
      "${compose[@]}" exec -T api curl --fail --silent --cacert /https/localhost.pem https://localhost:8443/health/ready
    else
      "${compose[@]}" exec -T api curl --fail --silent http://localhost:8080/health/ready
    fi
    "${compose[@]}" exec -T frontend wget -q -O /dev/null http://127.0.0.1/health
    if [[ "$has_certificate" == true ]]; then
      "${compose[@]}" exec -T api curl --fail --silent --cacert /https/localhost.pem --output /dev/null https://localhost:8443/swagger/index.html
    else
      "${compose[@]}" exec -T api curl --fail --silent --output /dev/null http://localhost:8080/swagger/index.html
    fi
    "${compose[@]}" ps
    ;;
  *) echo 'Usage: local.sh init|up|up-https|certificate|down|test|export|verify|restore|migrate|analyze-core|import-core|smoke|logs [env-file]' >&2; exit 2 ;;
esac
