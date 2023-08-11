targetScope = 'subscription'

@minLength(1)
@maxLength(6)
@description('Name of the the environment which is used to generate a short unique hash used in all resources.')
param prefix string

@minLength(1)
@description('Primary location for all resources')
param location string

@description('Name of the FHIR Service to import resources into. Format is "workspace/fhirService".')
param fhirServiceName string

@description('Name of the API for FHIR to export resources from.')
param apiForFhirName string

@description('Name of the FHIR Service to import resources into. Format is "workspace/fhirService".')
param storageAccountName string

@description('Name of the API for FHIR to export resources from.')
param newStorageAccountName string

@description('Name of your existing resource group (leave blank to create a new one)')
param existingResourceGroupName string = ''

var envRandomString = toLower(uniqueString(subscription().id, prefix, existingResourceGroupName, location))
var nameShort = length(prefix) > 11 ? substring(prefix, 0, 11) : prefix
var resourcePrefix = '${nameShort}-${substring(envRandomString, 0, 5)}'

var createResourceGroup = empty(existingResourceGroupName) ? true : false

var appTags = {
  'app-id': 'fhir-migration-tool'
}

resource resourceGroup 'Microsoft.Resources/resourceGroups@2021-04-01' = if (createResourceGroup) {
  name: '${resourcePrefix}-rg'
  location: location
  tags: appTags
}

resource existingResourceGroup 'Microsoft.Resources/resourceGroups@2021-04-01' existing = if (!createResourceGroup) {
  name: existingResourceGroupName
}

module template 'core.bicep'= if (createResourceGroup) {
  name: 'main'
  scope: resourceGroup
  params: {
    prefixName: resourcePrefix
    apiForFhirName: apiForFhirName
    fhirServiceName: fhirServiceName
    location: location
    appTags: appTags
    storageAccountExisting: storageAccountName
    storageAccountNew: newStorageAccountName
  }
}

module existingResourceGrouptemplate 'core.bicep'= if (!createResourceGroup) {
  name: 'mainExistingResourceGroup'
  scope: existingResourceGroup
  params: {
    prefixName: resourcePrefix
    apiForFhirName: apiForFhirName
    fhirServiceName: fhirServiceName
    location: location
    appTags: appTags
    storageAccountExisting: storageAccountName
    storageAccountNew: newStorageAccountName
  }
}

// These map to user secrets for local execution of the program
output LOCATION string = location
output Azure_FunctionURL string = createResourceGroup ? template.outputs.Azure_FunctionURL : existingResourceGrouptemplate.outputs.Azure_FunctionURL
