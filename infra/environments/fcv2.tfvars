# Non-secret configuration of the fcv2 environment. Secrets come from the
# GitHub "production" environment (see README).
environment_name    = "fcv2"
resource_group_name = "fcv2"
# https://apifcv2.azurewebsites.net (fcv2.azurewebsites.net belongs to a third party).
api_app_name            = "apifcv3"
api_resource_group_name = "fcv2-b"
app_service_plan_name   = "asp-fcv2b"
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

# Only the API (api_outbound rules) reaches PostgreSQL. For maintenance add a single operator address here, apply, then remove it.
postgres_admin_ip_ranges = []

# Possible outbound addresses of apifcv2 (az webapp show -g fcv2 -n apifcv2 --query possibleOutboundIpAddresses).
postgres_api_outbound_ips = [
  "4.232.112.133",
  "4.232.112.140",
  "4.232.112.141",
  "4.232.112.142",
  "4.232.112.144",
  "4.232.112.150",
  "4.232.112.151",
  "4.232.112.152",
  "4.232.112.153",
  "4.232.112.154",
  "4.232.112.155",
  "4.232.112.156",
  "4.232.112.157",
  "4.232.112.168",
  "4.232.112.169",
  "4.232.112.171",
  "4.232.112.174",
  "4.232.112.203",
  "4.232.112.222",
  "4.232.112.61",
  "4.232.112.84",
  "4.232.113.33",
  "4.232.113.34",
  "4.232.113.40",
  "4.232.113.41",
  "4.232.113.42",
  "4.232.113.43",
  "4.232.113.44",
  "4.232.113.45",
  "4.232.14.227",
  "4.232.99.2",
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

  # Production accepts the Microsoft login only from the public site; HTTPS is enforced by App Service itself.
  SocialLogin__Microsoft__RedirectDomains = "https://fanta.zone"
  Http__RedirectToHttps                   = "false"
}
