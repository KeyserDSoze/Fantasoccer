# Private registry for the API image. GitHub pushes with the deployment
# principal (Contributor); the App Service pulls with its managed identity.
resource "azurerm_container_registry" "api" {
  name                          = local.names.registry
  resource_group_name           = azurerm_resource_group.main.name
  location                      = azurerm_resource_group.main.location
  sku                           = var.container_registry_sku
  admin_enabled                 = false
  anonymous_pull_enabled        = false
  public_network_access_enabled = true
  role_assignment_mode          = "LegacyRegistryPermissions"
  tags                          = merge(local.tags, { component = "api-registry" })
}

# The only role assignment of the configuration: no registry password exists.
resource "azurerm_role_assignment" "api_acr_pull" {
  scope                = azurerm_container_registry.api.id
  role_definition_name = "AcrPull"
  principal_id         = azurerm_user_assigned_identity.api.principal_id
  principal_type       = "ServicePrincipal"
}
