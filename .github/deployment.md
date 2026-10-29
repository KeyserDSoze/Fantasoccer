# GitHub deployment setup

Both workflows deploy from `main`. The API build uses the `production` GitHub Environment; the frontend deploy uses the GitHub-managed `github-pages` environment.

The API workflow currently skips the solution test step while the test project is being repaired. Re-enable it before treating the production deployment as fully validated.

## Environment variables

## Production Azure target

Keep these identifiers available when querying the production App Service or Application Insights with Azure CLI:

- Tenant ID: `6998af00-286c-4e5e-8b3e-713471e8487f`
- Subscription ID: `c46eb61c-c951-471f-aa59-bf20aacf4d75`
- Resource group: `fc`
- API App Service: `apifc`
- API URL: `https://apifc.azurewebsites.net`

Create these **Variables** in `Settings > Environments > production`:

- `API_RESOURCE_GROUP`: resource group containing the API App Service
- `API_APP_NAME`: API App Service name, historically `fcv4` or `apifc`
- `FRONTEND_API_URL`: public API URL, for example `https://apifc.azurewebsites.net`

Create these **Secrets** in the same environment:

- `AZURE_CREDENTIALS`: JSON containing `clientId`, `clientSecret`, `tenantId`, and `subscriptionId` for the Entra application
- `API_STORAGE_CONNECTION_STRING`: Azure Storage connection string
- `API_APPINSIGHTS_CONNECTION_STRING`: Application Insights connection string
- `MICROSOFT_CLIENT_SECRET`: Microsoft social-login client secret
- `OPENAI_API_KEY`: Azure OpenAI API key
- `ENCRYPTION_KEY`: application encryption key
- `ENCRYPTION_IV`: application encryption IV

For the current targets, set `API_APP_NAME=apifc`, `API_RESOURCE_GROUP=fc`, and `FRONTEND_API_URL=https://apifc.azurewebsites.net`.

The Entra service principal needs at least `Website Contributor` on the API App Service (or its resource group) for this workflow; it is currently Owner of the subscription, which the Terraform workflow for `fcv2` relies on.

## GitHub Pages

Configure `Settings > Pages > Build and deployment > Source` to use **GitHub Actions** and set `fanta.zone` as the custom domain. The frontend is built for the domain root and GitHub Pages manages the TLS certificate.

The client secret should have an explicit expiration and be rotated. An infinite-duration secret is deliberately not recommended because it creates a permanent deployment credential.

Rotate every credential that was previously committed to the repository, including storage, OAuth, OpenAI, encryption, App Insights, and Static Web App credentials. Removing a value from Git history does not revoke it.

## New PostgreSQL environment `fcv2` (Terraform)

The `fcv2` resource group (Container Registry, container App Service, PostgreSQL Flexible Server, Storage with the private cache container, Key Vault, Log Analytics/Application Insights, Azure OpenAI `gpt-6-luna`) is defined in [`infra/`](../infra/README.md) and created by the `Infrastructure fcv2` workflow: plan runs automatically, apply only via `workflow_dispatch` with `action=apply` and `confirm=fcv2`. The apply creates the registry, builds and pushes the API image, creates the rest of the environment and deploys the image. `Deploy API fcv2` later builds, pushes and deploys new images without changing the Terraform-managed settings.

Both workflows reuse `AZURE_CREDENTIALS`, `MICROSOFT_CLIENT_SECRET`, `ENCRYPTION_KEY` and `ENCRYPTION_IV` from the `production` environment. They authenticate with the same client secret as today: the app registration is Owner of the subscription, which covers creating the resource groups, registering resource providers, pushing images and assigning `AcrPull` to the API identity. The apply checks these permissions before creating anything. The existing `fc`/`apifc` deployment is unchanged.

## Cutover to fcv2 and rollback

- The API of the PostgreSQL version is `https://apifcv2.azurewebsites.net` (`fcv2.azurewebsites.net` belongs to a third party). The frontend stays on GitHub Pages (`fanta.zone`).
- Once `refactor/postgresql-multi-group` is merged into `main`:
  - `Deploy API fcv2` deploys the commit that passed `Relational Migration Tests`;
  - `Deploy GitHub Pages` publishes the new frontend built against `apifcv2`, only after `apifcv2/health/ready` answers;
  - the old `Deploy API` on `main` refuses to run, so `apifc` is never overwritten.
- `rollback-main` is an exact copy of `main` before the cutover (`9ee2c0a`). It is protected against deletion and force pushes and allowed by the `github-pages` environment. To roll back, run `Deploy GitHub Pages` (and, only if needed, `Deploy API`) choosing the `rollback-main` branch: GitHub executes that branch's original workflows, which build against `FRONTEND_API_URL` and deploy to `API_APP_NAME`/`API_RESOURCE_GROUP`: keep these variables on the legacy values (`https://apifc.azurewebsites.net`, `apifc`, `fc`).
- Details and caveats (no dual write: data written after the cutover is not in the legacy storage): [`infra/README.md`](../infra/README.md#passaggio-in-produzione-e-rollback).
