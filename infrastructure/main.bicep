@description('Location for all resources')
param location string = 'uksouth'

@description('Base name for resources')
param baseName string = 'expensemgmt'

@description('Administrator login (email) for the SQL server Entra ID admin')
param adminLogin string

@description('Administrator Object ID for Entra ID authentication')
param adminObjectId string

@description('Deploy GenAI resources (Azure OpenAI and AI Search)')
param deployGenAI bool = false

// App Service and Managed Identity
module appService 'app-service.bicep' = {
  name: 'appServiceDeployment'
  params: {
    location: location
    baseName: baseName
  }
}

// Azure SQL Database
module azureSql 'azure-sql.bicep' = {
  name: 'azureSqlDeployment'
  params: {
    location: location
    baseName: baseName
    adminLogin: adminLogin
    adminObjectId: adminObjectId
    managedIdentityPrincipalId: appService.outputs.managedIdentityPrincipalId
  }
}

// GenAI resources (conditional)
module genAI 'genai.bicep' = if (deployGenAI) {
  name: 'genAIDeployment'
  params: {
    location: 'swedencentral' // GPT-4o only available in Sweden
    baseName: baseName
    managedIdentityPrincipalId: appService.outputs.managedIdentityPrincipalId
  }
}

// Outputs - App Service
output webAppName string = appService.outputs.webAppName
output webAppHostName string = appService.outputs.webAppHostName
output webAppUrl string = appService.outputs.webAppUrl

// Outputs - Managed Identity
output managedIdentityName string = appService.outputs.managedIdentityName
output managedIdentityClientId string = appService.outputs.managedIdentityClientId
output managedIdentityPrincipalId string = appService.outputs.managedIdentityPrincipalId

// Outputs - SQL Server
output sqlServerName string = azureSql.outputs.sqlServerName
output sqlServerFqdn string = azureSql.outputs.sqlServerFqdn
output databaseName string = azureSql.outputs.databaseName
output connectionString string = azureSql.outputs.connectionString

// Outputs - GenAI (conditional, with null-safe operators)
output openAIEndpoint string = deployGenAI ? genAI.outputs.openAIEndpoint : ''
output openAIModelName string = deployGenAI ? genAI.outputs.openAIModelName : ''
output openAIName string = deployGenAI ? genAI.outputs.openAIName : ''
output searchEndpoint string = deployGenAI ? genAI.outputs.searchEndpoint : ''
output searchServiceName string = deployGenAI ? genAI.outputs.searchServiceName : ''
