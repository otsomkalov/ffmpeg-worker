locals {
  tags = {
    env  = "prod"
    name = "ffmpeg-azure-storage-worker"
  }
}

terraform {
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "=3.0.0"
    }
  }
}

# Configure the Microsoft Azure Provider
provider "azurerm" {
  features {}
}

resource "azurerm_resource_group" "ffmpeg-azure-storage-worker" {
  name     = "ffmpeg-azure-storage-worker"
  location = "France Central"
}

resource "azurerm_application_insights" "ffmpeg-azure-storage-worker" {
  name                = "ffmpeg-azure-storage-worker"
  location            = azurerm_resource_group.ffmpeg-azure-storage-worker.location
  resource_group_name = azurerm_resource_group.ffmpeg-azure-storage-worker.name
  application_type    = "web"
  retention_in_days   = 30
}

resource "azurerm_storage_account" "ffmpegazurestorageworker" {
  name                     = "ffmpegazurestorageworker"
  resource_group_name      = azurerm_resource_group.ffmpeg-azure-storage-worker.name
  location                 = azurerm_resource_group.ffmpeg-azure-storage-worker.location
  account_tier             = "Standard"
  account_replication_type = "LRS"
  account_kind             = "Storage"

  tags = local.tags
}

resource "azurerm_storage_container" "input" {
  name                  = "input"
  storage_account_name  = azurerm_storage_account.ffmpegazurestorageworker.name
  container_access_type = "private"
}

resource "azurerm_storage_container" "output" {
  name                  = "output"
  storage_account_name  = azurerm_storage_account.ffmpegazurestorageworker.name
  container_access_type = "private"
}

resource "azurerm_storage_queue" "input" {
  name                 = "input"
  storage_account_name = azurerm_storage_account.ffmpegazurestorageworker.name
}

resource "azurerm_storage_queue" "output" {
  name                 = "output"
  storage_account_name = azurerm_storage_account.ffmpegazurestorageworker.name
}

output "app_insigts_instrumentation_key" {
  description = "Applications Insights key"
  value       = azurerm_application_insights.ffmpeg-azure-storage-worker.instrumentation_key
  sensitive   = true
}

output "storage_account_connection_string" {
  description = "Storage account connection string"
  value       = azurerm_storage_account.ffmpegazurestorageworker.primary_connection_string
  sensitive   = true
}
