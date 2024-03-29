param storageAccountName string
param appServiceName string
param functionAppName string
param appInsightsInstrumentationKey string
param location string
param functionSettings object = {}
param appTags object = {}
param apiForFhirName string
param fhirServiceName string
param deploymentRepoUrl string
param fhirserviceRg string
param apiforFhirRg string
param exportWithHistory bool
param exportWithDelete bool
@description('Automatically create a role assignment for the function app to access the FHIR service and API for FHIR.')
param createRoleAssignment bool = true
param apiForFhirsubid string
param fhirsubid string

@description('Azure Function required linked storage account')
resource funcStorageAccount 'Microsoft.Storage/storageAccounts@2021-08-01' = {
    name: storageAccountName
    location: location
    kind: 'StorageV2'
    sku: {
        name: 'Standard_LRS'
    }
    tags: appTags
}

resource table 'Microsoft.Storage/storageAccounts/tableServices@2022-09-01' = {
  name: 'default'
  parent: funcStorageAccount
}

resource chunktable 'Microsoft.Storage/storageAccounts/tableServices/tables@2022-09-01' = {
  name: 'chunk'
  parent: table
}

resource exporttable 'Microsoft.Storage/storageAccounts/tableServices/tables@2022-09-01' = {
  name: 'export'
  parent: table
}

@description('App Service used to run Azure Function')
resource hostingPlan 'Microsoft.Web/serverfarms@2021-03-01' = {
  name: appServiceName
  location: location
  //kind: 'functionapp'
  sku: {
    tier: 'Standard'
    name: 'S1'
  }
  properties: {
    //reserved: true
  }
  tags: appTags
}

@description('Azure Function used to run Migration Tool')
resource functionApp 'Microsoft.Web/sites@2021-03-01' = {
    name: functionAppName
    location: location
    kind: 'functionapp'
    identity: {
        type: 'SystemAssigned'
    }
    properties: {
        httpsOnly: true
        serverFarmId: hostingPlan.id
        siteConfig: {
            alwaysOn: true
        }
    }

    resource ftpPublishingPolicy 'basicPublishingCredentialsPolicies' = {
        name: 'ftp'
        // Location is needed regardless of the warning.
        #disable-next-line BCP187
        location: location
        properties: {
            allow: false
        }
    }

    resource scmPublishingPolicy 'basicPublishingCredentialsPolicies' = {
        name: 'scm'
        // Location is needed regardless of the warning.
        #disable-next-line BCP187
        location: location
        properties: {
            allow: false
        }
    }

    resource functionAppSettings 'config' = {
      name: 'appsettings'
      properties: union({
              AzureWebJobsStorage: 'DefaultEndpointsProtocol=https;AccountName=${funcStorageAccount.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${funcStorageAccount.listKeys().keys[0].value}'
              FUNCTIONS_EXTENSION_VERSION: '~4'
              FUNCTIONS_WORKER_RUNTIME: 'dotnet-isolated'
              APPINSIGHTS_INSTRUMENTATIONKEY: appInsightsInstrumentationKey
              APPLICATIONINSIGHTS_CONNECTION_STRING: 'InstrumentationKey=${appInsightsInstrumentationKey}'
              AZURE_ScheduleInterval: 2
              AZURE_ImportMode: 'IncrementalLoad'
              AZURE_DeepCheckCount: 1
              AZURE_UserAgent: 'FhirMigrationTool'
              AZURE_SourceHttpClient: 'SourceFhirEndpoint'
              AZURE_DestinationHttpClient: 'DestinationFhirEndpoint'
              AZURE_ExportTableName: 'export'
              AZURE_ChunkTableName: 'chunk'
              AZURE_ExportChunkTime: 30
              AZURE_ExportWithHistory: exportWithHistory
              AZURE_ExportWithDelete: exportWithDelete
              
              AZURE_stagingStorageAccountName: storageAccountName
              AZURE_StagingStorageUri: 'https://${storageAccountName}.table.core.windows.net'

              // This will trigger the custom deployment script to run during deployment
              SCM_DO_BUILD_DURING_DEPLOYMENT: 'true'
              ENABLE_ORYX_BUILD: 'true'
              WEBSITE_RUN_FROM_PACKAGE: 0
          }, functionSettings)
  }
}
resource functionAppDeployment 'Microsoft.Web/sites/sourcecontrols@2021-03-01' = {
  name: 'web'
  parent: functionApp
  properties: {
    repoUrl: deploymentRepoUrl
    branch: 'main'
    isManualIntegration: true
  }
}
resource fhirService 'Microsoft.HealthcareApis/workspaces/fhirservices@2022-06-01' existing = if (createRoleAssignment == true) {
  //#disable-next-line prefer-interpolation
  name: fhirServiceName
  scope: resourceGroup(fhirserviceRg)
  
}

resource apiForFhir 'Microsoft.HealthcareApis/services@2021-11-01' existing = if (createRoleAssignment == true) {
  name: apiForFhirName
  scope: resourceGroup(apiforFhirRg)
}

@description('Setup access between FHIR and the deployment script managed identity')
module functionFhirServiceRoleAssignment './roleAssignment.bicep' = if (createRoleAssignment == true) {
  name: 'functionFhirServiceRoleAssignment'
  scope: resourceGroup(fhirsubid,fhirserviceRg)
  params: {
    resourceId: fhirService.id
    roleId : '5a1fc7df-4bf1-4951-a576-89034ee01acd'
    principalId: functionApp.identity.principalId
  }
}

@description('Setup access between FHIR and the deployment script managed identity')
module functionApiForFhirRoleAssignment './roleAssignment.bicep' = if (createRoleAssignment == true) {
  name: 'bulk-import-function-fhir-managed-id-role-assignment'
  scope: resourceGroup(apiForFhirsubid,apiforFhirRg)
  params: {
    resourceId: apiForFhir.id
    roleId: '5a1fc7df-4bf1-4951-a576-89034ee01acd'
    principalId: functionApp.identity.principalId
  }
}

@description('Setup access between FHIR and the deployment script managed identity')
module functionstorageTableRoleAssignment './roleAssignment.bicep' = if (createRoleAssignment == true) {
  name: 'storageTable-access'
  params: {
    resourceId: functionApp.identity.principalId
    roleId: '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3'
    principalId: functionApp.identity.principalId
  }
}

var defaultHostKey = listkeys('${functionApp.id}/host/default', '2016-08-01').functionKeys.default
output functionAppKey string = defaultHostKey
output functionAppName string = functionAppName
output functionAppPrincipalId string = functionApp.identity.principalId
output hostName string = functionApp.properties.defaultHostName
output functionBaseUrl string = 'https://${functionApp.properties.defaultHostName}'
