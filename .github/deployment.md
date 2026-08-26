# GitHub deployment setup

Both workflows use the `production` GitHub Environment and deploy from `main`.

## Environment variables

Create these **Variables** in `Settings > Environments > production`:

- `API_RESOURCE_GROUP`: resource group containing the API App Service
- `API_APP_NAME`: API App Service name, historically `fcv4` or `apifc`
- `FRONTEND_API_URL`: public API URL, for example `https://apifc.azurewebsites.net`

Create these **Secrets** in the same environment:

- `AZURE_CLIENT_ID`: Entra application/client ID used by GitHub OIDC
- `AZURE_TENANT_ID`: Entra tenant ID
- `AZURE_SUBSCRIPTION_ID`: target Azure subscription ID
- `STATIC_WEB_APP_DEPLOYMENT_TOKEN`: deployment token from the Static Web App
- `API_STORAGE_CONNECTION_STRING`: Azure Storage connection string
- `API_APPINSIGHTS_CONNECTION_STRING`: Application Insights connection string
- `MICROSOFT_CLIENT_SECRET`: Microsoft social-login client secret
- `OPENAI_API_KEY`: Azure OpenAI API key
- `ENCRYPTION_KEY`: application encryption key
- `ENCRYPTION_IV`: application encryption IV

The OIDC identity needs `Website Contributor` on the API App Service (or its resource group). The Static Web App token is scoped to that Static Web App and is only used by the static-app workflow.

Rotate every credential that was previously committed to the repository, including storage, OAuth, OpenAI, encryption, App Insights, and Static Web App credentials. Removing a value from Git history does not revoke it.