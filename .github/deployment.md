# GitHub deployment setup

Both workflows use the `production` GitHub Environment and deploy from `main`.

## Environment variables

Create these **Variables** in `Settings > Environments > production`:

- `API_RESOURCE_GROUP`: resource group containing the API App Service
- `API_APP_NAME`: API App Service name, historically `fcv4` or `apifc`
- `FRONTEND_API_URL`: public API URL, for example `https://apifc.azurewebsites.net`

Create these **Secrets** in the same environment:

- `AZURE_CREDENTIALS`: JSON containing `clientId`, `clientSecret`, `tenantId`, and `subscriptionId` for the Entra application
- `STATIC_WEB_APP_DEPLOYMENT_TOKEN`: deployment token from the Static Web App
- `API_STORAGE_CONNECTION_STRING`: Azure Storage connection string
- `API_APPINSIGHTS_CONNECTION_STRING`: Application Insights connection string
- `MICROSOFT_CLIENT_SECRET`: Microsoft social-login client secret
- `OPENAI_API_KEY`: Azure OpenAI API key
- `ENCRYPTION_KEY`: application encryption key
- `ENCRYPTION_IV`: application encryption IV

For the current Azure target, set `API_APP_NAME=apifc`, `API_RESOURCE_GROUP=fc`, and `FRONTEND_API_URL=https://apifc.azurewebsites.net`.

The Entra service principal needs `Website Contributor` on the API App Service (or its resource group). The Static Web App token is scoped to that Static Web App and is only used by the static-app workflow.

The client secret should have an explicit expiration and be rotated. An infinite-duration secret is deliberately not recommended because it creates a permanent deployment credential.

Rotate every credential that was previously committed to the repository, including storage, OAuth, OpenAI, encryption, App Insights, and Static Web App credentials. Removing a value from Git history does not revoke it.