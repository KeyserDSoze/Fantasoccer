resource "azurerm_log_analytics_workspace" "main" {
  name                = local.names.log_analytics
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  sku                 = "PerGB2018"
  retention_in_days   = var.log_retention_days
  daily_quota_gb      = var.log_daily_quota_gb
  tags                = local.tags
}

# Workspace-based Application Insights: read by the API through
# ConnectionStrings:AppInsights (Telemetry:Provider = ApplicationInsights).
resource "azurerm_application_insights" "main" {
  name                = local.names.app_insights
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  workspace_id        = azurerm_log_analytics_workspace.main.id
  application_type    = "web"
  tags                = local.tags
}

resource "azurerm_monitor_diagnostic_setting" "api" {
  name                       = "to-log-analytics"
  target_resource_id         = azurerm_linux_web_app.api.id
  log_analytics_workspace_id = azurerm_log_analytics_workspace.main.id

  enabled_log { category = "AppServiceConsoleLogs" }
  enabled_log { category = "AppServiceAppLogs" }
  # AppServiceHTTPLogs is deliberately not collected: it stores CsUriQuery, and the
  # login refresh request carries the refresh token in its query string. Requests
  # remain visible in Application Insights, which strips query strings.
  enabled_metric { category = "AllMetrics" }
}

resource "azurerm_monitor_diagnostic_setting" "postgres" {
  name                       = "to-log-analytics"
  target_resource_id         = azurerm_postgresql_flexible_server.main.id
  log_analytics_workspace_id = azurerm_log_analytics_workspace.main.id

  enabled_log { category = "PostgreSQLLogs" }
  enabled_metric { category = "AllMetrics" }
}
