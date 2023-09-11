@description('Name of the Azure API for FHIR service.')
param apiForFhirName string

@description('Name of the FHIR workspace.')
param workspaceName string

@description('Name of the FHIR service.')
param fhirServiceName string

@description('Name of the storage account.')
param storageAccountName string

@description('Location for the API for FHIR service.')
param location string = resourceGroup().location

@description('Storage account to configure for export from Azure API for FHIR and import to FHIR Service')
resource storageAccount 'Microsoft.Storage/storageAccounts@2021-08-01' = {
  name: storageAccountName
  location: location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    isHnsEnabled: true
  }
}

resource apiForFhir 'Microsoft.HealthcareApis/services@2021-11-01' = {
  name: apiForFhirName
  location: location
  kind: 'fhir-R4'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    authenticationConfiguration: {
      audience: 'https://${apiForFhirName}.azurehealthcareapis.com'
      authority: uri(environment().authentication.loginEndpoint, subscription().tenantId)
    }
    exportConfiguration: {
      storageAccountName: storageAccountName
    }
  }
  dependsOn: [
    storageAccount
  ]
}

resource Microsoft_Authorization_roleDefinitions_ba92f5b4_2d11_453d_a403_e96b0029c9fe 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: storageAccount
  name: guid(subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'))
  properties: {
    principalId: reference(apiForFhir.id, '2021-11-01', 'full').identity.principalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe')
  }
}

resource hw_workspace 'Microsoft.HealthcareApis/workspaces@2022-05-15' = {
  name: replace('hw-${workspaceName}', '-', '')
  location: location
  properties: {
  }
}

resource hw_workspaceName_fs_fhirService 'Microsoft.HealthcareApis/workspaces/fhirservices@2022-05-15' = {
  name: '${replace('hw-${workspaceName}', '-', '')}/fs-${fhirServiceName}'
  location: location
  kind: 'fhir-R4'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    authenticationConfiguration: {
      authority: '${environment().authentication.loginEndpoint}${subscription().tenantId}'
      audience: 'https://${replace('hw-${workspaceName}', '-', '')}-fs-${fhirServiceName}.fhir.azurehealthcareapis.com'
      smartProxyEnabled: false
    }
    importConfiguration: {
      enabled: true
      initialImportMode: true
      integrationDataStore: storageAccountName
    }
  }
  dependsOn: [
    hw_workspace
    storageAccount
  ]
}

resource Microsoft_Authorization_roleDefinitions_ba92f5b4_2d11_453d_a403_e96b0029c9fe123 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: storageAccount
  name: guid(subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe123'))
  properties: {
    principalId: reference(hw_workspaceName_fs_fhirService.id, '2021-11-01', 'full').identity.principalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe')
  }
}
