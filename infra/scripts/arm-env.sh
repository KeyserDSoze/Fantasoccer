#!/usr/bin/env bash
# Exposes the existing app registration (GitHub secret AZURE_CREDENTIALS, the
# JSON used by azure/login) as the ARM_* variables read by Terraform.
set -euo pipefail
: "${AZURE_CREDENTIALS:?AZURE_CREDENTIALS is required}"
: "${GITHUB_ENV:?run inside GitHub Actions}"

for pair in clientId:ARM_CLIENT_ID clientSecret:ARM_CLIENT_SECRET tenantId:ARM_TENANT_ID subscriptionId:ARM_SUBSCRIPTION_ID; do
  field=${pair%%:*}
  value=$(jq -er --arg field "$field" '.[$field] // empty' <<<"$AZURE_CREDENTIALS") || {
    echo "AZURE_CREDENTIALS does not contain $field." >&2
    exit 1
  }
  echo "::add-mask::$value"
  echo "${pair#*:}=$value" >>"$GITHUB_ENV"
done
