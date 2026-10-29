provider "azurerm" {
  # Credentials come from ARM_CLIENT_ID / ARM_CLIENT_SECRET / ARM_TENANT_ID /
  # ARM_SUBSCRIPTION_ID (the existing GitHub app registration). Resource
  # providers are registered by scripts/register-providers.sh during the manual
  # apply only, so that plans never change the subscription.
  resource_provider_registrations = "none"

  features {
    key_vault {
      # The environment can be destroyed and recreated with the same names.
      purge_soft_delete_on_destroy    = true
      recover_soft_deleted_key_vaults = true
      recover_soft_deleted_secrets    = true
    }
    cognitive_account {
      purge_soft_delete_on_destroy = true
    }
    resource_group {
      # Never delete a resource group that still contains resources created outside Terraform.
      prevent_deletion_if_contains_resources = true
    }
  }
}
