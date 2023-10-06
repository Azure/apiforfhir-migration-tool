# Incremental Copy Migration Tool

The incremental copy migration tool helps you contnuously copy data from an Azure API for FHIR server to an Azure Health Data Services FHIR service. This migration tool is an Azure function app solution that utilizes [$export](https://learn.microsoft.com/azure/healthcare-apis/azure-api-for-fhir/export-data) to export data from a source Azure API for FHIR server, and [$import](https://learn.microsoft.com/azure/healthcare-apis/fhir/import-data) to import to a destination Azure Health Data Services FHIR service.

The API for FHIR migration tool is an [Azure durable function](https://learn.microsoft.com/azure/azure-functions/durable/) application and uses [Azure storage table](https://learn.microsoft.com/azure/storage/tables/table-storage-overview) for capturing and maintaining the status of the export-import operation.

## Architecture Overview

![Architecture](images/Migration-tool-V1.2-Architecture.png)

# Prerequisites needed
1. Review [general migration strategies]( https://learn.microsoft.com/azure/healthcare-apis/fhir/migration-strategies) and [limitations and list of configurations to configure](/incremental-copy-docs/Appendix.md) first. 
1.	Microsoft work or school account
2.	FHIR instances.
	-	**Source**: Azure API for FHIR server instance from where the data will be exported from.
		- Get the Azure API for FHIR server URL:
			```PowerShell
			https://<<SOURCE_ACCOUNT_NAME>>.azurehealthcareapis.com/
			```
	-	**Destination**: Azure Health Data Services FHIR service instance where the data will be imported to. If you do not already have an Azure Health Data Services FHIR server, [deploy a FHIR service within Azure Health Data services](https://learn.microsoft.com/en-us/azure/healthcare-apis/fhir/fhir-portal-quickstart). Make sure that the Azure Health Data Services FHIR server is in the same subscription as the Azure API for FHIR.
		- Get the Azure Health Data Service FHIR service URL:
			```PowerShell
			https://<<WORKSPACE_NAME>>-<<FHIR_SERVICE_NAME>>.fhir.azurehealthcareapis.com/
			```
3. Make sure that the intermediate Azure storage account that you plan to use for this migration is selected as the $export storage account for FHIR instance. You can do this by configuring [$export](https://learn.microsoft.com/azure/healthcare-apis/azure-api-for-fhir/configure-export-data) on source FHIR instance (Azure API for FHIR server). If you do not already have a storage account, create a new storage account in the same subscription as your Azure API for FHIR server and follow the steps for configuring [$export](https://learn.microsoft.com/azure/healthcare-apis/azure-api-for-fhir/configure-export-data) to select that storage account.
    - Reminder: The Source (Azure API for FHIR server instance), Destination (Azure Health Data Services FHIR server), and the intermediate storage account need to all be in the same subscription.

4. Configure [$import](https://learn.microsoft.com/azure/healthcare-apis/fhir/configure-import-data) on the destination FHIR instance (Azure Health Data Service FHIR service server).

## Deployment
### Portal Deployment

To quickly deploy the Migration tool, you can use the Azure deployment below. Please note, if you are using Azure Private Link, please follow separate instructions for [deploying the migration tool with Azure Private Link](/incremental-copy-docs/private-link-sample/ReadMe.md).

1. Deploy the infrastructure for migration tool.
	1. Deploy the migration tool through Azure Portal using the Deploy to Azure button

		[![Deploy to Azure](https://aka.ms/deploytoazurebutton)](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2FAzure%2Fapiforfhir-migration-tool%2Fpersonal%2Fsnarang%2Finfrafix%2Finfra%2Fmain.json/createUIDefinitionUri/https%3A%2F%2Fraw.githubusercontent.com%2FAzure%2Fapiforfhir-migration-tool%2Fmain%2Finfra%2FuiDefForm.json)

    2. Or, deploy the migration tool manually

		- Follow steps to deploy the APIFORFHIR-Migration Tool:

			1. Clone this repo
				```azurecli
				git clone https://github.com/Azure/apiforfhir-migration-tool.git --depth 1
				```
			2. Log in to Azure
			- Before you begin, ensure that you are logged in to your Azure account. If you are not already logged in, follow these steps:
				```
				az login
				```
			3. Set the Azure Subscription
				- If you have multiple Azure subscriptions and need to specify which one to use for this deployment, use the az account set command:
					```
					az account set --subscription [Subscription Name or Subscription ID]
					```
				- Replace [Subscription Name or Subscription ID] with the name or ID of the subscription you want to use for this deployment. You can find your subscription information by running az account list.

				- **Note** : This step is particularly important if you have multiple subscriptions, as it ensures that the resources are deployed to the correct subscription.

			4. Create the Resource Group

				- Use the following command to create a resource group.
					```
					az group create --name <resource_group_name> --location <location>
					```
				- Replace <*resource_group_name*> with your desired name and <*location*> with the Azure region where you want to create the resource group

			5. Deploy the function
			- Now, you can initiate the deployment using the Azure CLI
				```
				az deployment group create --resource-group<resource-group-name> --template-file <path-to-template> --parameters <path-to-parameter>
				```
				- <*resource-group-name*>: Replace this with the name of the resource group you want to use.
				- <*path-to-template*>: Provide the path to your ARM/Bicep template file i.e. main.json under infra folder.
				- <*path-to-parameter*>: Specify the path to the parameters file i.e. armmain.parameters.json under infra folder.

## Export FHIR Data from API for FHIR server

The [built-in API for FHIR $export operation](https://learn.microsoft.com/azure/healthcare-apis/azure-api-for-fhir/export-data) is leveraged in this migration tool for exporting the data from API for FHIR server. The $export PaaS endpoints are asynchronous, long-running HTTP APIs. 
The storage account is used for staging NDJSON files between the $export and $import. The storage account is also used by Azure Durable Functions to store state. 

In the migration tool we are using $export [query](https://learn.microsoft.com/azure/healthcare-apis/azure-api-for-fhir/export-data#query-parameters) which allows you to filter and export certain data accordingly from a source Azure API for FHIR server.

The migration tool hits the HTTP APIs endpoint for the $export operation, the response contains export operation Content-Location URL. The content-location URL give the status on export operation. Each $export operation status is stored in Azure storage table.

The migration tool exports 30 days (configurable using parameter: ExportChunkTime ) of data from API for FHIR in each export operation. The user can configure the start date from where the export should start from the API for FHIR server. If the start date is not provided the tool will fetch the first resource date from the server and start the migration.

Once the $export operation is completed, the export operation content location is stored in Azure storage table and the next export status orchestrator in the durable function picks the details from the storage table and checks the status of the export.

The migration tool is also storing the _since and _till date for the export operation in Azure storage table. Once the export operation is completed, then the import operation orchestrator starts in the migration tool application.

## Import FHIR Data to Azure Health Data Services FHIR service

The [built-in Azure Health Data Service FHIR service $import operation](https://learn.microsoft.com/azure/healthcare-apis/fhir/import-data) is leveraged inthe  migration tool for importing the data to the destination Azure Health Data Services FHIR server.The $import PaaS endpoints are asynchronous, long-running HTTP APIs. 
The storage account is used for getting the NDJSON files between the $export and $import. The storage account is also used by Azure Durable Functions to store state. 

In the migration tool we are using $import for importing the data which got exported from the $export orchestrator.

The migration tool hits the HTTP APIs endpoint for the $import operation, the response contains import operation Content-Location URL. The content-location URL gives the status on import operation. Each $import operation status is stored in Azure storage table.

Once the $import operation is completed, the import operation content location is stored in Azure storage table and the next import status orchestrator in the durable function picks the details from storage table and checks the status of the import.

## Troubleshooting

1. Azure API for FHIR.
	-  Please see the [troubleshooting section](https://learn.microsoft.com/azure/healthcare-apis/fhir/export-data#troubleshoot) to handle issues on exporting the data.
2. Azure Health Data Services FHIR service.
	-  Please see the [troubleshooting section](https://learn.microsoft.com/azure/healthcare-apis/fhir/import-data#troubleshooting) to handle issues on importing the data.

## Data Movement Verification

You can verify that the data was successfully copied over using the below checks.

1. Surface Check <br>
    For a quick validation, you can use the surface check. It compares the number  of resources of a particular FHIR resource type between the API for FHIR and FHIR service. You can configure the name of the resource type in the parameter: SurfaceCheckResources

2. Deep Check <br>
    For a deeper look, you can use the deep check to compare the JSON data of a subset of data from API for FHIR server and Azure Health Data Services FHIR service.You can configure the number of resources that will be compared in the parameter: DeepCheckCount.


