#!/usr/bin/env bash
# Builds the API image (ops/local/api.Dockerfile) and pushes it to the fcv2
# registry as <repository>:<tag>. An existing tag is not rebuilt (idempotent).
# Requires an authenticated az CLI with push rights (Contributor) and Docker.
set -euo pipefail
tag=${1:?usage: push-api-image.sh <tag>}
group=${RESOURCE_GROUP:-fcv2}
repository=${API_IMAGE_REPOSITORY:-fantasoccer-api}
root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)

registry=$(az acr list --resource-group "$group" --query "[?tags.component=='api-registry'].name | [0]" --output tsv --only-show-errors)
if [[ -z "$registry" ]]; then
  echo "No container registry in $group: run the infrastructure apply first." >&2
  exit 1
fi
server=$(az acr show --name "$registry" --query loginServer --output tsv --only-show-errors)
image="$server/$repository:$tag"

if az acr repository show --name "$registry" --image "$repository:$tag" --only-show-errors --output none 2>/dev/null; then
  echo "Image already in the registry: $image"
else
  az acr login --name "$registry" --only-show-errors
  docker build --file "$root/ops/local/api.Dockerfile" --tag "$image" \
    --label "org.opencontainers.image.revision=$tag" --label "org.opencontainers.image.title=$repository" "$root"
  docker push "$image"
  echo "Pushed $image"
fi
if [[ -n "${GITHUB_OUTPUT:-}" ]]; then
  echo "image=$image" >>"$GITHUB_OUTPUT"
fi
