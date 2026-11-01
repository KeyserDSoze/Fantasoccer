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

# Only the API (api_outbound rules) reaches PostgreSQL. For maintenance add a single operator address here, apply, then remove it.
postgres_admin_ip_ranges = []

# Possible outbound addresses of apifcv2 (az webapp show -g fcv2 -n apifcv2 --query possibleOutboundIpAddresses).
postgres_api_outbound_ips = [
  "4.232.7.47",
  "4.232.99.4",
  "4.232.224.248",
  "4.232.225.3",
  "4.232.225.127",
  "4.232.240.146",
  "4.232.248.101",
  "4.232.249.233",
  "9.235.96.125",
  "9.235.105.32",
  "72.146.48.228",
  "72.146.50.47",
  "72.146.51.63",
  "72.146.51.70",
  "72.146.51.149",
  "72.146.51.191",
  "72.146.51.229",
  "72.146.52.58",
  "72.146.52.69",
  "72.146.52.90",
  "72.146.52.115",
  "72.146.52.116",
  "72.146.52.138",
  "72.146.52.175",
  "72.146.52.177",
  "72.146.52.215",
  "72.146.52.230",
  "72.146.52.234",
  "72.146.52.238",
  "72.146.53.78",
  "72.146.53.93",
  "72.146.53.117",
  "72.146.53.130",
  "72.146.53.144",
  "72.146.53.184",
  "72.146.53.186",
  "72.146.53.199",
  "72.146.53.200",
  "72.146.54.10",
  "72.146.54.12",
  "72.146.130.26",
  "172.213.193.7",
  "172.213.197.181",
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
