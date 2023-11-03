@description('Prefix for all resources')
param prefix string = 'bulk'

@description('Location for all resources.')
param location string = resourceGroup().location

@description('Name of the FHIR Service to load resources into. Format is "workspace/fhirService".')
param fhirServiceName string = ''

@description('Name of the API for FHIR to load resources into.')
param apiForFhirName string = ''

@description('Name of existing storage account.')
param storageAccountName string = ''

@description('Tenant ID where resources are deployed')
var tenantId = subscription().tenantId

@description('Tags for all Azure resources in the solution')
var appTags = {
  AppID: 'fhir-migration-tool'
}

var uniqueResourceIdentifier = substring(uniqueString(resourceGroup().id, prefix), 0, 4)
var prefixNameClean = '${replace(prefix, '-', '')}${uniqueResourceIdentifier}'
var prefixNameCleanShort = length(prefixNameClean) > 16 ? substring(prefixNameClean, 0, 8) : prefixNameClean

@description('Name of the Log Analytics workspace to deploy or use. Leave blank to skip deployment')
var logAnalyticsName = '${prefixNameCleanShort}-la'

@description('Name for app insights resource used to monitor the Function App')
var appInsightsName = '${prefixNameCleanShort}-appins'

@description('Name for the App Service used to host the Function App.')
var appServiceName = '${prefixNameCleanShort}-appserv'

@description('Name for the Function App to deploy the Migration Tool.')
var functionAppName = '${prefixNameCleanShort}-func'

@description('Name for the storage account needed for the Function App')
var funcStoreName = '${replace(prefixNameCleanShort, '-', '')}funcsa'

@description('Any custom function app settings')
param functionAppCustomSettings object = {}

@description('Automatically create a role assignment for the function app to access the FHIR service and API for FHIR.')
param createRoleAssignment bool = true

var fhirServiceNameUrl = 'https://${replace(fhirServiceName, '/', '-')}.fhir.azurehealthcareapis.com' 
var apiForFhirNameUrl = 'https://${apiForFhirName}.azurehealthcareapis.com'

@description('Storage account to configure for export from Azure API for FHIR and import to FHIR Service')
resource newFhirStorageAccount 'Microsoft.Storage/storageAccounts@2021-08-01' = {
    name: storageAccountName
    location: location
    kind: 'StorageV2'
    sku: {
        name: 'Standard_LRS'
    }
    tags: appTags
}

// resource fhirStorageAccount 'Microsoft.Storage/storageAccounts@2021-08-01' existing = if (length(storageAccountName) > 0) {
//   name: storageAccountName
// }

resource apiForFhir 'Microsoft.HealthcareApis/services@2021-11-01' = {
  name: apiForFhirName
  kind: 'fhir-R4'
  location: location
  properties: {
    exportConfiguration: {
      storageAccountName: newFhirStorageAccount.name
    }
  }
}

resource fhirService 'Microsoft.HealthcareApis/workspaces/fhirservices@2022-06-01' = {
  //#disable-next-line prefer-interpolation
  name: fhirServiceName
  location: location
  kind: 'fhir-R4'
  properties: {
    authenticationConfiguration: {
      audience: fhirServiceNameUrl
      authority: uri(environment().authentication.loginEndpoint, tenantId)
    }
    importConfiguration: {
      enabled: true
      integrationDataStore: newFhirStorageAccount.name
    }
  }
}

@description('Logging workspace for FHIR and Function App')
resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2020-03-01-preview' = {
  name: logAnalyticsName
  location: location
  properties: {
    retentionInDays: 30
    sku: {
      name: 'PerGB2018'
    }
  }
  tags: appTags
}

