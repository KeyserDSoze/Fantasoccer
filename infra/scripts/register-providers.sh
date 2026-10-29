#!/usr/bin/env bash
# Registers the resource providers used by the fcv2 configuration (idempotent).
# Run only by the manual apply: plans never change the subscription.
set -euo pipefail
subscription=${ARM_SUBSCRIPTION_ID:?ARM_SUBSCRIPTION_ID is required}
az account set --subscription "$subscription" --only-show-errors
for namespace in Microsoft.CognitiveServices Microsoft.ContainerRegistry Microsoft.DBforPostgreSQL Microsoft.Insights Microsoft.KeyVault \
  Microsoft.ManagedIdentity Microsoft.OperationalInsights Microsoft.Storage Microsoft.Web; do
  state=$(az provider show --namespace "$namespace" --query registrationState --output tsv --only-show-errors)
  if [[ "$state" != "Registered" ]]; then
    echo "Registering $namespace ($state)"
    az provider register --namespace "$namespace" --wait --only-show-errors
  fi
done
echo "Resource providers registered."
