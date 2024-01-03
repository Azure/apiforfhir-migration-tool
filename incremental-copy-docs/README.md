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
3. Configure [$export](https://learn.microsoft.com/azure/healthcare-apis/azure-api-for-fhir/configure-export-data) on the origin Azure API for FHIR server. Make sure that the intermediate Azure storage account that you plan to use for this migration is selected as the $export storage account for FHIR instance. You can do this by configuring [$export](https://learn.microsoft.com/azure/healthcare-apis/azure-api-for-fhir/configure-export-data) on source FHIR instance (Azure API for FHIR server). If you do not already have a storage account, create a new storage account in the same subscription as your Azure API for FHIR server and follow the steps for configuring [$export](https://learn.microsoft.com/azure/healthcare-apis/azure-api-for-fhir/configure-export-data) to select that storage account.
    - Reminder: The Source (Azure API for FHIR server instance), Destination (Azure Health Data Services FHIR server), and the intermediate storage account need to all be in the same subscription.

4. Configure [$import](https://learn.microsoft.com/azure/healthcare-apis/fhir/configure-import-data) on the destination FHIR instance (Azure Health Data Service FHIR service server).
> [!IMPORTANT]  
> Please ensure that your $import is set to **incremental import mode** in order for the migration tool to work. If needed, you may switch back to initial import mode post-migration. Set incremental import mode following these [instructions](https://learn.microsoft.com/en-us/azure/healthcare-apis/fhir/configure-import-data#step-3b-set-import-configuration-for-incremental-import-mode). Learn more about incremental and initial import [here](https://learn.microsoft.com/en-us/azure/healthcare-apis/fhir/import-data). 

## Deployment
### Portal Deployment

To quickly deploy the Migration tool, you can use the Azure deployment below. Please note, if you are using Azure Private Link, please follow separate instructions for [deploying the migration tool with Azure Private Link](/incremental-copy-docs/private-link-sample/ReadMe.md).

1. Deploy the infrastructure for migration tool.
	1. Deploy the migration tool through Azure Portal using the Deploy to Azure button

		[![Deploy to Azure](https://aka.ms/deploytoazurebutton)](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2FAzure%2Fapiforfhir-migration-tool%2Fmain%2Finfra%2Fmain.json/createUIDefinitionUri/https%3A%2F%2Fraw.githubusercontent.com%2FAzure%2Fapiforfhir-migration-tool%2Fmain%2Finfra%2FuiDefForm.json)

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

The migration tool exports 30 days which is configurable by using parameters: ExportChunkTime and ExportChunkDuration of data from API for FHIR in each export operation.

#### How to configure Chunk Duration and Time for export.
1. Open the Data migration Azure function.
2. Go to the environemnt variable setting and under it go to App Setting.
3. Set the below configuration as per the need:
```
Name: AZURE_ExportChunkDuration
Value: "Days" or "Hours" or "Minutes"

Name: AZURE_ExportChunkTime
Value: <<Int number>>
```
AZURE_ExportChunkDuration it can take Days, Hours or Minutes as value. If the user want the data to be exported on Days basis, put "Days" as value in it. If the user want data to be exported on Hourly basis, put "Hours" as value OR if user want to export in minutes put "Minutes" as value.

AZURE_ExportChunkTime it will take integer as value.

Example:

Below setting in azure function will export 30 days data in single chunk: 
```
Name: AZURE_ExportChunkDuration
Value: "Days"

Name: AZURE_ExportChunkTime
Value: 30
```

The user can configure the start date in Azure function from where the export should start from the API for FHIR server. AZURE_StartDate will help to export the data from that specific date. <br>
If the start date is not provided the tool will fetch the first resource date from the server and start the migration.

Once the $export operation is completed, the export operation content location is stored in Azure storage table and the next export status orchestrator in the durable function picks the details from the storage table and checks the status of the export.

The migration tool is also storing the _since and _till date for the export operation in Azure storage table. Once the export operation is completed, then the import operation orchestrator starts in the migration tool application.

New export will not start once the previous import is completed on FHIR service.

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

## Monitoring

During the deployment of data migration tool , the dashboard is also deployed for monitoring the data migration from Azure API for FHIR to Azure Health Data Service FHIR service. This dashboard gives the overview and details of each export-import runs.

![Architecture](images/Dasboard.png)

Dasboard contain below details.

1. Export Operation Status.
	- Completed export counts - This gives the count of export executed on Azure API for FHIR.
	- Completed export details - This gives the details of each export executed on Azure API for FHIR.
	- Running export count - This gives the current export run count on Azure API for FHIR.
	- Running export details - This gives the details of current export run count on Azure API for FHIR.
	- Failed export count - This gives the failed export run count on Azure API for FHIR.
	- Failed export details - This gives the details of failed export run count on Azure API for FHIR.
2. Import Operation Status.
	- Completed Import counts - This gives the count of import executed on FHIR service.
	- Completed Import details - This gives the details of each import executed on FHIR service.
	- Running import count - This gives the current import run count on FHIR service
	- Running import details - This gives the details of current import run count on FHIR service.
	- Failed import count - This gives the failed import run count on FHIR service.
	- Failed import details - This gives the details of failed import run count FHIR service.
3. Surface Check - This give the details of surface check run for data movement verification.
4. Function App
	- Failures
	- Server exceptions and dependency failures
	- Avg processor / CPU utilization
	- Average available memory.

## Data Movement Verification

You can verify that the data was successfully copied over using the below checks.

1. Surface Check <br>
    For a quick validation, you can use the surface check. It compares the number  of resources of a particular FHIR resource type between the API for FHIR and FHIR service. You can configure the name of the resource type in the parameter: SurfaceCheckResources. 

	To configure SurfaceCheckResources paramter, follow below steps:

	1. Open the Data migration azure function.
	2. Go the the environment variable. Under App setting set the below configuration:

	```
	Name: AZURE_SurfaceCheckResources
	Value: ["<<Resource1 Name>>", "<<Resource2 Name>>"]
	```

	Value can contain list of resources that will check the count on both the server.

	3. Save the setting and hit the E2ETest_Http function. Below are the steps how to run it.

2. Deep Check <br>
    For a deeper look, you can use the deep check to compare the JSON data of a subset of data from API for FHIR server and Azure Health Data Services FHIR service.You can configure the number of resources that will be compared in the parameter: DeepCheckCount.

	To configure DeepCheckCount paramter, follow below steps:

	1. Open the Data migration azure function.
	2. Go the the environment variable. Under App setting set the below configuration:

	```
	Name: AZURE_DeepCheckCount
	Value: <<Resource Count>>
	```
	The number of complete fhir resources that need to be compared on both the server.

	3. Save the setting and hit the E2ETest_Http function. Below are the steps how to run it.

	### How to run surface and deep check

	1. Once the data migration from Azure API for FHIR to Azure Health Data Service is complete. Run the below steps to verfiy the data movement.

		1. Go to your Data migration Azure function and open E2ETest_Http function.
		![Architecture](images/E2E-S&D-1.png)
		2. Get the E2ETest_Http function URL.
		![Architecture](images/E2E-S&D-2.png)
		3. Hit the E2ETest_Http function URL directly on browser or from Postman.
		4. There will be response containing statusQueryGetUri. Copy the uri. This URI shows the status of the function which has been hit.
		5. Hit the URL to check the status of surface and deep check.
		6. Once the statusQueryGetUri response runtimeStatus is complete. There will be output for surface and deep check which will contain the resources checks for both the server.
		
## Stop Migration Tool

You can stop the migration tool once the data migration from Azure API for FHIR instance to Azure Health Data Service FHIR service is completed.

You can verfiy the data migration completion from Data Movement Verification step mentioned above.

Please follow below step to stop the migration tool.

### Azure Portal
1. Go to the resource group on Azure Portal where the data migration tool is deployed.
2. Open the data migration Azure function.
3. Click on Overview of Function App.
4. Click on stop and then click yes to stop the web app on prompt.
![Architecture](images/Stop-Migration.png)

### Azure CLI

Below command can be run through Azure Cloud Shell or locally.

1. Before you begin, ensure that you are logged in to your Azure account. If you are not already logged in, follow these steps:
```
az login
```
```
az account set --subscription [Subscription Name or Subscription ID]
```
2. Run the below command to stop the data migration tool Azure function app.<br>
Pass the function App name and resource group name as parameter to command.
```
az functionapp stop --name <<MyFunctionApp>>--resource-group <<MyResourceGroup>>
```