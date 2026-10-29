# The API reads its secrets as Key Vault references with a user-assigned
# identity, created before the web app so the first start already resolves them.
resource "azurerm_user_assigned_identity" "api" {
  name                = local.names.identity
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  tags                = local.tags
}

resource "azurerm_key_vault" "main" {
  name                = local.names.key_vault
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  tenant_id           = data.azurerm_client_config.current.tenant_id
  sku_name            = "standard"
  # Access policies: effective immediately, so the first apply can write the
  # secrets without waiting for an RBAC role assignment to propagate.
  rbac_authorization_enabled    = false
  soft_delete_retention_days    = 7
  purge_protection_enabled      = false
  public_network_access_enabled = true
  tags                          = local.tags
}

resource "azurerm_key_vault_access_policy" "deployer" {
  key_vault_id       = azurerm_key_vault.main.id
  tenant_id          = data.azurerm_client_config.current.tenant_id
  object_id          = data.azurerm_client_config.current.object_id
  secret_permissions = ["Get", "List", "Set", "Delete", "Purge", "Recover"]
}

resource "azurerm_key_vault_access_policy" "api" {
  key_vault_id       = azurerm_key_vault.main.id
  tenant_id          = data.azurerm_client_config.current.tenant_id
  object_id          = azurerm_user_assigned_identity.api.principal_id
  secret_permissions = ["Get", "List"]
}

# Values already known to Azure resources (and therefore in the state).
locals {
  generated_secrets = merge({
    postgres-connection-string    = local.postgres_connection_string
    storage-connection-string     = azurerm_storage_account.main.primary_connection_string
    appinsights-connection-string = azurerm_application_insights.main.connection_string
    }, var.openai_enabled ? {
    openai-api-key = azurerm_cognitive_account.openai[0].primary_access_key
  } : {})
}

resource "azurerm_key_vault_secret" "generated" {
  for_each     = nonsensitive(toset(keys(local.generated_secrets)))
  name         = each.key
  value        = local.generated_secrets[each.key]
  key_vault_id = azurerm_key_vault.main.id
  content_type = "text/plain"
  depends_on   = [azurerm_key_vault_access_policy.deployer]
}

# Values coming from GitHub secrets: write-only, never persisted in the state.
resource "azurerm_key_vault_secret" "microsoft_client_secret" {
  name             = "microsoft-client-secret"
  value_wo         = var.microsoft_client_secret
  value_wo_version = var.app_secrets_version
  key_vault_id     = azurerm_key_vault.main.id
  content_type     = "text/plain"
  depends_on       = [azurerm_key_vault_access_policy.deployer]
}

resource "azurerm_key_vault_secret" "encryption_key" {
  name             = "encryption-key"
  value_wo         = var.encryption_key
  value_wo_version = var.app_secrets_version
  key_vault_id     = azurerm_key_vault.main.id
  content_type     = "text/plain"
  depends_on       = [azurerm_key_vault_access_policy.deployer]
}

resource "azurerm_key_vault_secret" "encryption_iv" {
  name             = "encryption-iv"
  value_wo         = var.encryption_iv
  value_wo_version = var.app_secrets_version
  key_vault_id     = azurerm_key_vault.main.id
  content_type     = "text/plain"
  depends_on       = [azurerm_key_vault_access_policy.deployer]
}
