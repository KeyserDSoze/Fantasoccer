output "resource_group_name" {
  value = azurerm_resource_group.main.name
}

output "api_app_name" {
  value = azurerm_linux_web_app.api.name
}

output "api_url" {
  value = "https://${azurerm_linux_web_app.api.default_hostname}"
}

output "container_registry_login_server" {
  value = azurerm_container_registry.api.login_server
}

output "postgres_fqdn" {
  value = azurerm_postgresql_flexible_server.main.fqdn
}

output "postgres_database" {
  value = azurerm_postgresql_flexible_server_database.fantasoccer.name
}

output "key_vault_name" {
  value = azurerm_key_vault.main.name
}

output "storage_account_name" {
  value = azurerm_storage_account.main.name
}

output "public_blob_base_url" {
  value = trimsuffix(azurerm_storage_account.main.primary_blob_endpoint, "/")
}

output "openai_endpoint" {
  value = var.openai_enabled ? azurerm_cognitive_account.openai[0].endpoint : null
}
