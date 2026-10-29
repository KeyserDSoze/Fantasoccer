data "azurerm_client_config" "current" {}

locals {
  # Deterministic per subscription and environment: re-running the workflow
  # always targets the same globally unique names.
  suffix = var.name_suffix != "" ? var.name_suffix : substr(sha256("${data.azurerm_client_config.current.subscription_id}/${var.environment_name}"), 0, 6)

  names = {
    log_analytics = "log-${var.environment_name}"
    app_insights  = "appi-${var.environment_name}"
    storage       = "st${var.environment_name}${local.suffix}"
    key_vault     = "kv-${var.environment_name}-${local.suffix}"
    identity      = "id-${var.environment_name}-api"
    postgres      = "psql-${var.environment_name}-${local.suffix}"
    openai        = "oai-${var.environment_name}-${local.suffix}"
    registry      = "cr${var.environment_name}${local.suffix}"
    plan          = "asp-${var.environment_name}"
    api           = var.api_app_name != "" ? var.api_app_name : "app-${var.environment_name}-api-${local.suffix}"
  }

  tags = merge({
    application = "fantasoccer"
    environment = var.environment_name
    managed-by  = "terraform"
  }, var.tags)
}

resource "azurerm_resource_group" "main" {
  name     = var.resource_group_name
  location = var.location
  tags     = local.tags
}
