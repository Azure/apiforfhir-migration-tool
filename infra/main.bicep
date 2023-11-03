@minLength(1)
@maxLength(32)
@description('Name of the the environment which is used to generate a short unique hash used in all resources.')
param name string = 'mig'

@minLength(1)
@description('Primary location for all resources')
param location string = resourceGroup().location

@description('Name of the FHIR Service to import resources into. Format is "workspace/fhirService".')
param fhirServiceName string = ''

@description('Name of the API for FHIR to export resources from.')
param apiForFhirName string = ''

@description('Id of the FHIR Service to load resources into.')
param fhirid string = ''

@description('Id of the API for FHIR to load resources into.')
param apiForFhirid string = ''

@description('URL to the deployment package containing code to build the migration tool.')
#disable-next-line no-hardcoded-env-urls
param deploymentPackageUrl string = 'https://gen1export.blob.core.windows.net/zipdeploy/ApiForFhirMigrationTool.zip'

@description('Used if you want to use an existing Log Analytics Workspace.')
param existingLogAnalyticsWorkspaceName string = ''

@description('Any custom function app settings')
param functionAppCustomSettings object = {}

var envRandomString = toLower(uniqueString(subscription().id, name, location))
var nameShort = length(name) > 10 ? substring(name, 0, 10) : name
var resourceName = '${nameShort}${substring(envRandomString, 0, 6)}'

var appTags = {
  'app-id': 'fhir-migration-tool'
}

var logAnalyticsName = length(existingLogAnalyticsWorkspaceName) == 0 ? '${resourceName}-la' : existingLogAnalyticsWorkspaceName
var appInsightsName = '${resourceName}-appins'
var fhirServiceNameUrl = 'https://${replace(fhirServiceName, '/', '-')}.fhir.azurehealthcareapis.com' 
var apiForFhirNameUrl = 'https://${apiForFhirName}.azurehealthcareapis.com'

var fhirResourceIdSplit = split(fhirid,'/')
var apiforfhirResourceIdSplit = split(apiForFhirid,'/')

var fhirserviceRg = fhirResourceIdSplit[4]
var apiforFhirRg = apiforfhirResourceIdSplit[4]

@description('Deploy monitoring and logging')
module monitoring './monitoring.bicep'= {
    name: 'monitoringDeploy'
    params: {
        logAnalyticsName: logAnalyticsName
        appInsightsName: appInsightsName
        location: location
        appTags: appTags
    }
}

@description('Name for the App Service used to host the Function App.')
var appServiceName = '${resourceName}-appserv'

@description('Name for the Function App to deploy the Migration Tool.')
var functionAppName = '${resourceName}-func'

@description('Name for the storage account needed for the Function App')
var funcStorName = '${resourceName}funcsa'

@description('Deploy Azure Function to run Migration Tool')
module function './azureFunction.bicep'= {
    name: 'functionDeploy'
    params: {
        appServiceName: appServiceName
        functionAppName: functionAppName
        storageAccountName: funcStorName
        fhirServiceName : fhirServiceName
        apiForFhirName : apiForFhirName
        fhirserviceRg : fhirserviceRg
        apiforFhirRg : apiforFhirRg
        location: location
        appInsightsInstrumentationKey: monitoring.outputs.appInsightsInstrumentationKey
        functionSettings: union({
                AZURE_DestinationUri: fhirServiceNameUrl
                AZURE_SourceUri: apiForFhirNameUrl
                AZURE_AppInsightConnectionstring: monitoring.outputs.appInsightsInstrumentationString
            }, functionAppCustomSettings)
        appTags: appTags
        deploymentPackageUrl: deploymentPackageUrl
    }
}

@description('Setup access between FHIR and the deployment script managed identity')
module migToolDashboard './migrationToolDashboard.bicep' =  {
  name: 'mig-tool-dashboard'
  params: {
    applicationInsightsName: appInsightsName
    location: location
  }
}

// These map to user secrets for local execution of the program
output LOCATION string = location
output Azure_FunctionURL string = function.outputs.functionBaseUrl
