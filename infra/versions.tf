terraform {
  # Write-only attributes and ephemeral values keep generated and GitHub
  # secrets out of the state file.
  required_version = ">= 1.11.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 5.11"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.9"
    }
  }

  # Partial configuration: resource group, storage account, container and key
  # are passed by scripts/state-backend.sh (see README).
  backend "azurerm" {}
}
