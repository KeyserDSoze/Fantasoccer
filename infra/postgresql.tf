resource "random_password" "postgres" {
  length           = 40
  special          = true
  override_special = "-_"
  min_lower        = 2
  min_upper        = 2
  min_numeric      = 2
  min_special      = 1
  keepers = {
    version = var.postgres_password_version
  }
}

resource "azurerm_postgresql_flexible_server" "main" {
  name                          = local.names.postgres
  resource_group_name           = azurerm_resource_group.main.name
  location                      = azurerm_resource_group.main.location
  version                       = var.postgres_version
  sku_name                      = var.postgres_sku
  storage_mb                    = var.postgres_storage_mb
  auto_grow_enabled             = true
  backup_retention_days         = var.postgres_backup_retention_days
  geo_redundant_backup_enabled  = false
  public_network_access_enabled = true
  administrator_login           = var.postgres_admin_login
  administrator_password        = random_password.postgres.result

  authentication {
    password_auth_enabled         = true
    active_directory_auth_enabled = false
  }

  tags = local.tags

  lifecycle {
    # Authoritative game data after the migration: never destroyed by a plan.
    prevent_destroy = true
    # Azure assigns a zone when none is requested.
    ignore_changes = [zone]
  }
}

resource "azurerm_postgresql_flexible_server_database" "fantasoccer" {
  name      = "fantasoccer"
  server_id = azurerm_postgresql_flexible_server.main.id
  charset   = "UTF8"
  collation = "en_US.utf8"

  lifecycle {
    prevent_destroy = true
  }
}

resource "azurerm_postgresql_flexible_server_firewall_rule" "azure_services" {
  count            = var.postgres_allow_azure_services ? 1 : 0
  name             = "AllowAllAzureServicesAndResourcesWithinAzureIps"
  server_id        = azurerm_postgresql_flexible_server.main.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}

resource "azurerm_postgresql_flexible_server_firewall_rule" "operators" {
  for_each         = { for range in var.postgres_admin_ip_ranges : range.name => range }
  name             = each.key
  server_id        = azurerm_postgresql_flexible_server.main.id
  start_ip_address = each.value.start_ip
  end_ip_address   = each.value.end_ip
}

locals {
  # TLS with certificate and host name verification (Azure uses public CAs).
  postgres_connection_string = join(";", [
    "Host=${azurerm_postgresql_flexible_server.main.fqdn}",
    "Port=5432",
    "Database=${azurerm_postgresql_flexible_server_database.fantasoccer.name}",
    "Username=${var.postgres_admin_login}",
    "Password=${random_password.postgres.result}",
    "SSL Mode=VerifyFull",
  ])
}
