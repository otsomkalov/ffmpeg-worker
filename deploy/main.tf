variable "env" {
  type    = string
  default = "prod"
}

locals {
  tags = {
    env  = var.env
    name = "ffmpeg-azure-storage-worker"
  }
}

terraform {
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = ">=3.37.0"
    }
  }
}

# Configure the Microsoft Azure Provider
provider "azurerm" {
  features {}
}

resource "azurerm_resource_group" "rg-ffmpeg-azure-storage-worker" {
  name     = "rg-ffmpeg-azure-storage-worker-${var.env}"
  location = "France Central"

  tags = local.tags
}

resource "azurerm_application_insights" "appi-ffmpeg-azure-storage-worker" {
  name                = "appi-ffmpeg-azure-storage-worker-${var.env}"
  location            = azurerm_resource_group.rg-ffmpeg-azure-storage-worker.location
  resource_group_name = azurerm_resource_group.rg-ffmpeg-azure-storage-worker.name
  application_type    = "web"
  retention_in_days   = 30

  tags = local.tags
}

resource "azurerm_storage_account" "stffmpegazurestorageworker" {
  name                     = "stffmpegazurestworker${var.env}"
  resource_group_name      = azurerm_resource_group.rg-ffmpeg-azure-storage-worker.name
  location                 = azurerm_resource_group.rg-ffmpeg-azure-storage-worker.location
  account_tier             = "Standard"
  account_replication_type = "LRS"
  account_kind             = "Storage"

  tags = local.tags
}

resource "azurerm_storage_container" "stc-input" {
  name                  = "input"
  storage_account_name  = azurerm_storage_account.stffmpegazurestorageworker.name
  container_access_type = "private"
}

resource "azurerm_storage_container" "stc-output" {
  name                  = "output"
  storage_account_name  = azurerm_storage_account.stffmpegazurestorageworker.name
  container_access_type = "private"
}

resource "azurerm_storage_queue" "stq-input" {
  name                 = "input"
  storage_account_name = azurerm_storage_account.stffmpegazurestorageworker.name
}

resource "azurerm_storage_queue" "stq-output" {
  name                 = "output"
  storage_account_name = azurerm_storage_account.stffmpegazurestorageworker.name
}

output "app_insigts_instrumentation_key" {
  description = "Applications Insights key"
  value       = azurerm_application_insights.appi-ffmpeg-azure-storage-worker.instrumentation_key
  sensitive   = true
}

output "storage_account_connection_string" {
  description = "Storage account connection string"
  value       = azurerm_storage_account.stffmpegazurestorageworker.primary_connection_string
  sensitive   = true
}
