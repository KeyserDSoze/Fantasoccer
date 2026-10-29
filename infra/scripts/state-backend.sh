#!/usr/bin/env bash
# Terraform remote state on a dedicated storage account, outside the resource
# group managed by Terraform (destroying the environment never touches it).
#   check  : writes backend.hcl and exists=true|false (no change on Azure)
#   ensure : creates resource group, storage account and container if missing
# Requires an authenticated az CLI and ARM_SUBSCRIPTION_ID.
set -euo pipefail

mode=${1:-check}
environment=${TF_ENVIRONMENT_NAME:-fcv2}
location=${TF_STATE_LOCATION:-swedencentral}
subscription=${ARM_SUBSCRIPTION_ID:?ARM_SUBSCRIPTION_ID is required}
group=${TF_STATE_RESOURCE_GROUP:-${environment}-tfstate}
# Deterministic, globally unique and valid storage account name (3-24 lowercase alphanumerics).
account=${TF_STATE_STORAGE_ACCOUNT:-${environment}tf$(printf '%s' "$subscription/$environment" | sha256sum | cut -c1-10)}
container=tfstate
here=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)

az account set --subscription "$subscription" --only-show-errors

exists=false
if az storage account show --name "$account" --resource-group "$group" --only-show-errors --output none 2>/dev/null; then
  exists=true
fi

if [[ "$mode" == "ensure" && "$exists" == "false" ]]; then
  az group create --name "$group" --location "$location" \
    --tags application=fantasoccer environment="$environment" purpose=terraform-state \
    --only-show-errors --output none
  az storage account create --name "$account" --resource-group "$group" --location "$location" \
    --sku Standard_LRS --kind StorageV2 --https-only true --min-tls-version TLS1_2 \
    --allow-blob-public-access false --allow-shared-key-access true \
    --tags application=fantasoccer environment="$environment" purpose=terraform-state \
    --only-show-errors --output none
  # Versions and soft delete let a damaged state be restored.
  az storage account blob-service-properties update --account-name "$account" --resource-group "$group" \
    --enable-versioning true --enable-delete-retention true --delete-retention-days 30 \
    --only-show-errors --output none
  exists=true
fi

if [[ "$exists" == "true" ]]; then
  # --auth-mode key: az reads the account key itself (Contributor), no data-plane role needed.
  az storage container create --name "$container" --account-name "$account" --auth-mode key \
    --public-access off --only-show-errors --output none
fi

cat >"$here/backend.hcl" <<HCL
resource_group_name  = "$group"
storage_account_name = "$account"
container_name       = "$container"
key                  = "$environment.tfstate"
HCL

echo "State backend: $account/$container ($([[ $exists == true ]] && echo ready || echo missing))"
if [[ -n "${GITHUB_OUTPUT:-}" ]]; then
  echo "exists=$exists" >>"$GITHUB_OUTPUT"
fi
