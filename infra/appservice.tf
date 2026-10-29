resource "azurerm_service_plan" "main" {
  name                = local.names.plan
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  os_type             = "Linux"
  sku_name            = var.app_service_sku
  tags                = local.tags
}

locals {
  secret_references = merge(
    { for name, secret in azurerm_key_vault_secret.generated : name => "@Microsoft.KeyVault(SecretUri=${secret.versionless_id})" },
    {
      microsoft-client-secret = "@Microsoft.KeyVault(SecretUri=${azurerm_key_vault_secret.microsoft_client_secret.versionless_id})"
      encryption-key          = "@Microsoft.KeyVault(SecretUri=${azurerm_key_vault_secret.encryption_key.versionless_id})"
      encryption-iv           = "@Microsoft.KeyVault(SecretUri=${azurerm_key_vault_secret.encryption_iv.versionless_id})"
    },
  )

  # Every secret is a Key Vault reference; plain values are configuration only.
  app_settings = merge({
    ASPNETCORE_ENVIRONMENT               = "Production"
    WEBSITES_PORT                        = "8080"
    WEBSITES_ENABLE_APP_SERVICE_STORAGE  = "false"
    PostgreSql__Enabled                  = "true"
    Integrations__Infrastructure         = "Storage"
    ConnectionStrings__PostgreSql        = local.secret_references["postgres-connection-string"]
    ConnectionStrings__Storage           = local.secret_references["storage-connection-string"]
    ConnectionStrings__AppInsights       = local.secret_references["appinsights-connection-string"]
    Assets__PublicBlobBaseUrl            = trimsuffix(azurerm_storage_account.main.primary_blob_endpoint, "/")
    Cache__ContainerName                 = var.cache_container_name
    Cache__Namespace                     = var.cache_namespace
    SocialLogin__Microsoft__ClientSecret = local.secret_references["microsoft-client-secret"]
    Encryption__Key                      = local.secret_references["encryption-key"]
    Encryption__IV                       = local.secret_references["encryption-iv"]
    BackgroundJobs__Enabled              = tostring(var.background_jobs_enabled)
    Push__Enabled                        = "false"
    Mcp__Enabled                         = "false"
    # The container file system is not persistent: Data Protection keys (bearer
    # tokens) live in the private mcp-keys container and survive restarts.
    Mcp__DataProtectionBlobName = "${var.environment_name}/data-protection-keys.xml"
    OpenAi__Enabled             = tostring(var.openai_enabled)
    }, var.openai_enabled ? {
    OpenAi__Endpoint  = azurerm_cognitive_account.openai[0].endpoint
    OpenAi__ApiKey    = local.secret_references["openai-api-key"]
    OpenAi__ModelName = azurerm_cognitive_deployment.editorial[0].name
  } : {}, var.extra_app_settings)
}

resource "azurerm_linux_web_app" "api" {
  name                            = local.names.api
  resource_group_name             = azurerm_resource_group.main.name
  location                        = azurerm_resource_group.main.location
  service_plan_id                 = azurerm_service_plan.main.id
  https_only                      = true
  public_network_access_enabled   = true
  key_vault_reference_identity_id = azurerm_user_assigned_identity.api.id
  # SignalR (auctions) keeps a client on the same instance.
  client_affinity_enabled = true
  # Deployment uses Microsoft Entra ID (azure/login), never publishing credentials.
  ftp_publish_basic_authentication_enabled       = false
  webdeploy_publish_basic_authentication_enabled = false

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.api.id]
  }

  site_config {
    always_on                                     = true
    container_registry_use_managed_identity       = true
    container_registry_managed_identity_client_id = azurerm_user_assigned_identity.api.client_id
    websockets_enabled                            = true
    http2_enabled                                 = true
    minimum_tls_version                           = "1.2"
    scm_minimum_tls_version                       = "1.2"
    ftps_state                                    = "Disabled"
    health_check_path                             = "/health/live"
    health_check_eviction_time_in_min             = 10

    # Image of ops/local/api.Dockerfile pushed by the workflow before this resource is created.
    application_stack {
      docker_image_name   = "${var.api_image_repository}:${var.api_image_tag}"
      docker_registry_url = "https://${azurerm_container_registry.api.login_server}"
    }
  }

  app_settings = local.app_settings

  tags = merge(local.tags, { component = "api" })

  lifecycle {
    # Deployments set the image tag (scripts/deploy-api-image.sh); Terraform only creates the first one.
    ignore_changes = [site_config[0].application_stack[0].docker_image_name]
  }

  depends_on = [azurerm_key_vault_access_policy.api, azurerm_role_assignment.api_acr_pull]
}
