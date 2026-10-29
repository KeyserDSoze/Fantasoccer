# Non-secret configuration of the fcv2 environment. Secrets come from the
# GitHub "production" environment (see README).
environment_name    = "fcv2"
resource_group_name = "fcv2"
location            = "swedencentral"

app_service_sku         = "B1"
background_jobs_enabled = false

postgres_sku                   = "B_Standard_B1ms"
postgres_storage_mb            = 32768
postgres_backup_retention_days = 14
postgres_admin_ip_ranges       = []

openai_enabled       = true
openai_location      = "swedencentral"
openai_model_name    = "gpt-5.4-mini"
openai_model_version = "2026-03-17"
