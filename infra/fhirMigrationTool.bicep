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

@description('Name of new storage account.')
param newStorageAccountName string = ''

@description('Tenant ID where resources are deployed')
var tenantId = subscription().tenantId

@description('Tags for all Azure resources in the solution')
var appTags = {
  AppID: 'fhir-migration-function'
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
var funcStorName = '${replace(prefixNameCleanShort, '-', '')}funcsa'

@description('Any custom function app settings')
param functionAppCustomSettings object = {}

var fhirServiceNameUrl = 'https://${replace(fhirServiceName, '/', '-')}.fhir.azurehealthcareapis.com' 
var apiForFhirNameUrl = 'https://${apiForFhirName}.azurehealthcareapis.com'


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

@description('Deploy Azure Function to run Migration Tool')
module function './azureFunction.bicep'= {
    name: 'functionDeploy'
    params: {
        appServiceName: appServiceName
        functionAppName: functionAppName
        storageAccountName: funcStorName
        fhirServiceName : fhirServiceName
        apiForFhirName : apiForFhirName
        location: location
        appInsightsInstrumentationKey: monitoring.outputs.appInsightsInstrumentationKey
        functionSettings: union({
                AZURE_FhirServiceUrl: fhirServiceNameUrl
                AZURE_ApiForFhirUrl: apiForFhirNameUrl
                AZURE_InstrumentationKey: monitoring.outputs.appInsightsInstrumentationString
            }, functionAppCustomSettings)
        appTags: appTags
    }
}
