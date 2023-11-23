# Incremental Copy migration tool demo
This sample will guide you through a demo of the incremental copy migration tool. We will first deploy test Azure API for FHIR and Azure Health Data Services FHIR server, and then import some sample data to be used in the demo.



# Prerequisites needed
1. An Azure account
    - You must have an active Azure account. If you don't have one, you can sign up [here](https://azure.microsoft.com/en-us/free/).
2. Installed and configured [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/what-is-azure-cli).
    - You can download it from [here](https://aka.ms//installazurecli).
3. Postman Application.
    - If you haven't already, download and install Postman from [here](https://www.postman.com/downloads/).
4. Privilege to assign roles (User Access Adminstrator in Azure Portal).

## What will be deployed in this template
* Azure API for FHIR server (origin FHIR server)
* Azure Health Data Services workspace and FHIR server (destination FHIR server) 
* Intermediate storage account that will be used for the migration tool
* Sample data to test with (using the Postman collection at the end of this tutorial)

## Resource Deployment using ARM/Bicep Template with Azure CLI
These steps guide you through deploying Azure resources using an ARM/Bicep Template via the Azure Command-Line Interface (CLI).

**1. Set parameters in the parameters file**
* Prior to initiating the resource deployment, it is essential to make modifications to the parameter file [ARTMTemplate.parameters.json](/infra/Demo/ARMTemplate.parameters.json).This file contains values that are specific to your deployment, including:
  * apiForFhirName: Name for the API for FHIR server that will be created (origin server)
  * workspaceName: Name for the Azure Health Data Services Workspace that will be created
  * fhirServiceName: Name for the Azure Health Data Services FHIR service that will be created (destination server)
  * storageAccountName: Name for the storage account that will act as the intermediate between the origin and destination

**2. Log in to Azure**
- Before you begin, ensure that you are logged in to your Azure account. If you are not already logged in, follow these steps:
    ```
     az login
    ```
**3. Set the Azure Subscription**
- If you have multiple Azure subscriptions and need to specify which one to use for this deployment, use the az account set command:
    ```
    az account set --subscription "<Subscription Name or Subscription ID>"
    ```
- Replace <*Subscription Name or Subscription ID*> with the name or ID of the subscription you want to use for this deployment. You can find your subscription information by running az account list.

- **Note** : This step is particularly important if you have multiple subscriptions, as it ensures that the resources are deployed to the correct subscription.

**4.Create the Resource Group**

- Use the following command to create a resource group, if you don't already have one that you want to use.
    ```
    az group create --name <resource_group_name> --location <location>
    ```
  - Replace <*resource_group_name*> with your desired name  and <*location*> with the Azure region where you want to create the resource group

**5. Deploy the Resources** 
- Now, you can initiate the deployment using the Azure CLI
    ```
    az deployment group create --resource-group<resource-group-name> --template-file <path-to-template> --parameters <path-to-parameter>
    ```
    - <*resource-group-name*>: Replace this with the name of the resource group you want to use.
    - <*path-to-template*>: Provide the path to your ARM/Bicep template file (ARMTemplate.json)
    - <*path-to-parameter*>: Specify the path to the parameters file (ARMTemplate.parameters.json)

**6. Monitor Deployment Progress**
- During deployment, the Azure CLI will provide real-time feedback, displaying status messages as it creates the resources. Monitor the progress until the deployment completes.

**7. Review Deployment Results**
- Once the deployment is finished, you will receive a confirmation message in the CLI.


Now, you have deployed a brand new Azure API for FHIR server, intermediate storage account, and a new Azure Health Data Services FHIR server. These azure resources can be used to test out the migration tool. 

**8. Add initial sample data**
* Import sample data into the Azure API for FHIR (origin server) using Postman. If you aren't  familiar with Postman, see Postman setup instructions in our [Postman starter sample](https://github.com/Azure-Samples/azure-health-data-and-ai-samples/blob/main/samples/sample-postman-queries/README.md#prerequisites).

A. Import the Collection into the postman.
- Open Postman. 
- Click on the "Import" button located in the top-left corner of the Postman window.
- In the file selection dialog, choose the ['FHIR-Demo.postman_collection.json' file](/infra/Demo/FHIR-Demo.postman_collection.json) for import.

B. Set Up Environment Variables
- Click on the "No environment" dropdown in the top-right corner of Postman and select "Manage Environments".
- Click "Add" to create a new environment. Give it a name (e.g., "MyServerEnvironment") and include the variable "FhirUrl." Set the value of this variable to the  FHIR metadata endpoint (excluding the "/metadata" at the end) of the origin FHIR server (Azure API for FHIR server created above, or existing Azure API for FHIR server that you already have).
- Select the environment you created from the dropdown menu.

 C. Set Up Authorization
- For detailed instructions on setting up OAuth 2.0 authorization, you can refer to the [Postman starter sample](https://github.com/Azure-Samples/azure-health-data-and-ai-samples/tree/main/samples/sample-postman-queries). You may need to change the Authorization to whichever authorization you are using (for example, the Postman sample uses BearerToken)

D. Import sample data into your origin API for FHIR
- Open the 'DataInjection' request from the FHIR-Demo Collection. If the contents of the 'Body.json' file are not already in the body tab, please paste them there. Then, select the POST method and proceed to click the send button to initiate the execution.

**9. Deploy the migration tool**
* Follow the [instructions](/incremental-copy-docs/README.md) in this repo to deploy the migration tool. It can now be used with your new FHIR servers to test and demo!

**10. Test incremental copy functionality**
* To test the incremental copy functionality, open the 'ReadUpdate' request from the FHIR-Demo Collection. If the contents of the 'Body.json' and the pre-Request script file are not already present in the body and pre-request script tab, respectively, paste them there. Afterward, select the POST method and proceed to click the send button to initiate the execution. 
* This request will update some of the previously posted resources that we posted in the initial sample data import (DataInject above). The migration tool runs automatically every 5 minutes. After the resources are updated, simulating an update in a real server, wait about 10 minutes to see that the migration tool will pick up the new updates and migrate them to the server. You can see this on the migration tool dashboard, and verify in Postman. 





## Troubleshooting
**1. Template Deployment Errors**
- Please see the [troubleshooting section](https://learn.microsoft.com/en-us/azure/azure-resource-manager/troubleshooting/quickstart-troubleshoot-arm-deployment?tabs=azure-cli) to handle the issues of Azure Resource Manager template (ARM template) JSON deployment errors.
-  Please see the [troubleshooting section](https://learn.microsoft.com/en-us/azure/azure-resource-manager/troubleshooting/quickstart-troubleshoot-bicep-deployment?tabs=azure-cli) to handle the issues of Bicep file deployment errors.

**2. Postman Connection Issues**
- **Issue:** Unable to connect to the deployed resource.
- **Solution:**
    -   Double-check the URL and ensure it's correct.
    - Verify that your local machine has internet access.
    - Check if any firewall or network policies are blocking the connection.

**3. Authorization and Authentication**
- **Issue:** Unauthorized or Forbidden error.
- **Solution:**
    - Ensure that you have the necessary permissions to access the deployed resource.
    - Check if any access policies or role assignments need to be updated.




