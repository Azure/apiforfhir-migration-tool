@minLength(1)
@maxLength(32)
@description('Name of the the environment which is used to generate a short unique hash used in all resources.')
param name string = 'mig'

@minLength(1)
@description('Primary location for all resources')
param location string = resourceGroup().location

@description('Type Of Migration')
@allowed([
  'AzureAPIforFhir'
  'FhirService'
])
param typeOfMigration string = 'AzureAPIforFhir'

@description('Name of the FHIR Service from resources need to be exported. Format is "workspace/fhirService".')
param sourceFhirServiceName string = ''

@description('Name of the FHIR Service to import resources into. Format is "workspace/fhirService".')
param fhirServiceName string = ''

@description('Name of the API for FHIR to export resources from.')
param apiForFhirName string = ''

@description('Id of the FHIR Service to load resources into.')
param sourcefhirid string = ''

@description('Id of the FHIR Service to load resources into.')
param fhirid string = ''

@description('Id of the API for FHIR to load resources into.')
param apiForFhirid string = ''

@description('Repo URL containing code to build the migration tool.')
#disable-next-line no-hardcoded-env-urls
param deploymentRepoUrl string = 'https://github.com/Azure/apiforfhir-migration-tool/'

@description('Used if you want to use an existing Log Analytics Workspace.')
param existingLogAnalyticsWorkspaceName string = ''

@description('Any custom function app settings')
param functionAppCustomSettings object = {}

@description('Export With History')
param exportWithHistory bool 

@description('Export With Delete')
param exportWithDelete bool 

@description('Export Using isParallel')
param isParallel bool 

@description('Export deidentified data')
param exportDeidentified bool

@description('Name of Configuration file')
param configFile string = ''

@description('Export by timestamp')
param stopDm bool

@description('Indicates the start time for the hours when export and import operations are restricted')
param startTime int = 8

@description('Indicates the end time for the hours when export and import operations are restricted ')
param endTIme int = 17

@description('Export by timestamp')
param specificRun bool = false

@description('Indicates the start time for the hours when export and import operations are restricted')
param startDate string

@description('Indicates the end time for the hours when export and import operations are restricted ')
param endDate string

@description('Indicates if client credentials need to be used for Azure api for fhir')
param useClientCredentials bool
    
@description('Indicates client id for client credentials')
param clientId string

@secure()
@description('Indicates client secret for client credentials')
param clientSecret string

@description('Indicates client secret for tenant id')
param tenantId string 

var envRandomString = toLower(uniqueString(subscription().id, name, location))
var nameShort = length(name) > 10 ? substring(name, 0, 10) : name
var resourceName = '${nameShort}${substring(envRandomString, 0, 6)}'

var appTags = {
  'app-id': 'fhir-migration-tool'
}

var logAnalyticsName = length(existingLogAnalyticsWorkspaceName) == 0 ? '${resourceName}-la' : existingLogAnalyticsWorkspaceName
var appInsightsName = '${resourceName}-appins'
var sourcefhirServiceNameUrl = 'https://${replace(sourceFhirServiceName, '/', '-')}.fhir.azurehealthcareapis.com' 
var fhirServiceNameUrl = 'https://${replace(fhirServiceName, '/', '-')}.fhir.azurehealthcareapis.com' 
var apiForFhirNameUrl = 'https://${apiForFhirName}.azurehealthcareapis.com'

var fhirResourceIdSplit = split(fhirid,'/')
var sourcefhirResourceIdSplit = split(sourcefhirid,'/')
var apiforfhirResourceIdSplit = split(apiForFhirid,'/')

var fhirserviceRg = fhirResourceIdSplit[4]
var sourcefhirserviceRg = typeOfMigration == 'FhirService' ? sourcefhirResourceIdSplit[4]: ''
var apiforFhirRg = typeOfMigration == 'AzureAPIforFhir' ? apiforfhirResourceIdSplit[4]: ''

var fhirsubid= fhirResourceIdSplit[2]
var sourcefhirsubid= typeOfMigration == 'FhirService' ? sourcefhirResourceIdSplit[2]: ''
var apiForFhirsubid= typeOfMigration == 'AzureAPIforFhir'? apiforfhirResourceIdSplit[2]: ''

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
        typeOfMigration: typeOfMigration
        appServiceName: appServiceName
        functionAppName: functionAppName
        storageAccountName: funcStorName
        fhirServiceName : fhirServiceName
        sourceFhirServiceName: sourceFhirServiceName
        apiForFhirName : apiForFhirName
        sourcefhirserviceRg: sourcefhirserviceRg
        fhirserviceRg : fhirserviceRg
        apiforFhirRg : apiforFhirRg
        location: location
        exportWithHistory : exportWithHistory
        exportWithDelete : exportWithDelete
        isParallel :isParallel
        exportDeidentified:exportDeidentified
        stopDm:stopDm
        startTime:startTime
        endTime:endTIme
        specificRun: specificRun
        startDate: startDate
        endDate: endDate
        configFile:configFile
        appInsightsConnectionString: monitoring.outputs.appInsightsInstrumentationString
        functionSettings: union({
                AZURE_DestinationUri: fhirServiceNameUrl
                AZURE_SourceUri: typeOfMigration == 'AzureAPIforFhir'? apiForFhirNameUrl: sourcefhirServiceNameUrl
                AZURE_AppInsightConnectionstring: monitoring.outputs.appInsightsInstrumentationString
            }, functionAppCustomSettings)
        appTags: appTags
        deploymentRepoUrl: deploymentRepoUrl
        fhirsubid:fhirsubid
        sourcefhirsubid: sourcefhirsubid
        apiForFhirsubid:apiForFhirsubid
        useClientCredentials:useClientCredentials
        clientId:clientId
        clientSecret:clientSecret
        tenantId:tenantId
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
