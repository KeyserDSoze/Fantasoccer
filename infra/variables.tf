variable "environment_name" {
  description = "Short environment name used in resource names and tags."
  type        = string
  default     = "fcv2"
  validation {
    condition     = can(regex("^[a-z][a-z0-9]{1,7}$", var.environment_name))
    error_message = "environment_name: 2-8 lowercase letters/digits, starting with a letter."
  }
}

variable "resource_group_name" {
  description = "Resource group that contains the whole environment."
  type        = string
  default     = "fcv2"
}

variable "location" {
  description = "Azure region for every resource except Azure OpenAI. Changing it after the first apply recreates the resources."
  type        = string
  default     = "swedencentral"
}

variable "name_suffix" {
  description = "Suffix for globally unique names (storage, Key Vault, PostgreSQL, OpenAI, App Service). Empty: derived from the subscription and environment."
  type        = string
  default     = ""
  validation {
    condition     = can(regex("^[a-z0-9]{0,6}$", var.name_suffix))
    error_message = "name_suffix: at most 6 lowercase letters/digits."
  }
}

variable "api_app_name" {
  description = "App Service name (host <name>.azurewebsites.net). Empty: app-<environment>-api-<suffix>."
  type        = string
  default     = ""
}

variable "tags" {
  description = "Extra tags for every resource."
  type        = map(string)
  default     = {}
}

# --- App Service -------------------------------------------------------------

variable "app_service_sku" {
  description = "Linux App Service plan SKU. Basic or higher: Always On is required by the SQL job and push workers."
  type        = string
  default     = "B1"
}

variable "dotnet_version" {
  description = ".NET runtime of the API."
  type        = string
  default     = "10.0"
}

variable "background_jobs_enabled" {
  description = "Starts the SQL job worker. Keep false until the legacy import is completed: the first application write closes the reimport window."
  type        = bool
  default     = false
}

variable "cache_namespace" {
  description = "First path segment of the distributed cache blobs (Cache:Namespace)."
  type        = string
  default     = "production"
}

variable "extra_app_settings" {
  description = "Additional non-secret App Service settings (for example Authorization__GlobalAdministrators__0)."
  type        = map(string)
  default     = {}
}

# --- Storage -----------------------------------------------------------------

variable "storage_replication_type" {
  description = "Replication of the storage account holding images, logos, card images and the cache."
  type        = string
  default     = "LRS"
}

variable "cache_container_name" {
  description = "Private container of the IDistributedCache provider (Cache:ContainerName)."
  type        = string
  default     = "fantazone-cache"
}

variable "blob_soft_delete_days" {
  description = "Soft delete retention for blobs and containers (images/logos protection; the cache lifecycle rule removes old versions)."
  type        = number
  default     = 7
}

# --- PostgreSQL --------------------------------------------------------------

variable "postgres_version" {
  description = "PostgreSQL major version (same as the local laboratory)."
  type        = string
  default     = "17"
}

variable "postgres_sku" {
  description = "Flexible Server SKU."
  type        = string
  default     = "B_Standard_B1ms"
}

variable "postgres_storage_mb" {
  description = "Initial storage in MB (auto-grow enabled)."
  type        = number
  default     = 32768
}

variable "postgres_backup_retention_days" {
  description = "Point-in-time restore window."
  type        = number
  default     = 14
  validation {
    condition     = var.postgres_backup_retention_days >= 7 && var.postgres_backup_retention_days <= 35
    error_message = "postgres_backup_retention_days: between 7 and 35."
  }
}

variable "postgres_admin_login" {
  description = "Administrator login used by the API (it applies the EF Core migrations at startup)."
  type        = string
  default     = "fantasoccer_admin"
}

variable "postgres_password_version" {
  description = "Increase to rotate the generated administrator password (server and Key Vault secret are updated together)."
  type        = number
  default     = 1
}

variable "postgres_allow_azure_services" {
  description = "Firewall rule 0.0.0.0 for Azure services (App Service outbound addresses are not known at plan time). Authentication and TLS stay mandatory."
  type        = bool
  default     = true
}

variable "postgres_admin_ip_ranges" {
  description = "Operator IP ranges allowed to reach PostgreSQL (data migration, maintenance). Remove them after use."
  type = list(object({
    name     = string
    start_ip = string
    end_ip   = string
  }))
  default = []
}

# --- Azure OpenAI ------------------------------------------------------------

variable "openai_enabled" {
  description = "Creates Azure OpenAI and enables the optional editorial generation (always triggered by an explicit POST)."
  type        = bool
  default     = true
}

variable "openai_location" {
  description = "Azure OpenAI region."
  type        = string
  default     = "swedencentral"
}

variable "openai_deployment_name" {
  description = "Deployment name, used by the API as OpenAi:ModelName."
  type        = string
  default     = "editorial"
}

variable "openai_model_name" {
  description = "Model of the deployment. gpt-4o (current production) is deprecated for new deployments."
  type        = string
  default     = "gpt-5.4-mini"
}

variable "openai_model_version" {
  description = "Model version (check with az cognitiveservices model list --location <region>)."
  type        = string
  default     = "2026-03-17"
}

variable "openai_sku_name" {
  description = "Deployment type."
  type        = string
  default     = "GlobalStandard"
}

variable "openai_capacity" {
  description = "Deployment capacity in thousands of tokens per minute."
  type        = number
  default     = 10
}

# --- Monitoring --------------------------------------------------------------

variable "log_retention_days" {
  description = "Log Analytics retention."
  type        = number
  default     = 30
}

variable "log_daily_quota_gb" {
  description = "Log Analytics daily ingestion cap in GB (-1: unlimited)."
  type        = number
  default     = 1
}

# --- Secrets from GitHub (never stored in the Terraform state) ---------------

variable "microsoft_client_secret" {
  description = "Microsoft social login client secret (GitHub secret MICROSOFT_CLIENT_SECRET)."
  type        = string
  sensitive   = true
  ephemeral   = true
}

variable "encryption_key" {
  description = "Application encryption key (GitHub secret ENCRYPTION_KEY)."
  type        = string
  sensitive   = true
  ephemeral   = true
}

variable "encryption_iv" {
  description = "Application encryption IV (GitHub secret ENCRYPTION_IV)."
  type        = string
  sensitive   = true
  ephemeral   = true
}

variable "app_secrets_version" {
  description = "Increase after changing MICROSOFT_CLIENT_SECRET, ENCRYPTION_KEY or ENCRYPTION_IV in GitHub: write-only secrets are rewritten only when this changes."
  type        = number
  default     = 1
}
