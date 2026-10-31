# Offline tests: the Azure provider is mocked, nothing is created. They verify
# naming, secret handling and the configuration contract with the API.
mock_provider "azurerm" {
  mock_data "azurerm_client_config" {
    defaults = {
      subscription_id = "11111111-1111-1111-1111-111111111111"
      tenant_id       = "22222222-2222-2222-2222-222222222222"
      object_id       = "33333333-3333-3333-3333-333333333333"
      client_id       = "44444444-4444-4444-4444-444444444444"
    }
  }
  mock_resource "azurerm_resource_group" {
    defaults = {
      id = "/subscriptions/11111111-1111-1111-1111-111111111111/resourceGroups/fcv2"
    }
  }
  mock_resource "azurerm_log_analytics_workspace" {
    defaults = {
      id = "/subscriptions/11111111-1111-1111-1111-111111111111/resourceGroups/fcv2/providers/Microsoft.OperationalInsights/workspaces/mock"
    }
  }
  mock_resource "azurerm_service_plan" {
    defaults = {
      id = "/subscriptions/11111111-1111-1111-1111-111111111111/resourceGroups/fcv2/providers/Microsoft.Web/serverFarms/mock"
    }
  }
  mock_resource "azurerm_linux_web_app" {
    defaults = {
      id               = "/subscriptions/11111111-1111-1111-1111-111111111111/resourceGroups/fcv2/providers/Microsoft.Web/sites/mock"
      default_hostname = "mock.azurewebsites.net"
    }
  }
  mock_resource "azurerm_storage_account" {
    defaults = {
      id                        = "/subscriptions/11111111-1111-1111-1111-111111111111/resourceGroups/fcv2/providers/Microsoft.Storage/storageAccounts/mock"
      primary_blob_endpoint     = "https://mock.blob.core.windows.net/"
      primary_connection_string = "DefaultEndpointsProtocol=https;AccountName=mock;AccountKey=bW9jaw==;EndpointSuffix=core.windows.net"
    }
  }
  mock_resource "azurerm_postgresql_flexible_server" {
    defaults = {
      id   = "/subscriptions/11111111-1111-1111-1111-111111111111/resourceGroups/fcv2/providers/Microsoft.DBforPostgreSQL/flexibleServers/mock"
      fqdn = "mock.postgres.database.azure.com"
    }
  }
  mock_resource "azurerm_key_vault" {
    defaults = {
      id        = "/subscriptions/11111111-1111-1111-1111-111111111111/resourceGroups/fcv2/providers/Microsoft.KeyVault/vaults/mock"
      vault_uri = "https://mock.vault.azure.net/"
    }
  }
  mock_resource "azurerm_key_vault_secret" {
    defaults = {
      versionless_id = "https://mock.vault.azure.net/secrets/mock"
    }
  }
  mock_resource "azurerm_user_assigned_identity" {
    defaults = {
      id           = "/subscriptions/11111111-1111-1111-1111-111111111111/resourceGroups/fcv2/providers/Microsoft.ManagedIdentity/userAssignedIdentities/mock"
      principal_id = "55555555-5555-5555-5555-555555555555"
      client_id    = "66666666-6666-6666-6666-666666666666"
    }
  }
  mock_resource "azurerm_container_registry" {
    defaults = {
      id           = "/subscriptions/11111111-1111-1111-1111-111111111111/resourceGroups/fcv2/providers/Microsoft.ContainerRegistry/registries/mock"
      login_server = "mock.azurecr.io"
    }
  }
  mock_resource "azurerm_cognitive_account" {
    defaults = {
      id                 = "/subscriptions/11111111-1111-1111-1111-111111111111/resourceGroups/fcv2/providers/Microsoft.CognitiveServices/accounts/mock"
      endpoint           = "https://mock.openai.azure.com/"
      primary_access_key = "mock-key"
    }
  }
  mock_resource "azurerm_application_insights" {
    defaults = {
      connection_string = "InstrumentationKey=00000000-0000-0000-0000-000000000000"
    }
  }
}

variables {
  api_image_tag           = "0123456789abcdef"
  microsoft_client_secret = "test-client-secret"
  encryption_key          = "test-encryption-key"
  encryption_iv           = "test-encryption-iv"
}

