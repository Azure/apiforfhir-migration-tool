param storageAccountName string
param appServiceName string
param functionAppName string
param appInsightsInstrumentationKey string
param location string
param functionSettings object = {}
param appTags object = {}
param apiForFhirName string
param fhirServiceName string

@description('Automatically create a role assignment for the function app to access the FHIR service and API for FHIR.')
param createRoleAssignment bool = true

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

@description('App Service used to run Azure Function')
resource hostingPlan 'Microsoft.Web/serverfarms@2021-03-01' = {
  name: appServiceName
  location: location
  kind: 'functionapp'
  sku: {
    tier: 'Standard'
    name: 'S2'
  }
  properties: {
    reserved: true
  }
  tags: appTags
}

@description('Azure Function used to run toolkit compute')
resource functionApp 'Microsoft.Web/sites@2021-03-01' = {
    name: functionAppName
    location: location
    kind: 'functionapp,linux'

    identity: {
        type: 'SystemAssigned'
    }

    properties: {
        httpsOnly: true
        enabled: true
        serverFarmId: hostingPlan.id
        //reserved: true
        clientAffinityEnabled: false
        siteConfig: {
            linuxFxVersion: 'dotnet-isolated|7.0'
            use32BitWorkerProcess: false
            alwaysOn: true
        }
    }

    // tags: union(appTags, {
    //         'azd-service-name': 'func'
    //     })

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
}

resource functionAppSettings 'Microsoft.Web/sites/config@2020-12-01' = {
    name: 'appsettings'
    parent: functionApp
    properties: union({
            AzureWebJobsStorage: 'DefaultEndpointsProtocol=https;AccountName=${funcStorageAccount.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${funcStorageAccount.listKeys().keys[0].value}'
            FUNCTIONS_EXTENSION_VERSION: '~4'
            FUNCTIONS_WORKER_RUNTIME: 'dotnet-isolated'
            APPINSIGHTS_INSTRUMENTATIONKEY: appInsightsInstrumentationKey
            APPLICATIONINSIGHTS_CONNECTION_STRING: 'InstrumentationKey=${appInsightsInstrumentationKey}'
            SCM_DO_BUILD_DURING_DEPLOYMENT: 'false'
            ENABLE_ORYX_BUILD: 'false'
            WEBSITE_RUN_FROM_PACKAGE: 1
        }, functionSettings)
}


resource fhirService 'Microsoft.HealthcareApis/workspaces/fhirservices@2021-06-01-preview' existing = if (createRoleAssignment == true) {
  //#disable-next-line prefer-interpolation
  name: fhirServiceName
}

resource apiForFhir 'Microsoft.HealthcareApis/services@2021-11-01' existing = if (createRoleAssignment == true) {
  name: apiForFhirName
}

@description('Setup access between FHIR and the deployment script managed identity')
module functionFhirServiceRoleAssignment './roleAssignment.bicep' = if (createRoleAssignment == true) {
  name: 'functionFhirServiceRoleAssignment'
  params: {
    resourceId: fhirService.id
    // FHIR Contributor
    //roleId: '5a1fc7df-4bf1-4951-a576-89034ee01acd'

    //FHIR Importer
    roleId : '4465e953-8ced-4406-a58e-0f6e3f3b530b'
    principalId: functionApp.identity.principalId
  }
}

@description('Setup access between FHIR and the deployment script managed identity')
module functionApiForFhirRoleAssignment './roleAssignment.bicep' = if (createRoleAssignment == true) {
  name: 'bulk-import-function-fhir-managed-id-role-assignment'
  params: {
    resourceId: apiForFhir.id
    // FHIR Contributor
    //roleId: '5a1fc7df-4bf1-4951-a576-89034ee01acd'

    //FHIR Export
    roleId: '3db33094-8700-4567-8da5-1501d4e7e843'
    principalId: functionApp.identity.principalId
  }
}

var defaultHostKey = listkeys('${functionApp.id}/host/default', '2016-08-01').functionKeys.default
output functionAppKey string = defaultHostKey
output functionAppName string = functionAppName
output functionAppPrincipalId string = functionApp.identity.principalId
output hostName string = functionApp.properties.defaultHostName
output functionBaseUrl string = 'https://${functionApp.properties.defaultHostName}'
