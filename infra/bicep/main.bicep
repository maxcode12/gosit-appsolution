targetScope = 'resourceGroup'

@description('Environment short name, e.g. dev, qa, prod.')
@allowed(['dev', 'qa', 'prod'])
param environmentName string

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('PostgreSQL administrator login.')
param postgresAdminLogin string

@description('PostgreSQL administrator password. Supply from a pipeline secret or Key Vault reference — never a literal.')
@secure()
param postgresAdminPassword string

@description('Object ID of the principal granted Key Vault secret access (deployment identity).')
param keyVaultAdminObjectId string

var namePrefix = 'gosit-${environmentName}'
var uniqueSuffix = uniqueString(resourceGroup().id)

module logAnalytics 'modules/log-analytics.bicep' = {
  name: 'logAnalytics'
  params: {
    name: '${namePrefix}-law'
    location: location
  }
}

module appInsights 'modules/app-insights.bicep' = {
  name: 'appInsights'
  params: {
    name: '${namePrefix}-appi'
    location: location
    workspaceId: logAnalytics.outputs.id
  }
}

module keyVault 'modules/key-vault.bicep' = {
  name: 'keyVault'
  params: {
    // Key Vault names are globally unique and capped at 24 characters.
    name: take('gosit${environmentName}kv${uniqueSuffix}', 24)
    location: location
    adminObjectId: keyVaultAdminObjectId
  }
}

module postgres 'modules/postgres.bicep' = {
  name: 'postgres'
  params: {
    name: '${namePrefix}-pg'
    location: location
    administratorLogin: postgresAdminLogin
    administratorPassword: postgresAdminPassword
  }
}

module redis 'modules/redis.bicep' = {
  name: 'redis'
  params: {
    name: '${namePrefix}-redis'
    location: location
  }
}

module storage 'modules/storage.bicep' = {
  name: 'storage'
  params: {
    name: take('gosit${environmentName}st${uniqueSuffix}', 24)
    location: location
  }
}

module serviceBus 'modules/service-bus.bicep' = {
  name: 'serviceBus'
  params: {
    name: '${namePrefix}-sb-${uniqueSuffix}'
    location: location
  }
}

output appInsightsConnectionString string = appInsights.outputs.connectionString
output keyVaultName string = keyVault.outputs.name
output postgresFqdn string = postgres.outputs.fullyQualifiedDomainName
output storageAccountName string = storage.outputs.name
output serviceBusNamespace string = serviceBus.outputs.name