@description('General log setting for workspace')
resource logAnalyticsWorkspaceDiagnostics 'Microsoft.Insights/diagnosticSettings@2017-05-01-preview' = if (length(logAnalyticsName) > 0) {
  name: 'diagnosticSettings'
  location: location
  scope: logAnalyticsWorkspace
  properties: {
    workspaceId: logAnalyticsWorkspace.id
    logs: [
      {
        category: 'Audit'
        enabled: true
        retentionPolicy: {
          days: 30
          enabled: true
        }
      }
    ]
    metrics: [
      {
        category: 'AllMetrics'
        enabled: true
        retentionPolicy: {
          days: 30
          enabled: true
        }
      }
    ]
  }
}

@description('Monitoring for Function App')
resource appInsights 'Microsoft.Insights/components@2020-02-02-preview' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalyticsWorkspace.id
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
  tags: appTags
}

@description('Azure Function required linked storage account')
resource funcStorageAccount 'Microsoft.Storage/storageAccounts@2021-08-01' = {
    name: funcStoreName
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

@description('Azure Function used to run Migration Tool')
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
        clientAffinityEnabled: false
        siteConfig: {
            linuxFxVersion: 'dotnet-isolated|7.0'
            use32BitWorkerProcess: false
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
}

resource functionAppSettings 'Microsoft.Web/sites/config@2020-12-01' = {
  name: 'appsettings'
  location: location
  parent: functionApp
  properties: union({
          AzureWebJobsStorage: 'DefaultEndpointsProtocol=https;AccountName=${funcStorageAccount.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${funcStorageAccount.listKeys().keys[0].value}'
          FUNCTIONS_EXTENSION_VERSION: '~4'
          FUNCTIONS_WORKER_RUNTIME: 'dotnet-isolated'
          AppInsightConnectionKey: appInsights.properties.InstrumentationKey
          AppInsightConnectionString: 'InstrumentationKey=${appInsights.properties.ConnectionString}'
          SCM_DO_BUILD_DURING_DEPLOYMENT: 'false'
          ENABLE_ORYX_BUILD: 'false'
          WEBSITE_RUN_FROM_PACKAGE: 1
          DestinationFhirUri: format('https://{0}.fhir.azurehealthcareapis.com', replace(fhirServiceName, '/', '-'))
          SourceFhirUri: format('https://{0}.azurehealthcareapis.com', apiForFhirName)
          StartDate: '1970-01-01'
          ScheduleInterval: 5
          ImportMode: 'IncrementalLoad'
          SurfaceCheckResources: ''
          DeepCheckCount: ''
          UserAgent: 'FhirMigrationTool'
          SourceHttpClient: 'SourceFhirEndpoint'
          DestinationHttpClient: 'DestinationFhirEndpoint'
          TokenCredential: ''
          RetryCount: 3
          WaitForRetry: 1
          Debug: true
          ExportTableName: 'export'
          ChunkTableName: 'doChunk'
          ExportChunkTime: 12
          PartitionKey: 'partitionkey'
          RowKey: 'rowkey'
      }, functionAppCustomSettings)
}

@description('Setup access between FHIR and the deployment script managed identity')
module functionFhirServiceRoleAssignment './roleAssignment.bicep' = if (createRoleAssignment == true) {
  name: 'functionFhirServiceRoleAssignment'
  params: {
    resourceId: fhirService.id
    //FHIR Importer
    roleId : '3db33094-8700-4567-8da5-1501d4e7e843'
    principalId: functionApp.identity.principalId
  }
}

@description('Setup access between FHIR and the deployment script managed identity')
module functionApiForFhirRoleAssignment './roleAssignment.bicep' = if (createRoleAssignment == true) {
  name: 'bulk-import-function-fhir-managed-id-role-assignment'
  params: {
    resourceId: apiForFhir.id
    //FHIR Export
    roleId: '3db33094-8700-4567-8da5-1501d4e7e843'
    principalId: functionApp.identity.principalId
  }
}

@description('Setup access between FHIR and the deployment script managed identity')
module migToolDashboard './migrationToolDashboard.bicep' =  {
  name: 'mig-tool-dashboard'
  params: {
    applicationInsightsName: appInsights.name
    location: location
  }
}

output Azure_FunctionURL string = 'https://${functionApp.properties.defaultHostName}'
