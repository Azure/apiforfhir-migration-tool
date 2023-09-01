targetScope = 'subscription'

@minLength(1)
@maxLength(64)
@description('Name of the the environment which is used to generate a short unique hash used in all resources.')
param name string

@minLength(1)
@description('Primary location for all resources')
param location string

@description('Name of the FHIR Service to import resources into. Format is "workspace/fhirService".')
param fhirServiceName string

@description('Name of the API for FHIR to export resources from.')
param apiForFhirName string

@description('Name of your existing resource group (leave blank to create a new one)')
param existingResourceGroupName string = ''

@description('Git repository URL for the FHIR resources to import. For private repos, do https://{github-username}:{access-token}@github.com/{organisation-acount}/{repo}.git')
@secure()
param repoUrl string

var envRandomString = toLower(uniqueString(subscription().id, name, existingResourceGroupName, location))
var nameShort = length(name) > 11 ? substring(name, 0, 11) : name
var resourcePrefix = '${nameShort}-${substring(envRandomString, 0, 5)}'

var createResourceGroup = empty(existingResourceGroupName) ? true : false

var appTags = {
  'app-id': 'fhir-migration-tool'
}

resource resourceGroup 'Microsoft.Resources/resourceGroups@2021-04-01' = if (createResourceGroup) {
  name: '${name}-rg'
  location: location
  tags: appTags
}

resource existingResourceGroup 'Microsoft.Resources/resourceGroups@2021-04-01' existing = if (!createResourceGroup) {
  name: existingResourceGroupName
}

module template 'core.bicep' = if (createResourceGroup) {
  name: 'main'
  scope: resourceGroup
  params: {
    prefixName: resourcePrefix
    apiForFhirName: apiForFhirName
    fhirServiceName: fhirServiceName
    location: location
    appTags: appTags
    repoUrl: repoUrl
  }
}

module existingResourceGrouptemplate 'core.bicep' = if (!createResourceGroup) {
  name: 'mainExistingResourceGroup'
  scope: existingResourceGroup
  params: {
    prefixName: resourcePrefix
    apiForFhirName: apiForFhirName
    fhirServiceName: fhirServiceName
    location: location
    appTags: appTags
    repoUrl: repoUrl
  }
}

// These map to user secrets for local execution of the program
output LOCATION string = location
output Azure_FunctionURL string = createResourceGroup ? template.outputs.Azure_FunctionURL : existingResourceGrouptemplate.outputs.Azure_FunctionURL
