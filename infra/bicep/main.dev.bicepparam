using './main.bicep'

param environmentName = 'dev'
param location = 'canadacentral'
param postgresAdminLogin = 'gositadmin'
// Supplied by the pipeline from a variable group / Key Vault link. Never commit a real value.
param postgresAdminPassword = readEnvironmentVariable('POSTGRES_ADMIN_PASSWORD')
param keyVaultAdminObjectId = readEnvironmentVariable('KEYVAULT_ADMIN_OBJECT_ID')