run "default_environment" {
  command = apply

  assert {
    condition     = azurerm_resource_group.main.name == "fcv2"
    error_message = "The environment lives in the fcv2 resource group."
  }
  assert {
    condition     = can(regex("^stfcv2[0-9a-f]{6}$", azurerm_storage_account.main.name)) && length(azurerm_key_vault.main.name) <= 24
    error_message = "Globally unique names must be deterministic and within Azure limits."
  }
  assert {
    condition = alltrue([for name in ["ConnectionStrings__PostgreSql", "ConnectionStrings__Storage", "ConnectionStrings__AppInsights",
      "SocialLogin__Microsoft__ClientSecret", "Encryption__Key", "Encryption__IV", "OpenAi__ApiKey"] :
    startswith(azurerm_linux_web_app.api.app_settings[name], "@Microsoft.KeyVault(SecretUri=")])
    error_message = "Every secret setting must be a Key Vault reference."
  }
  assert {
    condition     = alltrue([for value in values(azurerm_linux_web_app.api.app_settings) : !strcontains(value, "Password=") && !strcontains(value, "AccountKey=")])
    error_message = "No secret value may appear in plain App Service settings."
  }
  assert {
    condition = (!contains(keys(azurerm_linux_web_app.api.app_settings), "PostgreSql__Enabled") &&
      azurerm_linux_web_app.api.app_settings["BackgroundJobs__Enabled"] == "false" &&
      azurerm_linux_web_app.api.app_settings["Cache__ContainerName"] == "fantazone-cache" &&
    azurerm_linux_web_app.api.app_settings["Assets__PublicBlobBaseUrl"] == "https://mock.blob.core.windows.net")
    error_message = "No retired backend selector; cache container, public assets and jobs disabled until the import."
  }
  assert {
    condition = (azurerm_storage_container.main["images"].container_access_type == "blob" &&
      azurerm_storage_container.main["logos"].container_access_type == "blob" &&
      azurerm_storage_container.main["cardimages"].container_access_type == "blob" &&
      azurerm_storage_container.main["fantazone-cache"].container_access_type == "private" &&
    azurerm_storage_container.main["mcp-keys"].container_access_type == "private")
    error_message = "Only images, logos and card images are public."
  }
  assert {
    condition     = nonsensitive(strcontains(azurerm_key_vault_secret.generated["postgres-connection-string"].value, "SSL Mode=VerifyFull"))
    error_message = "PostgreSQL connections must verify the server certificate."
  }
  assert {
    condition     = nonsensitive(strcontains(azurerm_key_vault_secret.generated["postgres-connection-string"].value, "Maximum Pool Size=15"))
    error_message = "The connection pool must stay below the 35 user connections of a B1ms server."
  }
  assert {
    condition     = !contains([for log in azurerm_monitor_diagnostic_setting.api.enabled_log : log.category], "AppServiceHTTPLogs")
    error_message = "HTTP logs keep query strings and must not be sent to Log Analytics."
  }
  assert {
    condition     = azurerm_key_vault.main.rbac_authorization_enabled == false && azurerm_key_vault_access_policy.api.secret_permissions == tolist(["Get", "List"])
    error_message = "The API identity only reads secrets."
  }
  assert {
    condition     = length(azurerm_postgresql_flexible_server_firewall_rule.azure_services) == 1 && length(azurerm_postgresql_flexible_server_firewall_rule.operators) == 0
    error_message = "Only Azure services reach PostgreSQL by default."
  }
  assert {
    condition = (azurerm_linux_web_app.api.site_config[0].application_stack[0].docker_image_name == "fantasoccer-api:0123456789abcdef" &&
      azurerm_linux_web_app.api.site_config[0].application_stack[0].docker_registry_url == "https://mock.azurecr.io" &&
      azurerm_linux_web_app.api.site_config[0].container_registry_use_managed_identity &&
      azurerm_linux_web_app.api.site_config[0].container_registry_managed_identity_client_id == "66666666-6666-6666-6666-666666666666" &&
    azurerm_linux_web_app.api.app_settings["WEBSITES_PORT"] == "8080" && azurerm_linux_web_app.api.site_config[0].websockets_enabled)
    error_message = "The API runs the pushed image from the private registry with its managed identity, port 8080 and WebSockets."
  }
  assert {
    condition = (azurerm_container_registry.api.admin_enabled == false && azurerm_container_registry.api.anonymous_pull_enabled == false &&
      azurerm_role_assignment.api_acr_pull.role_definition_name == "AcrPull" &&
      azurerm_role_assignment.api_acr_pull.scope == azurerm_container_registry.api.id &&
    azurerm_role_assignment.api_acr_pull.principal_id == "55555555-5555-5555-5555-555555555555")
    error_message = "No registry password: only the API identity can pull, with AcrPull on this registry."
  }
  assert {
    condition     = azurerm_linux_web_app.api.app_settings["Mcp__DataProtectionBlobName"] == "fcv2/data-protection-keys.xml"
    error_message = "Data Protection keys must survive container restarts (private blob)."
  }
  assert {
    condition     = azurerm_cognitive_deployment.editorial[0].model[0].name == "gpt-6-luna" && azurerm_cognitive_deployment.editorial[0].model[0].version == "2026-09-22"
    error_message = "Editorial generation uses gpt-6-luna."
  }
}

run "without_openai_and_with_operator_access" {
  command = apply

  variables {
    openai_enabled          = false
    background_jobs_enabled = true
    api_app_name            = "apifcv2"
    postgres_admin_ip_ranges = [
      { name = "migration-operator", start_ip = "203.0.113.10", end_ip = "203.0.113.10" },
    ]
  }

  assert {
    condition     = length(azurerm_cognitive_account.openai) == 0 && !contains(keys(azurerm_linux_web_app.api.app_settings), "OpenAi__ApiKey")
    error_message = "No Azure OpenAI resources or settings when disabled."
  }
  assert {
    condition     = azurerm_linux_web_app.api.app_settings["OpenAi__Enabled"] == "false" && azurerm_linux_web_app.api.app_settings["BackgroundJobs__Enabled"] == "true"
    error_message = "Toggles are forwarded to the API."
  }
  assert {
    condition     = azurerm_linux_web_app.api.name == "apifcv2"
    error_message = "The API keeps the fixed public name apifcv2.azurewebsites.net."
  }
  assert {
    condition     = azurerm_postgresql_flexible_server_firewall_rule.operators["migration-operator"].start_ip_address == "203.0.113.10"
    error_message = "Operator ranges become named firewall rules."
  }
}

run "invalid_suffix_is_rejected" {
  command = plan

  variables {
    name_suffix = "Not-Valid"
  }

  expect_failures = [var.name_suffix]
}
