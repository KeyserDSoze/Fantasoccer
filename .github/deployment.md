# GitHub deployment setup

Both workflows deploy from `main`. The API build uses the `production` GitHub Environment; the frontend deploy uses the GitHub-managed `github-pages` environment.

The API workflow currently skips the solution test step while the test project is being repaired. Re-enable it before treating the production deployment as fully validated.

## Environment variables

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
