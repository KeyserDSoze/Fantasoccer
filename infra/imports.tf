# One-off: the web app moved to a new resource group, plan and name (new outbound addresses) and was created
# with the Azure CLI. These blocks adopt the resources into the state (adopt_existing_api = true in the tfvars);
# the old app and plan no longer exist, so the refresh drops them from the state first.
# After the first successful apply set adopt_existing_api to false and delete this file.
import {
  for_each = var.adopt_existing_api ? { adopt = true } : {}
  to       = azurerm_resource_group.api[0]
  id       = "/subscriptions/c46eb61c-c951-471f-aa59-bf20aacf4d75/resourceGroups/fcv2-b"
}

import {
  for_each = var.adopt_existing_api ? { adopt = true } : {}
  to       = azurerm_service_plan.main
  id       = "/subscriptions/c46eb61c-c951-471f-aa59-bf20aacf4d75/resourceGroups/fcv2-b/providers/Microsoft.Web/serverFarms/asp-fcv2b"
}

import {
  for_each = var.adopt_existing_api ? { adopt = true } : {}
  to       = azurerm_linux_web_app.api
  id       = "/subscriptions/c46eb61c-c951-471f-aa59-bf20aacf4d75/resourceGroups/fcv2-b/providers/Microsoft.Web/sites/apifcv3"
}

import {
  for_each = var.adopt_existing_api ? { adopt = true } : {}
  to       = azurerm_monitor_diagnostic_setting.api
  id       = "/subscriptions/c46eb61c-c951-471f-aa59-bf20aacf4d75/resourceGroups/fcv2-b/providers/Microsoft.Web/sites/apifcv3|to-log-analytics"
}

import {
  for_each = var.adopt_existing_api ? toset(var.postgres_api_outbound_ips) : toset([])
  to       = azurerm_postgresql_flexible_server_firewall_rule.api_outbound[each.key]
  id       = "/subscriptions/c46eb61c-c951-471f-aa59-bf20aacf4d75/resourceGroups/fcv2/providers/Microsoft.DBforPostgreSQL/flexibleServers/psql-fcv2-f71915/firewallRules/api-${replace(each.key, ".", "-")}"
}
