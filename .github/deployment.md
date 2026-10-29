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

The Entra service principal needs `Website Contributor` on the API App Service (or its resource group).

## GitHub Pages

Configure `Settings > Pages > Build and deployment > Source` to use **GitHub Actions** and set `fanta.zone` as the custom domain. The frontend is built for the domain root and GitHub Pages manages the TLS certificate.

The client secret should have an explicit expiration and be rotated. An infinite-duration secret is deliberately not recommended because it creates a permanent deployment credential.

Rotate every credential that was previously committed to the repository, including storage, OAuth, OpenAI, encryption, App Insights, and Static Web App credentials. Removing a value from Git history does not revoke it.

## New PostgreSQL environment `fcv2` (Terraform)

The `fcv2` resource group (Container Registry, container App Service, PostgreSQL Flexible Server, Storage with the private cache container, Key Vault, Log Analytics/Application Insights, Azure OpenAI `gpt-6-luna`) is defined in [`infra/`](../infra/README.md) and created by the `Infrastructure fcv2` workflow: plan runs automatically, apply only via `workflow_dispatch` with `action=apply` and `confirm=fcv2`. The apply creates the registry, builds and pushes the API image, creates the rest of the environment and deploys the image. `Deploy API fcv2` later builds, pushes and deploys new images without changing the Terraform-managed settings.

Both workflows reuse `AZURE_CREDENTIALS`, `MICROSOFT_CLIENT_SECRET`, `ENCRYPTION_KEY` and `ENCRYPTION_IV` from the `production` environment. The service principal needs `Contributor` on the subscription plus `Role Based Access Control Administrator` constrained to the `AcrPull` role (today it only has `Website Contributor` on `apifc`); see `infra/README.md` for the exact commands. The existing `fc`/`apifc` deployment is unchanged.
