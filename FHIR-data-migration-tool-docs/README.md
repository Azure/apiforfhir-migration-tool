# FHIR Data Migration Tool

# 1. Overview

The FHIR data migration tool helps you continuously copy data from an Azure API for FHIR server to an Azure Health Data Services FHIR service. This migration tool is an Azure function app solution that utilizes [$export](https://learn.microsoft.com/azure/healthcare-apis/azure-api-for-fhir/export-data) to export data from a source Azure API for FHIR server, and [$import](https://learn.microsoft.com/azure/healthcare-apis/fhir/import-data) to import to a destination Azure Health Data Services FHIR service.

The API for FHIR migration tool is an [Azure durable function](https://learn.microsoft.com/azure/azure-functions/durable/) application and uses [Azure storage table](https://learn.microsoft.com/azure/storage/tables/table-storage-overview) for capturing and maintaining the status of the export-import operation.

### FHIR Data Migration Tool Overview
The OSS migration tool is an Azure Durable Function-based tool layered on top of existing FHIR server \$export and \$import functionality to orchestrate one-way migration of FHIR data. It continuously migrates new data to give you time to test your new FHIR server with your data, and flexibility to align your cutover with your organization’s existing maintenance windows.

At a high level, this migration pattern involves:

1. Start moving chunks of data from Azure API for FHIR to Azure Health Data Services. The Azure API for FHIR can continue having new writes, updates, and soft deletes during this process. As long as [incremental mode $import](https://learn.microsoft.com/en-us/azure/healthcare-apis/fhir/import-data#incremental-mode) is used, the writes, updates, and soft deletes that happen during the migration will be copied over to your new FHIR server. However, hard deletes that happen during the migration process will not be automatically copied over. Please see the [Appendix](/FHIR-data-migration-tool-docs/Appendix.md) for more information on hard deletes.
2. Continuously copy new data from Azure API for FHIR to Azure Health Data Services. 
3. After a majority of the data has been copied, stop all writes to Azure API for FHIR. Wait for the final $export/$imports to complete. 
4. Cutover and point all applications and workloads to the new Azure Health Data Services FHIR service.
5. Decommission Azure API for FHIR and stop the migration tool. 

## Architecture Overview

![Architecture](images/Migration-tool-V1.2-Architecture.png)

### Concepts
The  FHIR data migration tool executes a series of smaller export-import rounds in succession in order to continuously copy over **chunks** of data (default chunk is set at 30 days of data), checking every five minutes to check if the last export-import finished, and if there is new data to migrate. The five minute interval is set by the **orchestrator**. 
  - **Chunks**: The migration tool will "chunk" the data into 30-day segments (based on the resources' lastUpdated timestamp) for each round of export-import. The default is 30 days and can be adjusted (more information below). Reducing the size of the export and imports helps with the efficiency of the migration tool and helps to minimize errors.
  - **Orchestrator**: Immediately after deploying the migration tool, the migration tool will find the earliest "chunk" of data and kick off a export, followed by an import. The orchestrator then checks every 5 minutes to see if the previous export-import round has finished. If the previous export-import round has indeed finished, it will kick off the migration of the next "chunk" of data to migrate, with the process continuing on and on until you choose to end the migration tool. The orchestrator will check every 5 minutes to see if there is new data since the last export-import in the origin Azure API for FHIR server to migrate over to the destination Azure Health Data Services FHIR server. This way, you can keep your Azure API for FHIR server up and running during the migration process, and choose exactly when to cut over to the new FHIR server.

## Deployed Components
During the deployment of the FHIR data migration tool, the following components will be deployed:

1. Azure Functions app
	- The data migration tool code is deployed in Azure Functions. The migration tool function app acts as the orchestrator.
2. Storage account
	- A new storage account will be linked to the migration tool function app and will be used to store and monitor the export-import data. The table storage inside this new storage account will capture and store the details for each export-import round.  This new storage account will be different from the storage account that you designated for export-import storage location.
3. Shared Dashboard
	- The dashboard captures and visualizes the details for each export-import of data. 

4. Application Insights
	- This will capture the logs of the migration tool function app. 


# 2. Pre-Migration

## Prerequisites needed (all scenarios)
1. Review [general migration strategies]( https://learn.microsoft.com/azure/healthcare-apis/fhir/migration-strategies) and [limitations and list of configurations to configure](/FHIR-data-migration-tool-docs/Appendix.md) first. 
2.	Microsoft work or school account
3.	FHIR instances.
	-	**Source**: Azure API for FHIR server instance from where the data will be exported from.
		- Get the Azure API for FHIR server URL:
			```PowerShell
			https://<<SOURCE_ACCOUNT_NAME>>.azurehealthcareapis.com/
			```
	-	**Destination**: Azure Health Data Services FHIR service instance where the data will be imported to. If you do not already have an Azure Health Data Services FHIR server, [deploy a FHIR service within Azure Health Data services](https://learn.microsoft.com/en-us/azure/healthcare-apis/fhir/fhir-portal-quickstart). 
	
		- Get the Azure Health Data Service FHIR service URL:
			```PowerShell
			https://<<WORKSPACE_NAME>>-<<FHIR_SERVICE_NAME>>.fhir.azurehealthcareapis.com/
			```
    
4. Storage account needs to be set as both the export location for Azure API for FHIR and the import location for Azure Health Data Services FHIR service (5 and 6 below). 
  - Note: The migration tool supports migration with Azure API for FHIR and Azure Health Data Services FHIR servers that are within the same subscription or across different subscriptions, as long as they are in the same Tenant ID. In both scenarios, please make sure that the same storage account is configured as export and import configurations for Azure API for FHIR and Azure Health Data Services FHIR server. 
5. Configure [$export](https://learn.microsoft.com/azure/healthcare-apis/azure-api-for-fhir/configure-export-data) on the origin Azure API for FHIR server. Make sure that the intermediate Azure storage account that you plan to use for this migration is selected as the $export storage account for FHIR instance. You can do this by configuring [$export](https://learn.microsoft.com/azure/healthcare-apis/azure-api-for-fhir/configure-export-data) on the source FHIR instance (Azure API for FHIR server). If you do not already have a storage account, create a new storage account and follow the steps for configuring [$export](https://learn.microsoft.com/azure/healthcare-apis/azure-api-for-fhir/configure-export-data) to select that storage account.
   
6. Configure [$import](https://learn.microsoft.com/azure/healthcare-apis/fhir/configure-import-data) on the destination FHIR instance (Azure Health Data Service FHIR service server) with the same storage account as the import location, and set import mode to incremental mode.


> [!IMPORTANT]  
> Please ensure that your $import is set to **incremental import mode** in order for the migration tool to work. If needed, you may switch back to initial import mode post-migration. Set incremental import mode following these [configuration settings](https://learn.microsoft.com/en-us/azure/healthcare-apis/fhir/configure-import-data#step-3b-set-import-configuration-for-incremental-import-mode) and [parameter value](https://learn.microsoft.com/en-us/azure/healthcare-apis/fhir/import-data#body). Learn more about incremental and initial import [here](https://learn.microsoft.com/en-us/azure/healthcare-apis/fhir/import-data).



## Extra prerequisites needed (advanced scenarios)
You may have certain advanced scenarios surrounding your migration that may require more configuration or steps. We have listed a few of these scenarios below with instructions. If you have other scenarios that are not listed here, please submit a Github issue and we can take a look for consideration!

### Private Link
- If you are using Azure Private Link, please follow separate instructions in this Github for [deploying the migration tool with Azure Private Link](/FHIR-data-migration-tool-docs/private-link-sample/ReadMe.md).

### Custom search parameters
- If you have custom search parameters that need to be migrated over, please note the following:
	- The FHIR Migration Tool will copy over custom search parameters from your Azure API for FHIR over to your Azure Health Data Services FHIR service at the very beginning of migration. 
	- Once migration has started, if you wish to add any more custom search parameters after that, you must add them directly to the Azure Health Data Service FHIR service post-migration.
	-  Once all your custom search parameters are in Azure Health Data Service FHIR service (regardless of if they were added by the migration tool or manually), you will need to run $reindex post-migration in order to index the custom search parameters and be able to use them in live production.
	- Learn more about custom search parameters:
	[How to do custom search in FHIR service](https://learn.microsoft.com/en-us/azure/healthcare-apis/fhir/how-to-do-custom-search) 
	and $reindex:
	[How to run a reindex job in FHIR service](https://learn.microsoft.com/en-us/azure/healthcare-apis/fhir/how-to-run-a-reindex).

### De-identified export
- If you need to systemmatically edit or transform your data during the migration process, we have an option to include a step in migration that calls the [Tools for Health Data Anonymization](https://learn.microsoft.com/en-us/azure/healthcare-apis/azure-api-for-fhir/de-identified-export), which is a tool that can help de-identify data, to do those transformations after exporting from Azure API for FHIR and before importing into Azure Health Data Services FHIR Service.
- An example of when this might be needed is if you have resources with decimal values that are more than 18 digits. The [FHIR spec](https://www.hl7.org/fhir/datatypes.html#decimal) is headed in the direction to limit the length of decimal values to 18 digits, and Azure Health Data Services only supports up to 18 digits of precision for decimal data type. Azure API for FHIR did not have this restriction on precision. If you have data of decimal data type that has more than 18 digits that you are trying to migrate, $import may reject those values, and you may say a 500 Internal Server Error. To avoid this, you may use the De-id option in the migration tool to edit the data by truncating down to 18 digits to meet the restriction.
- To use this optional step, you will need to configure [de-identified export](https://learn.microsoft.com/en-us/azure/healthcare-apis/azure-api-for-fhir/de-identified-export) in Azure API for FHIR prior to the migration, with the following steps:
   1. Set up your anonymization config file, which details how you would like to transform your data. Learn more about the config file [here](https://learn.microsoft.com/en-us/azure/healthcare-apis/azure-api-for-fhir/de-identified-export). This repo includes [DemoTruncate.json](/infra/Anonymization/DemoTruncate.json), which is an example anonymization config file that truncates Observation quantity values down to 18 digits. 
   

   2. This repo has a PowerShell script available to help create the container in the storage account which is configured with Azure API for FHIR for export, and will also put the specified file in the container. Following instructions detail how to use the PowerShell script. This must be done before starting the Migration Tool deployment. To run the PowerShell Script, you need to have the "Storage Blob data contributor" role on the storage account, as the script requires access to the storage account.
   3. You can run the Powershell script locally, or you can use [Open Azure Cloud Shell](https://shell.azure.com). You can also access this from [Azure Portal](https://portal.azure.com).\
	More details on how to setup Azure Cloud Shell [here](https://learn.microsoft.com/azure/cloud-shell/overview).

		- If using Azure Cloud Shell, select PowerShell for the environment 
		- Clone this repo
			```azurecli
			git clone https://github.com/Azure/apiforfhir-migration-tool.git
			```
		- Change working directory to the repo directory
			```azurecli-interactive
			cd $HOME/apiforfhir-migration-tool/infra
			```
	4. Sign into your Azure account
		``` PowerShell
		Connect-AzAccount -Subscription 'xxxx-xxxx-xxxx-xxxx-xxxxxx'
		```
		where 'xxxx-xxxx-xxxx-xxxx-xxxxxx' is your subscription ID.

	5. Browse to the scripts folder under this path (..\infra\Anonymization).

	6. Run the following PowerShell script. 
		```Powershell
		./anonymization.ps1 -storageaccount '<Storage Account Name>' -filepath 'Anonymization configuration file' 
		```
				
		|Parameter   | Description   |
		|---|---|
		| storageaccount | Export Storage Account Name.
		| filepath | File Path of anonymization configuration file.

		Example:
		``` PowerShell
		./anonymization.ps1  -storageaccount 'teststorageaccount' -filepath '/home/apiforfhir-migration-tool/infra/Anonymization/DemoTruncate.json'
		```
	7. Follow the deployment instructions in the section for ["Deploy Migration Tool"](/FHIR-data-migration-tool-docs/README.md#extra-prerequisites-needed-advanced-scenarios), making sure to turn on the migration tool option for [Export with de-identified data](/FHIR-data-migration-tool-docs/README.md#export-with-de-identified-data).



# 3. Deploy Migration Tool
## Deploy the migration tool


Deploy the infrastructure for migration tool. More details on configurations that can be set during deployment are in the next section, "Configurations to set up during deployment".

### Option A: Deploy the migration tool through Azure Portal
Deploy the migration tool through Azure Portal using this Deploy to Azure button

[![Deploy to Azure](https://aka.ms/deploytoazurebutton)](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2FAzure%2Fapiforfhir-migration-tool%2Fmain%2Finfra%2Fmain.json/createUIDefinitionUri/https%3A%2F%2Fraw.githubusercontent.com%2FAzure%2Fapiforfhir-migration-tool%2Fmain%2Finfra%2FuiDefForm.json)

Notes: <br>
- Choose your own unique "Prefix for FHIR Migration Tool resources" during deployment.
- Configurations can be set in the UI in Azure Portal after clicking the Deploy the Azure button. Learn more about the configurations in the next section, "Configurations to set up during deployment".


### Option B: Deploy the migration tool manually through an ARM template
Deploy the migration tool manually <br>
Please note that if you are using [Private Link](/FHIR-data-migration-tool-docs/private-link-sample/ReadMe.md), you will need to use this option to deploy the migration tool manually. 
<br />
<details>
<summary>Click to expand to see manual deployment instructions.</summary>

1. Clone this repo
	```azurecli
	git clone https://github.com/Azure/apiforfhir-migration-tool.git --depth 1
	```
2. Log in to Azure  
	Before you begin, ensure that you are logged in to your Azure account. If you are not already logged in, follow these steps:
	```
	az login
	```
3. Set the Azure Subscription  
	If you have multiple Azure subscriptions and need to specify which one to use for this deployment, use the az account set command:
	```
	az account set --subscription [Subscription Name or Subscription ID]
	```
	Replace [Subscription Name or Subscription ID] with the name or ID of the subscription you want to use for this deployment. You can find your subscription information by running az account list.

	**Note** : This step is particularly important if you have multiple subscriptions, as it ensures that the resources are deployed to the correct subscription.

4. If needed, create a resource group

	If you don't already have a resource group that you want to use, use the following command to create a resource group.  
	```
		az group create --name <resource_group_name> --location <location>
	```  
	Replace <*resource_group_name*> with your desired name and <*location*> with the Azure region where you want to create the resource group

5. Deploy the function  
	Now, you can initiate the deployment using the Azure CLI
	```
	az deployment group create --resource-group<resource-group-name> --template-file <path-to-template> --parameters <path-to-parameter>
	```
	- <*resource-group-name*>: Replace this with the name of the resource group you want to use.
	- <*path-to-template*>: Provide the path to the ARM/Bicep template file i.e. main.json under infra folder.
	- <*path-to-parameter*>: Specify the path to the parameters file i.e. armmain.parameters.json under infra folder.
	<br><br>
	**NOTE** : Please update the **ARMmain.parameters.json** file with the configurations that you need. Learn more about the configurations in the next section, "Configurations to set up during deployment" <br>

	|Parameter   | Description   | Example Value |
	|---|---|---|
	| prefix | Unique prefix for naming resources.| "mig"|
	| fhirServiceName | Name of the FHIR service instance.| "[workspace]/[fhir service]"
	|apiForFhirName| Name of the API for the FHIR service.| "[AzureApiForFhirName]" |
	|appName|Name of the Function App.|"MyMigrationApp"|
	|fhirid| Resource ID of the FHIR service instance.|/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.HealthcareApis/workspaces/{workspaceName}/fhirservices/{FHIRserviceName}|
	|apiForFhirid| Resource ID of the Azure API for FHIR.|/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.HealthcareApis/services/{AzureApiForFHIRserviceName}|
	|dashboardName|Name of the monitoring dashboard.|MigrationToolDashboard|
	|exportWithHistory| A boolean value indicating whether to Include historical data in export (true/false).|true|
	|exportWithDelete|A boolean value indicating whether to include deleted resources in export (true/false).|false|
	|isParallel| A boolean value indicating whether the export operation should be performed in parallel. |true|
	|exportDeidentified|A boolean value indicating whether to export deidentified data. |true|
	|configFile|Name of Configuration File (only needed if you are using [Export with de-identified data](/FHIR-data-migration-tool-docs/README.md#export-with-de-identified-data)) |"DemoTruncate.json"|
	|sourceFhirServiceName|Name of the Source FHIR service instance.|"[workspace]/[Source fhir service]"|
	|sourcefhirid|Resource ID of the Source FHIR service instance.|/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.HealthcareApis/workspaces/{workspaceName}/fhirservices/{SourceFHIRserviceName}|


	**NOTE** :
	- If you want to export de-identified data, set the exportDeidentified parameter to true, and ensure isParallel is also set to true.<br>
	- If exportDeidentified is set to true, you must provide the configFile parameter with a valid configuration file name; otherwise, the export operation will fail.
	- Choose your own unique "Prefix for FHIR Migration Tool resources" during deployment.


</details>
<br />
	
## Configurations to set up during deployment
The following are optional settings that you can configure during deployment.

### Type of Migration.
The data migration tool gives the option to choose the type of migration:

- Azure API for FHIR to Azure Health Data Service
	- Source FHIR instance will be Azure API for FHIR and destination will be Azure Health Data Service 
- Azure Health Data Service to Azure Health Data Service
	- Source FHIR instance will be Azure Health Data Service and destination will be Azure Health Data Service 
 
![Source Instance](images/source-instance.png)

### Export with History and Soft Delete

Exporting with [history](https://learn.microsoft.com/en-us/azure/healthcare-apis/azure-api-for-fhir/purge-history) allows you to export current state of a resource as well as its previous versions. Exporting with [soft deletes](https://learn.microsoft.com/en-us/azure/healthcare-apis/azure-api-for-fhir/fhir-rest-api-capabilities#delete-hard--soft-delete) allows you to export soft deleted historic versions.

During the deployment of the migration tool, you will have the option to enable or disable the exporting of history and soft deletes by specifying their values as true or false. This is the equivalent of setting the $export query parameter "includeAssociatedData" with _history and _deleted, as mentioned [here](https://learn.microsoft.com/en-us/azure/healthcare-apis/azure-api-for-fhir/export-data#query-parameters).

Take a look at the screenshot below to learn how to configure the export settings. By default, exporting with history and deletion is set to true. If you prefer to export without history and deletion, you can change the value to false.

![Export](images/Export-with-history-and-delete.png)

Upon the completion of deployment, you can still make adjustments to the export settings for history and deletion by modifying the values within the Azure function's environment variable. 

Example:

```
Name: AZURE_ExportWithHistory
Value: True

Name: AZURE_ExportWithDelete
Value: False

```
### Export with isParallel

Exporting with isParallel allows you to export the resources in threads which make the export operation faster. <br>
__Note__: The isparallel parameter with export may consume more RU's in Azure API for FHIR compared to without isparallel, which may lead to higher costs for Azure API for FHIR.

During the deployment of the migration tool, you will have the option to enable or disable the exporting with isParallel by specifying their values as true or false. This is the equivalent of setting the $export query parameter "_isparallel", as mentioned [here](https://learn.microsoft.com/en-us/azure/healthcare-apis/azure-api-for-fhir/export-data#query-parameters).

Take a look at the screenshot below to learn how to configure the export settings. By default, exporting with isParallel is set to true. If you prefer to export without isParallel, you can change the value to false.

![Export](images/Export-with-isParallel.png)

Upon the completion of deployment, you can still make adjustments to the export settings for isParallel by modifying the values within the Azure function's environment variable. 

Example:

```
Name: AZURE_IsParallel
Value: True
```

If you set the isparallel parameter value to false for export, then the export will be based on the FHIR resource type rather than at the system level of the Azure API for FHIR.

**Note** : Migration tool does not support the export of custom resources.

### Export with de-identified data

Export can be done with de-identified data. This is helpful when you may need to systematically change or transform your data during the process of migration (for example, if you need to truncate some data to the 18th digit). <br>
__Note__: The de-identification of data during export is only supported when isparallel is set to true as Azure API for FHIR only supports de-identified export at the system level ($export).

Please note that if you need to use this option, you will need to follow the steps listed in the [De-identified export](/FHIR-data-migration-tool-docs/README.md#de-identified-export) section above to set up the configuration file and storage account container.  During the deployment of the migration tool, you will have the option to enable or disable exporting with de-identification by specifying their values as true or false. Setting this parameter as true is the equivalent of setting the $export query parameter "_anonymizationConfig_" as mentioned [here](https://learn.microsoft.com/en-us/azure/healthcare-apis/azure-api-for-fhir/de-identified-export). If you select true, you will also need to enter the configuration file name.

Take a look at the screenshot below to learn how to configure the export settings. By default, exporting with de-identification is set to false.

![De-Identified](images/Export-with-history-and-delete-deidentified.png)


### Stop Data Migration Tool During Business Hours

The migration tool allows you to specify a time frame during which no new export operations will start. By setting a time frame, you can ensure that new exports do not begin during business hours, while still allowing ongoing exports to complete and new exports to start after the specified time frame ends.

During the deployment of the migration tool, you will have the option to enable or disable "Stop Migration Tool During Business Hours" by specifying its value as true or false. If you select true, you will also need to enter the start time and end time for the restricted period  in UTC. By default, the start time is set to 8 and the end time is set to 17.

Take a look at the screenshot below to learn how to configure these settings. By default, the value of "Stop Migration Tool During Business Hours" is set to false. If you prefer to stop the migration tool during business hours, you can change the value to true and specify the start time and end time in a 24-hour format format in UTC.<br>

![StopDM](images/Stop-Data-MigrationTool.png)

Upon the completion of deployment, you can still make adjustments to the export settings to stop the migration tool by modifying the values within the Azure function's environment variables.

Example:
```
Name: AZURE_StopDm
Value: True
```
You can also specify the start and end times for the restricted period. For example, if you do not want new exports to start between 09:00 AM and 06:00 PM, modify the following values within the Azure function's environment variables

Example:
```
Name: AZURE_StartTime
Value: 9

Name: AZURE_EndTime
Value: 18
``` 
## Export in the migration tool

The [built-in API for FHIR $export operation](https://learn.microsoft.com/azure/healthcare-apis/azure-api-for-fhir/export-data) is leveraged in this migration tool for exporting the data from API for FHIR server. The $export PaaS endpoints are asynchronous, long-running HTTP APIs. 
The storage account is used for staging NDJSON files between the $export and $import. The storage account is also used by Azure Durable Functions to store state. 

In the migration tool we are using $export [query](https://learn.microsoft.com/azure/healthcare-apis/azure-api-for-fhir/export-data#query-parameters) which allows you to filter and export certain data accordingly from a source Azure API for FHIR server.

The migration tool hits the HTTP APIs endpoint for the $export operation, and the response contains export operation Content-Location URL. The content-location URL gives the status on the export operation. Each $export operation status is stored in the Azure storage table.

By default, the migration tool exports chunks of 100M resources in 30 days, which is configurable by using parameters: ExportChunkTime, ExportChunkDuration and ChunkLimit of data from API for FHIR in each export operation.

#### How to configure Chunk Duration, Time and Size for export
1. After deploying, open the Data migration Azure function.
2. Go to the environment variable setting and under it go to App Setting.
3. Set the below configuration as per the need:
```
Name: AZURE_ExportChunkDuration
Value: "Days" or "Hours" or "Minutes"

Name: AZURE_ExportChunkTime
Value: <<Int number>>

Name: AZURE_ChunkLimit
Value: <<Int number>>
```
AZURE_ExportChunkDuration can take Days, Hours or Minutes as the value.   
AZURE_ExportChunkTime will take integer as value.   
AZURE_ChunkLimit  will take integer as value to specify how many resources will be exported in a single chunk.

Example:

Below setting in Azure Function will export 100M resources under 30 days of data in a single chunk: 
```
Name: AZURE_ExportChunkDuration
Value: "Days"

Name: AZURE_ExportChunkTime
Value: 30

Name: AZURE_ChunkLimit
Value: 100000000
```

By default, the migration tool exports have _maxCount per job to 10000, which is configurable by using parameters: MaxCount, MaxCountValue of data from API for FHIR in each export operation.

#### How to configure MaxCount and MaxCountValue for export
1. After deploying, open the Data migration Azure function.
2. Go to the environment variable setting and under it go to App Setting.
3. Set the below configuration as per the need:
```
Name: AZURE_MaxCount
Value: true or false

Name: AZURE_MaxCountValue
Value: <<Int number>>

```
AZURE_MaxCount can be set to true or false. This parameter allows you to modify the migration tool to use the specified _maxCount value.
- If set to false, the export job will default to a value of 10,000 for _maxCount.
- If set to true, it enables you to change the _maxCount value to address issues encountered during the export job.

AZURE_MaxCountValue will take integer as value.   

Example:

Below setting in Azure Function for export allowing the _maxCount parameter in export query with value 5000 in a single chunk: 
```
Name: AZURE_MaxCount
Value: true

Name: AZURE_MaxCountValue
Value: 5000

```

You  can configure the start date in Azure function from where the export should start from the API for FHIR server. AZURE_StartDate will help to export the data from that specific date. <br>
If the start date is not provided the tool will fetch the first resource date from the server and start the migration.


Once the $export operation is completed, the export operation content location is stored in Azure storage table and the next export status orchestrator in the durable function picks the details from the storage table and checks the status of the export.

The migration tool is also storing the _since and _till date for the export operation in Azure storage table. Once the export operation is completed, then the import operation orchestrator starts in the migration tool application.

The next export will not start until the previous import is completed on FHIR service.

## Import in the migration tool

The [built-in Azure Health Data Service FHIR service $import operation](https://learn.microsoft.com/azure/healthcare-apis/fhir/import-data) is leveraged in the  migration tool for importing the data to the destination Azure Health Data Services FHIR server. The $import PaaS endpoints are asynchronous, long-running HTTP APIs. 
The storage account is used for getting the NDJSON files between the $export and $import. The storage account is also used by Azure Durable Functions to store state and the payload created for $import operation.

In the migration tool we are using $import for importing the data which got exported from the $export orchestrator.

The migration tool hits the HTTP APIs endpoint for the $import operation, the response contains import operation Content-Location URL. The content-location URL gives the status on import operation. Each $import operation status is stored in Azure storage table.

You can run multiple import jobs at the same time, but running multiple jobs might affect the overall throughput of the import operation. The FHIR server can handle up to five parallel import jobs. If you exceed this limit, the FHIR server might throttle or reject your requests. The data migration tool handle five import job at a time and once all the jobs are completed, then only it will start the remaining jobs for $import operation.

Once the $import operation is completed, the import operation content location is stored in Azure storage table and the next import status orchestrator in the durable function picks the details from storage table and checks the status of the import.

# 4. During Migration
## Monitoring during migration
### Dashboard Monitoring

During the deployment of the data migration tool , the dashboard is also deployed for monitoring the data migration from Azure API for FHIR to Azure Health Data Service FHIR service. It's the visualization of each export-import run.

![Architecture](images/Dasboard.png)

Dashboard contains the below details.

1. Export Operation Status.
	- Completed export counts - This gives the count of export executed on Azure API for FHIR.
	- Completed export details - This gives the details of each export executed on Azure API for FHIR.
	- Running export count - This gives the current export run count on Azure API for FHIR.
	- Running export details - This gives the details of current export run count on Azure API for FHIR.
	- Failed export count - This gives the failed export run count on Azure API for FHIR.
	- Failed export details - This gives the details of failed export run count on Azure API for FHIR.
	- Export resources details - This gives the details of export resources run count on Azure API for FHIR.
2. Import Operation Status.
	- Completed Import counts - This gives the count of import executed on FHIR service.
	- Completed Import details - This gives the details of each import executed on FHIR service.
	- Running import count - This gives the current import run count on FHIR service
	- Running import details - This gives the details of current import run count on FHIR service.
	- Failed import count - This gives the failed import run count on FHIR service.
	- Failed import details - This gives the details of failed import run count FHIR service.
	- Import resources details - This gives the details of import resources run count on FHIR service.
3. Surface Check - This give the details of surface check run for data movement verification.
4. Function App
	- Failures
	- Server exceptions and dependency failures
	- Avg processor / CPU utilization
	- Average available memory.
5. Total Resource Count - This gives the count of total resources on FHIR service and Azure API for FHIR.

	![Total Resource Count](images/TotalResourceCount.png)
### Table Storage Monitoring

During the deployment of data migration tool , the table storage (chunk and export table) linked to the Function App is used to store and monitoring the data migration from Azure API for FHIR to Azure Health Data Service FHIR service. This table gives the overview and details of each export-import runs.

There are two table storages created during deployment.

1. Chunk table storage: 
	- This shows how many runs have been done or started for the migration.
	- It stores the datetime value in since column. It indicates from which time the data should be exported from next run. This value is since in export URL for next export-import run.

	![ChunkTable](images/Chunk_Table.png)

2. Export table storage:
	- This contains details for each export-import run.
	- It captures the time taken for each export and import.
	- It captures the status of export and import.
	- The export-import content location is capture which can be used to get the extact error occured during export-import by fetching the details through URL.

	![ExportTable](images/Export_Table.png)



## Troubleshooting

1. Azure API for FHIR.
	-  Please see the [troubleshooting section](https://learn.microsoft.com/azure/healthcare-apis/fhir/export-data#troubleshoot) to handle issues on exporting the data.
2. Azure Health Data Services FHIR service.
	-  Please see the [troubleshooting section](https://learn.microsoft.com/azure/healthcare-apis/fhir/import-data#troubleshooting) to handle issues on importing the data.
3. Deployment Issue
	- Below issue might encountered during the infrastructure deployment of the data migration tool 

	1. Deployment failed due to "The resource write operation failed to complete successfully, because it reached terminal provisioning state 'Failed'".

		Steps to follow:
		1. Click on redeploy.

		![Deploy-Reached-Failure](images/Deploy-Reached-Terminal-State.png)

		2. The deployment details will already be filled, but ensure you select the same Resource Group that was previously deployed.
		![Redploy-Template](images/Redeploy-Template.jpg)
		3. Click Review + Create

		4. Wait for the deployment to complete.

	2. Deployment was successful but the function are not visible in the Azure Function.

		Below are the steps to follow in this case:
		1.  Run the Azure CLI command to check if any function exists:
			```
			az functionapp function list -g <Resource Group Name> -n <Function Name>
			```

			- If the list is available it means function are deployed but in Portal it is not visible. In that case restart the azure function :

				```
				az functionapp restart -g <ResourceGroup> -n <FunctionAppName>
				```

			- If the output is empty, like [], it means the function is not available. <br>			
			You can redeploy the migration tool deployment in the same resource group (no need to redeploy a new data migration tool, just redeploy the existing migration tool deployment): 

		2. If function still not visible, Click on Redeploy in the Azure portal.
 
			![Redeploy](images/Redeploy.jpg)
			
		3. The deployment details will already be filled, but ensure you select the same Resource Group that was previously deployed.

			![Redploy-Template](images/Redeploy-Template.jpg)
 
		4. Click Review + Create.
 
		5. Wait for the deployment to complete. Afterward, check if the functions are available again by running the following Azure CLI command:

			```
			az functionapp function list -g <Resource Group Name> -n <Function Name>
			```

		If still the functions are not visible ,here are the steps to redeploy the commit:
		
		1. Open the Azure Function, and on the left-hand side, navigate to the Deployment Center.
		![Deployment=Center](images/Deployment-Center.png)

		2. Click on Logs.
		
		3. Verify whether the deployment was successful, failed or stuck by reviewing the deployment logs.

		![Deployment=Center](images/Deployment-Center-Logs.png)

		4. If the function is not visible or if there was a failure during deployment, you can redeploy the commit by clicking on it and waiting for the deployment to complete.
		
		![Deployment=Center](images/Deployment-Center-Redeploy-Commit.png)

		5. Go to the Overview, restart the azure function, and check if the function is now visible.


4. To troubleshoot the error or failure of export-import. 
	- Please check the export table storage created during deployment process linked to Azure function app.
		- It contain the details for each export-import error status.
		- If the export failed due to : "The FHIR Server ran out of memory while processing an export job. Please use the _maxCount parameter when requesting an export job to reduce the number of resources exported at one time. The count used in this job was 10000"<br>
			Resolution:
			- Reduce the _maxCount per export job by following the steps outlined in the Export section of the migration tool documentation.
			- Refer to the "How to configure MaxCount and MaxCountValue for export" section for detailed instructions on adjusting these parameters.<br>
	By reducing the _maxCount, you can prevent memory-related issues during the export process.
	- Please check the details of export-import failure on dashboard as well
		- Export failure details can be found in Failed Export details 
		- Import failure details can be found in Failed Import details 
    

# 5. Stopping migration

## Data Movement Verification

You can check how much data has successfully copied over using the below checks.

1. Surface Check <br>
    For a quick validation, you can use the surface check. It compares the number  of resources of a particular FHIR resource type between the API for FHIR and FHIR service. 

2. Deep Check <br>
    For a deeper look, you can use the deep check to compare the JSON data of a subset of data from API for FHIR server and Azure Health Data Services FHIR service. You can configure the number of resources that will be compared in the parameter: DeepCheckCount.

	To configure DeepCheckCount parameter, follow below steps:

	1. Open the Data migration Azure function.
	2. Go the the environment variable. Under App setting set the below configuration:

	```
	Name: AZURE_DeepCheckCount
	Value: <<Resource Count>>
	```
	The number of complete fhir resources that need to be compared on both the server.

	3. Save the setting and hit the E2ETest_Http function.
	
	 Follow these steps to perform surface check and deep check.

	### How to run surface and deep check

	1. Once the data migration from Azure API for FHIR to Azure Health Data Service is complete. Run the below steps to verify the data movement.

		1. Go to your Data migration Azure function and open E2ETest_Http function.
		![Architecture](images/E2E-S&D-1.png)
		2. Get the E2ETest_Http function URL.
		![Architecture](images/E2E-S&D-2.png)
		3. Hit the E2ETest_Http function URL directly on browser or from Postman.
		4. There will be response containing statusQueryGetUri. Copy the uri. This URI shows the status of the function which has been hit.
		5. Hit the URL to check the status of surface and deep check.
		6. Once the statusQueryGetUri response runtimeStatus is complete. There will be output for surface and deep check which will contain the resources checks for both the server.
		

## Stopping the Migration Tool

Towards the end of migration, after a majority of the data has been copied, we recommend stopping all writes to Azure API for FHIR, and then waiting for the final $export/$imports to complete and for all the data to be migrated over to the new AHDS FHIR server prior to cutting over and starting to write to the new FHIR server. However, if you wish to start writing to the new AHDS FHIR server before the migration finishes, you can search, read, and POST new resources, but you should not update any resources. 


You can stop the migration tool once the data migration from Azure API for FHIR instance to Azure Health Data Service FHIR service is completed.

You can verify the progress of data migration using the steps in [Data Movement Verification](/FHIR-data-migration-tool-docs/README.md#data-movement-verification) section above.

Please follow below steps to stop the migration tool.

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

# 6. Post-migration
Some notes to remember post-migration:
1. Conduct any testing that you would like to use to check your new Azure Health Data Services FHIR service. 
2. As mentioned [above](/FHIR-data-migration-tool-docs/README.md#custom-search-parameters), if you have custom search parameters that were migrated with the migration tool or that you added yourself, run a [$reindex](https://learn.microsoft.com/en-us/azure/healthcare-apis/fhir/how-to-run-a-reindex). 
3. Cut over to your new Azure Health Data Services FHIR service, and start using your new FHIR server
	- Start pointing your pipelines to your new FHIR server's URL
	- Turn off any remaining pipelines that are running on Azure API for FHIR, delete data from the intermediate storage account that was used in the migration tool if necessary, delete data from your Azure API for FHIR server, and decommission your Azure API for FHIR account.
