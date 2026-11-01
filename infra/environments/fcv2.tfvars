# Non-secret configuration of the fcv2 environment. Secrets come from the
# GitHub "production" environment (see README).
environment_name    = "fcv2"
resource_group_name = "fcv2"
# https://apifcv2.azurewebsites.net (fcv2.azurewebsites.net belongs to a third party).
api_app_name = "apifcv2"
# Sweden Central refuses PostgreSQL B1ms (and B2s) for this subscription with SkuNotAvailable, although list-skus shows it
# as available (31/10/2026). Italy North, West Europe, North Europe and Germany West Central accept it; Italy North is the
# closest to the players and keeps the App Service next to the database. Azure OpenAI stays in Sweden Central.
location = "italynorth"

app_service_sku         = "B1"
container_registry_sku  = "Basic"
background_jobs_enabled = true

postgres_sku                   = "B_Standard_B1ms"
postgres_storage_mb            = 32768
postgres_backup_retention_days = 14
# Temporarily open to everyone (friends-only environment); to be closed later.
# Authentication and TLS stay mandatory.
postgres_admin_ip_ranges = [
  { name = "open-to-all", start_ip = "0.0.0.0", end_ip = "255.255.255.255" },
]

openai_enabled       = true
openai_location      = "swedencentral"
openai_model_name    = "gpt-6-luna"
openai_model_version = "2026-09-22"

# 2: the Microsoft client secret was replaced on 31/10/2026 (the previous one was committed to the public history).
app_secrets_version = 2

# Rotated operational secrets are provisioned directly in Key Vault, outside
# Terraform state. Only references (never secret values) belong in this file.
# Certificate expiry: late October 2028; preserve it to read protected key-ring entries.
extra_app_settings = {
  # App Service terminates TLS at its reverse proxy; OAuth must see the original HTTPS scheme.
  ASPNETCORE_FORWARDEDHEADERS_ENABLED = "true"
  Push__Enabled                       = "true"
  Push__Subject                       = "https://fanta.zone"
  Push__PublicKey                     = "@Microsoft.KeyVault(SecretUri=https://kv-fcv2-f71915.vault.azure.net/secrets/push-public-key-20261101)"
  Push__PrivateKey                    = "@Microsoft.KeyVault(SecretUri=https://kv-fcv2-f71915.vault.azure.net/secrets/push-private-key-20261101)"
  Mcp__Enabled                        = "true"
  Mcp__Issuer                         = "https://api.fanta.zone"
  Mcp__FrontendUrl                    = "https://fanta.zone/"
  Mcp__CertificateBase64              = "@Microsoft.KeyVault(SecretUri=https://kv-fcv2-f71915.vault.azure.net/secrets/mcp-certificate-20261101)"
}
