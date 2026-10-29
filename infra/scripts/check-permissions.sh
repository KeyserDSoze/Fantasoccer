#!/usr/bin/env bash
# Verifies that the deployment principal (AZURE_CREDENTIALS) can create the
# environment and the AcrPull role assignment: Owner, or Contributor plus a
# role assignment administrator role, on the subscription.
#   warn    : annotation only (plan)
#   require : fails before anything is created (apply)
# Uses only ARM (no Microsoft Graph permission needed).
set -euo pipefail
mode=${1:-warn}
subscription=${ARM_SUBSCRIPTION_ID:?ARM_SUBSCRIPTION_ID is required}

# Object ID of the signed-in principal from the access token (base64url payload).
payload=$(az account get-access-token --query accessToken --output tsv --only-show-errors | cut -d. -f2 | tr '_-' '/+')
while (( ${#payload} % 4 )); do payload+="="; done
object_id=$(base64 -d <<<"$payload" | jq -r '.oid')

roles=$(az role assignment list --assignee-object-id "$object_id" --scope "/subscriptions/$subscription" --include-inherited \
  --fill-principal-name false --query "[].roleDefinitionName" --output tsv --only-show-errors 2>/dev/null | sort -u || true)
echo "Deployment principal roles on the subscription: $(paste -sd, - <<<"${roles:-none}")"

if grep -qx 'Owner' <<<"$roles" ||
  { grep -qx 'Contributor' <<<"$roles" && grep -qxE 'Role Based Access Control Administrator|User Access Administrator' <<<"$roles"; }; then
  echo "Permissions are sufficient for the fcv2 environment."
  exit 0
fi

message="The AZURE_CREDENTIALS principal needs Owner on the subscription (or Contributor plus a role assignment administrator role)."
if [[ "$mode" == "require" ]]; then
  echo "::error::$message"
  exit 1
fi
echo "::warning::$message"
