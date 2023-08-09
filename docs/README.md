# Migrating data from Azure API for FHIR server to Azure Health Data Services FHIR service

This sample will focus on how to migrate the FHIR data from Azure API for FHIR server to Azure Health Data Services FHIR service. This is an Azure function app solution that utilizes [$export](https://learn.microsoft.com/azure/healthcare-apis/azure-api-for-fhir/export-data) to export data from a source Azure API for FHIR server, and [$import](https://learn.microsoft.com/azure/healthcare-apis/fhir/import-data) to import to Azure Health Data Service FHIR service.

API for FHIR migration tool is an [Azure durable function](https://learn.microsoft.com/azure/azure-functions/durable/) application and uses [Azure storage table](https://learn.microsoft.com/azure/storage/tables/table-storage-overview) for capturing and maintaining the status of the export-import operation.

## Architecture Overview

![Architecture](Aimages/Migration-tool-V1.2-Architecture.png)

# Prerequisites needed
1.	Microsoft work or school account
2.	FHIR instances.
	-	**Source**: Azure API for FHIR server instance from where the data will be exported from.
		- Get the Azure API for FHIR server URL:
			```PowerShell
			https://<<SOURCE_ACCOUNT_NAME>>.azurehealthcareapis.com/
			```
	-	**Destination**: Azure Health Data Service FHIR service instance where the data will be imported to.
		- Get the Azure Health Data Service FHIR service URL:
			```PowerShell
			https://<<WORKSPACE_NAME>>-<<FHIR_SERVICE_NAME>>.fhir.azurehealthcareapis.com/
			```
## Deployment
### Portal Deployment

To quickly deploy the Migration tool, you can use the Azure deployment below. This deployment method provides simple configuration.

1. Deploy the infrastructure for migration tool.
	1. Deploy the migration tool using deploy to azure button
    2. Deploy the migration tool manually

		- Follow the steps to deploy the APIFORFHIR-Migration Tool:

			1. Clone this repo
				```azurecli
				git clone https://github.com/Azure/apiforfhir-migration-tool.git --depth 1
				```
			2. Open the cloned project in Visual Studio.
			3. Sign into your Azure account in visual studio [link](https://learn.microsoft.com/visualstudio/azure/how-to-sign-in-with-azure-subscription?view=vs-2022).
			4. Configuration setting
			5. Publish the function from visual studio [link](https://learn.microsoft.com/en-us/azure/azure-functions/functions-develop-vs?tabs=in-process#publish-to-azure).
			6. Once the publish of function app completed. The migration app will auto start the export-import process as the function app is time trigger.

## Export FHIR Data from API for FHIR server

The [built-in API for FHIR $export operation](https://learn.microsoft.com/azure/healthcare-apis/azure-api-for-fhir/export-data) is leveraged in migration tool for exporting the data from API for FHIR server.The $export PaaS endpoints are asynchronous, long-running HTTP APIs. 
The storage account is used for staging NDJSON files between the $export and $import. The storage account is also used by Azure Durable Functions to store state. 

In the migration tool we are using $export [query](https://learn.microsoft.com/azure/healthcare-apis/azure-api-for-fhir/export-data#query-parameters) which allows you to filter and export certain data accordingly from a source Azure API for FHIR server.

The migration tool hit the HTTP APIs endpoint for the $export operation, the response contain export operation Content-Location URL. The content-location URL give the status on export operation. Each $export operation status are stored in Azure storage table.

The migration tool export 30 day (configurable using parameter: ExportChunkTime ) data from API for FHIR in each export operation. The user can configure the start date from where the export should start from the API for FHIR server. If the start date is not provided the tool will fetch the first resource date from the server and start the migration.

Once the $export operation is completed, the export operation content location is stored in Azure storage table and next export status orchestrator in durable function pick the details from storage table and check the status of the export.

The migration tool is also storing the since and till date for the export operation in azure storage table. Once the export operation is completed, then the import operation orchestrator started in migration tool application.

## Import FHIR Data to Azure Health Data Services FHIR service

The [built-in Azure Health Data Service FHIR service $import operation](https://learn.microsoft.com/azure/healthcare-apis/fhir/import-data) is leveraged in migration tool for importing the data to FHIR server.The $import PaaS endpoints are asynchronous, long-running HTTP APIs. 
The storage account is used for getting the NDJSON files between the $export and $import. The storage account is also used by Azure Durable Functions to store state. 

In the migration tool we are using $import for importing the data which got exported from the $export orchestrator.

The migration tool hit the HTTP APIs endpoint for the $import operation, the response contain import operation Content-Location URL. The content-location URL give the status on import operation. Each $import operation status are stored in Azure storage table.

Once the $import operation is completed, the import operation content location is stored in Azure storage table and next import status orchestrator in durable function pick the details from storage table and check the status of the import.

## Troubleshooting

1. Azure API for FHIR.
	-  Please see the [troubleshooting section](https://learn.microsoft.com/azure/healthcare-apis/fhir/export-data#troubleshoot) to handle issues on exporting the data.
2. Azure Health Data Services FHIR service.
	-  Please see the [troubleshooting section](https://learn.microsoft.com/azure/healthcare-apis/fhir/import-data#troubleshooting) to handle issues on importing the data.

## Data Movement Verification

In Migration tool the data movement verfication can be done using below checks.

1. Deep Check 
    The Deep check is the process which verfiy the FHIR json by comparing the data from API for FHIR server and Azure Health data services FHIR service.
    The number of FHIR resources that will be compared is configurable. You can put the number as per the requirement in the parameter: DeepCheckCount.

2. Surface Check
    The Surface check is the process which verfiy the number of particular resource count in the FHIR server. It compares the number of the FHIR resource between the API for FHIR and FHIR service. The name of the resources for which the count will be check between the server is configurable. You can put the name of the resources as per the requirement in the parameter: SurfaceCheckResources
