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

# Only the web app reaches the database: one rule per possible outbound address of the
# App Service (the list comes from the variable because for_each cannot use apply-time
# values). Refresh it if the plan moves to another scale unit, see infra/README.md.
resource "azurerm_postgresql_flexible_server_firewall_rule" "api_outbound" {
  for_each         = toset(var.postgres_api_outbound_ips)
  name             = "api-${replace(each.value, ".", "-")}"
  server_id        = azurerm_postgresql_flexible_server.main.id
  start_ip_address = each.value
  end_ip_address   = each.value
}

# Used by the weekly backup and the restore workflows: they point it at the runner address
# and park it on an unroutable one afterwards (the resource group lock forbids deleting it).
resource "azurerm_postgresql_flexible_server_firewall_rule" "backup_runner" {
  name             = "backup-runner"
  server_id        = azurerm_postgresql_flexible_server.main.id
  start_ip_address = "192.0.2.1"
  end_ip_address   = "192.0.2.1"

  lifecycle {
    ignore_changes = [start_ip_address, end_ip_address]
  }
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
  # B1ms allows 35 user connections and a deployment briefly runs the old and the
  # new container side by side: two pools of 15 leave room for the operator.
  postgres_connection_string = join(";", [
    "Host=${azurerm_postgresql_flexible_server.main.fqdn}",
    "Port=5432",
    "Database=${azurerm_postgresql_flexible_server_database.fantasoccer.name}",
    "Username=${var.postgres_admin_login}",
    "Password=${random_password.postgres.result}",
    "SSL Mode=VerifyFull",
    "Maximum Pool Size=15",
  ])
}

# A deploy can kill the old API without closing its connections cleanly. PostgreSQL then keeps the
# dead session (and the global write lock it may hold) until its TCP keepalive gives up: with the
# defaults (idle 120 s, 9 probes every 30 s) that is about 6.5 minutes of 120 s lock timeouts for every
# other writer. Probing after 30 s with 3 probes every 10 s frees the lock in about a minute.
resource "azurerm_postgresql_flexible_server_configuration" "tcp_keepalives_idle" {
  name      = "tcp_keepalives_idle"
  server_id = azurerm_postgresql_flexible_server.main.id
  value     = "30"
}

resource "azurerm_postgresql_flexible_server_configuration" "tcp_keepalives_interval" {
  name      = "tcp_keepalives_interval"
  server_id = azurerm_postgresql_flexible_server.main.id
  value     = "10"

  # Server parameters are changed one at a time (the server rejects concurrent updates).
  depends_on = [azurerm_postgresql_flexible_server_configuration.tcp_keepalives_idle]
}

resource "azurerm_postgresql_flexible_server_configuration" "tcp_keepalives_count" {
  name      = "tcp_keepalives_count"
  server_id = azurerm_postgresql_flexible_server.main.id
  value     = "3"

  depends_on = [azurerm_postgresql_flexible_server_configuration.tcp_keepalives_interval]
}
