# Files (images, logos, card images) and the private IDistributedCache container.
# The API authenticates with the connection string, stored in Key Vault.
resource "azurerm_storage_account" "main" {
  name                             = local.names.storage
  resource_group_name              = azurerm_resource_group.main.name
  location                         = azurerm_resource_group.main.location
  account_kind                     = "StorageV2"
  account_tier                     = "Standard"
  account_replication_type         = var.storage_replication_type
  access_tier                      = "Hot"
  min_tls_version                  = "TLS1_2"
  https_traffic_only_enabled       = true
  shared_access_key_enabled        = true
  cross_tenant_replication_enabled = false
  # Player images, team logos and card images are served directly to browsers
  # (Assets:PublicBlobBaseUrl); every other container stays private.
  allow_nested_items_to_be_public = true

  blob_properties {
    # No versioning: live votes are rewritten every minute during matches.
    versioning_enabled = false
    delete_retention_policy {
      days = var.blob_soft_delete_days
    }
    container_delete_retention_policy {
      days = var.blob_soft_delete_days
    }
  }

  tags = local.tags
}

locals {
  containers = {
    images                     = "blob"
    logos                      = "blob"
    cardimages                 = "blob"
    (var.cache_container_name) = "private"
    mcp-keys                   = "private"
  }
}

resource "azurerm_storage_container" "main" {
  for_each              = local.containers
  name                  = each.key
  storage_account_id    = azurerm_storage_account.main.id
  container_access_type = each.value
}

# Same rule as ops/azure/cache-lifecycle-policy.json: the cleanup job removes
# expired entries daily, this is the backstop for anything left behind.
resource "azurerm_storage_management_policy" "cache" {
  storage_account_id = azurerm_storage_account.main.id

  rule {
    name    = "fantazone-cache-expired-documents"
    enabled = true
    filters {
      blob_types   = ["blockBlob"]
      prefix_match = ["${var.cache_container_name}/"]
    }
    actions {
      base_blob {
        delete_after_days_since_modification_greater_than = 45
      }
      snapshot {
        delete_after_days_since_creation_greater_than = 1
      }
      version {
        delete_after_days_since_creation = 1
      }
    }
  }
}
