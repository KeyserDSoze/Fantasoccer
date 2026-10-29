#!/usr/bin/env bash
# Points the fcv2 App Service to <repository>:<tag> (already pushed) and waits
# for /health/live. Only the image changes: settings stay owned by Terraform,
# and the pull keeps using the managed identity configured by Terraform.
set -euo pipefail
tag=${1:?usage: deploy-api-image.sh <tag>}
group=${RESOURCE_GROUP:-fcv2}
repository=${API_IMAGE_REPOSITORY:-fantasoccer-api}

app=$(az webapp list --resource-group "$group" --query "[?tags.component=='api'].name | [0]" --output tsv --only-show-errors)
registry=$(az acr list --resource-group "$group" --query "[?tags.component=='api-registry'].loginServer | [0]" --output tsv --only-show-errors)
if [[ -z "$app" || -z "$registry" ]]; then
  echo "App Service or registry missing in $group: run the infrastructure apply first." >&2
  exit 1
fi
image="DOCKER|$registry/$repository:$tag"
current=$(az webapp config show --resource-group "$group" --name "$app" --query linuxFxVersion --output tsv --only-show-errors)
if [[ "$current" != "$image" ]]; then
  az webapp config set --resource-group "$group" --name "$app" --linux-fx-version "$image" --only-show-errors --output none
  echo "Deployed $registry/$repository:$tag to $app"
  # Let App Service stop the previous container before probing the new one.
  sleep 30
else
  echo "$app already runs $registry/$repository:$tag"
fi

host=$(az webapp show --resource-group "$group" --name "$app" --query defaultHostName --output tsv --only-show-errors)
# First start also pulls the image and applies the EF Core migrations.
for _ in $(seq 1 36); do
  if curl --silent --fail --max-time 10 "https://$host/health/live" >/dev/null; then
    echo "API live on https://$host"
    exit 0
  fi
  sleep 10
done
echo "https://$host/health/live did not answer within 6 minutes." >&2
exit 1
