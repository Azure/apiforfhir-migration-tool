@description('Prefix for resources deployed by this solution (App Service, Function App, monitoring, etc)')
param prefixName string = 'hdssdk${uniqueString(resourceGroup().id)}'

@description('Name of the FHIR service to deloy or use.')
param fhirServiceName string

@description('Name of the API for FHIR to export resources from.')
param apiForFhirName string

@description('Name of the Log Analytics workspace to deploy or use. Leave blank to skip deployment')
param logAnalyticsName string = '${prefixName}-la'

@description('Location to deploy resources')
param location string = resourceGroup().location

@description('Location to deploy resources')
param appTags object = {}

@description('Any custom function app settings')
param functionAppCustomSettings object = {}

@description('Tenant ID where resources are deployed')
var tenantId  = subscription().tenantId

@description('Name for app insights resource used to monitor the Function App and APIM')
var appInsightsName = '${prefixName}-appins'

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

@description('Name for the App Service used to host the Function App.')
var appServiceName = '${prefixName}-appserv'

@description('Name for the Function App to deploy the SDK custom operations to.')
var functionAppName = '${prefixName}-func'

@description('Name for the storage account needed for the Function App')
var funcStorName = '${replace(prefixName, '-', '')}funcsa'

@description('Deploy Azure Function to run SDK custom operations')
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
                AZURE_apiForFhirUrl: apiForFhirNameUrl
                AZURE_InstrumentationKey: monitoring.outputs.appInsightsInstrumentationString
            }, functionAppCustomSettings)
        appTags: appTags
    }
}

output Azure_FunctionURL string = function.outputs.functionBaseUrl

