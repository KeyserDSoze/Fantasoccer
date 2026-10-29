resource "azurerm_cognitive_account" "openai" {
  count                         = var.openai_enabled ? 1 : 0
  name                          = local.names.openai
  resource_group_name           = azurerm_resource_group.main.name
  location                      = var.openai_location
  kind                          = "OpenAI"
  sku_name                      = "S0"
  custom_subdomain_name         = local.names.openai
  local_auth_enabled            = true
  public_network_access_enabled = true
  tags                          = local.tags
}

resource "azurerm_cognitive_deployment" "editorial" {
  count                  = var.openai_enabled ? 1 : 0
  name                   = var.openai_deployment_name
  cognitive_account_id   = azurerm_cognitive_account.openai[0].id
  version_upgrade_option = "OnceNewDefaultVersionAvailable"

  model {
    format  = "OpenAI"
    name    = var.openai_model_name
    version = var.openai_model_version
  }

  sku {
    name     = var.openai_sku_name
    capacity = var.openai_capacity
  }
}
