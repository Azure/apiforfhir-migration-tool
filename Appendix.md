On Azure Docs: (General migration guidance)  
Migration strategies for moving from Azure API for FHIR to Azure Health Data Services

On September 30th, 2026, Azure API for FHIR will be retired. For more information, see the official announcement *(insert link to official announcement)*. If you are using Azure API for FHIR, please migrate to Azure Health Data Services prior to that date.

This article explains the recommended migration approach for moving FHIR data from Azure API for FHIR to Azure Health Data Services (FHIR Service), as well as a few migration patterns and OSS tools that have been developed to help in the process.

Azure Health Data Services is the evolved version of Azure API for FHIR. It enables customers to manage FHIR, DICOM, and MedTech Services with common configuration and integration with other Azure Services. Azure Health Data Services follows consumption-based pricing model, only charging for storage, API calls, transformation and conversion used. Learn more about Azure Health Data Services capabilities here.

# Recommended Approach

At a high level, the recommended approach is:

-   Step 1: Assess Readiness
-   Step 2: Prepare to igrate
-   Step 3: Migrate data and application workloads
-   Step 4: Cutover from Azure API for FHIR to Azure Health Data Services

## 1) Assess Readiness

-   Learn about Azure Health Data Services [here.](https://learn.microsoft.com/en-us/azure/healthcare-apis/fhir/)
-   Compare the capabilities of Azure API for FHIR with Azure Health Data Services. *(insert link to below table TODO)*

| **Areas**               | **Azure API for FHIR**                                                                                                                                                 | **Azure Health Data Services**                                                                                                                                                              |
|-------------------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **Settings**            | *Supported :*  *Local RBAC*  *SMART on FHIR Proxy*                                                                                                                     | *Planned deprecation:*  *Local RBAC (9/6/23)* *SMART on FHIR Proxy (9/21/26)*                                                                                                               |
| **Data storage Volume** | *More than 4TB*                                                                                                                                                        | *Current support is 4TB.a need for more than 4TB.*                                                                                                                                          |
| **Data Ingress**        | *Tools available in OSS*                                                                                                                                               | *Import operation*                                                                                                                                                                          |
| **Autoscaling**         | *Supported on request and incurs charge*                                                                                                                               | *Enabled by default and no additional charge*                                                                                                                                               |
| **Search Parameters**   | *Bundle type supported: Batch*  *Include and revinclude, iterate modifier not supported*  *Sorting supported by first name, last name , birthdate and clinical date.*  | *Bundle Type supported: Batch and Transaction.*  *Selectable search parameters*  *Include and revinclude, iterate modifier is supported*  *Sorting supported by string and dateTime fields* |
| **Events**              | *Not Supported*                                                                                                                                                        | *Supported*                                                                                                                                                                                 |
| **Infrastructure**      | *Supported* *Customer Managed Keys* *AZ Support and PITR* *Cross Region DR*                                                                                            | *Supported : Data Recovery* *Upcoming*  *AZ support*  *Customer Managed Keys*                                                                                                               |

Only the capabilities that differ between the two products are called out above.

-   Review your architecture and assess if any changes need to be made.
    -   Things to consider that may affect your architecture:
        -   Sync Agent is being deprecated. If you were using Sync Agent to connect to Dataverse, please see [Overview of Data integration toolkit \| Microsoft Learn](https://learn.microsoft.com/en-us/dynamics365/industry/healthcare/data-integration-toolkit-overview?toc=%2Findustry%2Fhealthcare%2Ftoc.json&bc=%2Findustry%2Fbreadcrumb%2Ftoc.json)
        -   FHIR Proxy is being deprecated. If you were using FHIR Proxy for eventing in , please refer to the new [eventing](https://learn.microsoft.com/en-us/azure/healthcare-apis/events/events-overview) feature built in. Alternatives can also be customized and built using the new [Azure Health Data Services Toolkit](https://github.com/microsoft/azure-health-data-services-toolkit).
        -   SMART on FHIR proxy is being deprecated. You will need to use the new SMART on FHIR capability, more information here: <https://learn.microsoft.com/en-us/azure/healthcare-apis/fhir/smart-on-fhir>
        -   
        -   Azure Health Data Services FHIR Service does not support local RBAC and custom authority. The token issuer authority will have to be the authentication endpoint for the tenant that the FHIR Service is running in.
        -   IoT Connector migration:
            -   The IoT connector is only supported using an [Azure API for FHIR](https://docs.microsoft.com/azure/healthcare-apis/azure-api-for-fhir/overview) service. The IoT Connector is succeeded by the MedTech service. You will need to deploy a MedTech service and corresponding FHIR service within an existing or new Azure Health Data Services workspace and point your devices to the new Azure Events Hubs device event hub. You can utilize their existing IoT connector device and destination mapping files with the MedTech service deployment if you choose. If you want to migrate existing IoT connector device FHIR data from your Azure API for FHIR service to the new AHDS FHIR service, you can accomplish this using the bulk export and import functionality using the Migration tool \<Add URL for Migration Guidance and github repository\>. Another migration path would be to deploy a new MedTech service and replay the IoT device messages through the MedTech service.

## 2) Prepare to migrate

-   Create a migration plan.
    -   We recommend the following migration patterns. Depending on your organization’s tolerance for downtime, you may choose to use certain migration patterns and tools to help facilitate your migration.

| Migration Pattern | Details                                                                                                                                                                                          | How?                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                        |
|-------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Lift and Shift    | The simplest pattern. Ideal if your data pipelines can afford large downtime.                                                                                                                    | Choose the option that works best for your organization: Configure a workflow to [\$export](https://learn.microsoft.com/en-us/azure/healthcare-apis/azure-api-for-fhir/export-data) your data on Azure API for FHIR, then [\$import](https://learn.microsoft.com/en-us/azure/healthcare-apis/fhir/configure-import-data) into Azure Health Data Services FHIR Service. The Github repo has some tips on running these commands, as well as a script to help automate creating the \$import payload [here](https://github.com/Azure/apiforfhir-migration-tool/blob/main/v0/V0_README.md).  Or, you can create your own tool to migrate the data using \$export and \$import. |
| Incremental copy  | Continuous version of lift and shift, with less downtime. Ideal for large amounts of data that take longer to copy, or if you want to continue running Azure API for FHIR during the migration.  | Choose the option that works best for your organization: We have created an OSS migration tool that can help with this migration pattern (insert link to github) Or, you can create your own tool to migrate the data in an incremental fashion.                                                                                                                                                                                                                                                                                                                                                                                                                            |

-   If you choose to use the OSS migration tool, review and understand the migration tool’s capabilities and limitations (*insert link to Github below).*
-   Prepare Azure API for FHIR server
    -   Identify data to migrate.
        -   Take this opportunity to clean up data or FHIR servers that you no longer use.
        -   Decide if you want to migrate historical versions or not. See below “Deploy a new Azure Health Data Services FHIR Service server” for more information.
    -   If you’re planning to use the migration tool:
        -   See “Azure API for FHIR preparation” (link to github below) for additional things to note.
-   Deploy a new Azure Health Data Services FHIR Service server
    -   Deploy an Azure Health Data Services Workspace first
    -   Then deploy a Azure Health Data Services FHIR Service server ([Deploy a FHIR service within Azure Health Data Services \| Microsoft Learn)](https://learn.microsoft.com/en-us/azure/healthcare-apis/fhir/fhir-portal-quickstart)
        -   Configure your new Azure Health Data Services FHIR Service server. If you’d like to use the same configurations that you had in Azure API for FHIR for your new Azure Health Data Services FHIR Service server, see a recommended list of what to check for here (link to Github section [Migration Tool Documentation.docx](https://microsoft.sharepoint.com/:w:/t/msh/Eb7WohSv_6JNlG1xAI8TyvoBbAPfhxnPzr4wv9py1InEww?e=QeSbo3&nav=eyJoIjoiMTUwODIzNzMifQ)) Configure settings that are needed “pre-migration”.

## 3) Migrate data

-   Choose the migration pattern that works best for your organization. (link to above table for migration patterns)
    -   If you are using OSS migration tools, please follow instructions on Github (*Insert link to Github documentation)*

## 4) Migrate applications and reconfigure settings

-   Migrate applications that were pointing to the old FHIR server.
    -   Change the endpoints on your applications so that they point to the new FHIR server’s URL.
        -   (insert link on how to find the new server’s URL)
    -   Set up permissions again for these apps.
        -   <https://learn.microsoft.com/en-us/azure/storage/blobs/assign-azure-role-data-access>
-   Reconfigure any remaining settings in the new Azure Health Data Services FHIR Service server “post-migration” (Insert link below)
    -   If you’d like to doublecheck to make sure that the Azure Health Data Services FHIR Service and Azure API for FHIR servers have the same configurations, you can check both [metadata endpoints](https://learn.microsoft.com/en-us/azure/healthcare-apis/fhir/use-postman#get-capability-statement) to compare and contrast the two servers.
-   Set up any jobs that were previously running in your old Azure API for FHIR server (for example, \$export jobs)

### 

## 5) Cutover from Azure API for FHIR to Azure Health Data Services FHIR Service

After you’re confident that your Azure Health Data Services FHIR Service server is stable, you can begin using Azure Health Data Services FHIR Service to satisfy your business scenarios. Turn off any remaining pipelines that are running on Azure API for FHIR, delete data from the intermediate storage account that was used in the migration tool (if you used it), delete data from your Azure API for FHIR server, and decommission your Azure API for FHIR account.

## Appendix

## Azure API for FHIR and Azure Health Data Services capabilities

# FAQ

(to be added as first response to Q&A page also)

-   When will Azure API for FHIR be retired?

Azure API for FHIR will be retired on 30 September 2026.

-   Why are we retiring Azure API for FHIR?

Azure API for FHIR is a service that was purpose built for protected health information (PHI), meeting regional compliance requirements. In March 2022, we announced the general availability of [Azure Health Data Services](https://learn.microsoft.com/en-us/azure/healthcare-apis/healthcare-apis-overview), that enables quick deployment of managed, enterprise-grade FHIR, DICOM, and MedTech services for diverse health data integration. See below for detailed benefits of migrating to Azure Health Data Services FHIR service. With this new experience, we’re retiring Azure API for FHIR

-   What are the benefits of migrating to Azure Health Data Services FHIR service?

AHDS FHIR service offers a rich set of capabilities such as

-   Consumption-based pricing model where customers pay only for used storage & throughput
-   Support for transaction bundles
-   Chained search improvements
-   Improved ingress & egress of data with \$import, \$export including new features such as incremental import (preview)
-   Events to trigger new workflows when FHIR resources are created, updated or deleted
-   Connectors to Azure Synapse Analytics, Power BI and Azure Machine Learning for enhanced analytics
-   SMART on FHIR Proxy is planned for deprecation in Gen2, as we migrate from Gen1 what are the steps for enabling SMART on FHIR in Gen2?

SMART on FHIR proxy will be retiring, please transition to the SMART on FHIR (Enhanced) which uses Azure Health Data and AI OSS samples by **21 September 2026**. After 21 September 2026, applications relying on SMART on FHIR proxy will report errors in accessing the FHIR service.

SMART on FHIR (Enhanced) provides added capabilities than SMART on FHIR proxy and can be considered to meet requirements with SMART on FHIR Implementation Guide (v 1.0.0) and §170.315(g)(10) Standardized API for patient and population services criterion.

-   What will happen after the service is retired on 30 September 2026?

Customers will not be able to do the following:

-   Create or manage Azure API for FHIR accounts
-   Access the data through the Azure portal or APIs/SDKs/client tools
-   Receive service updates to Azure API for FHIR or APIs/SDKs/client tools
-   Access customer support (phone, email, web)
-   Where can customers go to learn more about migrating to Azure Health Data Services FHIR service?

You can start with \<Link to Azure Docs migration guidance \> to learn more about Azure API for FHIR to Azure Health Data Services FHIR service migration. Please be advised that the migration from Azure API for FHIR to Azure Health Data Services FHIR service involves data migration as well updating the applications to use Azure Health Data Services FHIR service. You can find more documentation on the step-by-step approach to migrating your data and applications in this migration tool \<Link to github repo\>.

-   Where can customers go for answers to questions?

Customers have multiple options to get answers to questions.

-   Get answers from community experts in Microsoft Q&A \<Link to Microsoft Q&A Page\><mailto:APIforFHIRtoAHDSFHIRMigrationQA@service.microsoft.com>
-   If you have a support plan and require technical support, please [contact us.](https://portal.azure.com/#blade/Microsoft_Azure_Support/HelpAndSupportBlade/newsupportrequest)
1.  Under Issue type, select Technical.
2.  Under Subscription, select your subscription.
3.  Under Service, click My services, then Azure API for FHIR
4.  Under Summary, type a description of your issue.
5.  Under Problem type, Troubleshoot configuration issue
6.  Under Problem subtype, my issue is not listed

TODO when to use CSS vs use github issues

On Github Docs (migration tool-specific):

# ReadMe

# Azure API for FHIR to Azure Health Data Services FHIR Service Migration Tool

## Introduction

The Azure API for FHIR to Azure Health Data Services FHIR Service Migration Tool is an OSS tool that helps you migrate your data from Azure API for FHIR to the new evolved version, Azure Health Data Services FHIR Service. Please find more information here:

-   Learn more about Azure Health Data Services capabilities [here](https://azure.microsoft.com/en-us/products/health-data-services/?ef_id=_k_d0ffa03c8f79199459fec443f0510019_k_&OCID=AIDcmm5edswduu_SEM__k_d0ffa03c8f79199459fec443f0510019_k_&msclkid=d0ffa03c8f79199459fec443f0510019).
-   Learn more about the overall recommended migration approach here (link to above Azure doc)

The migration tool is an Azure Durable Function-based tool layered on top of existing FHIR server \$export and \$import functionality to orchestrate one-way migration of FHIR data. It continuously migrates new data to give you time to test your new FHIR server with your data, and flexibility to align your cutover with your organization’s existing maintenance windows.

## Migration tool capabilities

-   Customer-managed tool: Deploy and execute the migration tool in your own environment
-   Automates using existing FHIR server capabilities \$export and \$import
-   Does incremental copies: continuously migrates new data until you stop the tool, giving time to to test your new Azure Health Data Services FHIR server, and flexibility to cutover when you want
-   Migration tool copies data from your source Azure API for FHIR to an intermediate storage account, then imports that data into a new Azure Health Data Services FHIR server, giving you control over when to delete data from your original FHIR server.
-   Monitor status using Azure Monitor

## Migration tool limitations

Please take note of the following limitations of the migration tool before choosing to utilize the tool:

-   Since you are copying data to a new Azure Health Data Service FHIR server, you will have a new FHIR server URL and will need to manually point your applications to a new FHIR URL.
-   The migration tool only copies FHIR resources. It will not copy over your FHIR server configurations, you will need to manually set those configurations again in your new FHIR server. See here (insert link to below section  
    Configurations to set in your new Azure Health Data Services FHIR Server) for more information on setting configurations.
-   Our current \$export and \$import do not support historical versions and soft deletes. This is in the roadmap.
-   After the initial migration, migration tool only incrementally copies new and edited FHIR resources. If a FHIR resource is hard-deleted from the source Azure API for FHIR server during the migration (after the initial migration), you will need to manually keep track of deletes that happen during the migration, and manually delete them from your new server post-migration.

## Key Concepts

TODO technical overview and architecture diagram

## How to use

## Contributing

This project welcomes contributions and suggestions. Most contributions require you to agree to a Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us the rights to use your contribution. For details, visit https://cla.opensource.microsoft.com.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the Microsoft Open Source Code of Conduct. For more information see the Code of Conduct FAQ or contact opencode@microsoft.com with any additional questions or comments.

## Disclaimers

The migration tool is an open-source project. It is not a managed service, and it is not part of Microsoft Azure Health Data Services. You bear sole responsibility for compliance with local law and for any data you use with this open-source toolkit. Please review the information and licensing terms on this GitHub website before using the migration tool.

The migration tool GitHub is intended only for use in migrating data. It is not intended for use as a medical device or to perform any analysis or any medical function and the performance of the software for such purposes has not been established. You bear sole responsibility for any use of this software, including incorporation into any product intended for a medical purpose.

## Trademarks

This project may contain trademarks or logos for projects, products, or services. Authorized use of Microsoft trademarks or logos is subject to and must follow Microsoft's Trademark & Brand Guidelines. Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship. Any use of third-party trademarks or logos are subject to those third-party's policies.

FHIR® is the registered trademark of HL7 and is used with the permission of HL7.

# Appendix

## Additional Azure API for FHIR Prep to do specific to Migration Tool

If you are planning to use the migration tool, please take note of these additional steps to take when preparing your Azure API for FHIR server for migration:

-   The tool consumes resources, so if you want it to run as fast as it can, cancel all non-business critical jobs (like reindexing or large imports) if you can.
-   If your current Azure API for FHIR server has autoscaling turned on, \$export on a large amount of data may boost your RU and increase your cost. If you’d like to change from autoscale to fixed, please file a support ticket to turn autoscaling off.
-   If you don’t already have a storage account set up for exports, choose a storage account to export to in Azure Portal under the “Export” tab.

### Eventing

-   The migration tool will put all the exports from Azure API for FHIR into one container called “Migration” in the storage account that is specified for export. If you currently are using eventing for your Azure API for FHIR service and have a filter on EventGrid that tracks your \$export storage account, be sure to exclude the “Migration” container so that it does not interfere with your existing eventing set up.

## Configurations to set in your new Azure Health Data Services FHIR Server

As you set up your new Azure Health Data Services FHIR Service server, you may want to use the same configurations that you had in Azure API for FHIR for your new Azure Health Data Services FHIR Service server. If so, here are some recommended configurations to note. Some configurations can be set up during the Azure Health Data Services FHIR Service deployment process ([Deploy a FHIR service within Azure Health Data Services \| Microsoft Learn)](https://learn.microsoft.com/en-us/azure/healthcare-apis/fhir/fhir-portal-quickstart) . Others can be set up post-migration in Azure Portal.

*In your Azure API for FHIR server on Azure Portal, go to the “Properties” tab. Here you will see a list of your curent Azure API for FHIR configurations.* The following table details when and where to set up the corresponding same configurations in Azure Health Data Services FHIR Service, if you’d like to set up the same configurations.

| Configuration                                            | When to configure                                                                                                          | Where to configure                                                                                                                                                                                                    | Notes                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                      |
|----------------------------------------------------------|----------------------------------------------------------------------------------------------------------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Location                                                 | Pre-migration                                                                                                              | During FHIR server deployment                                                                                                                                                                                         |                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                            |
| FHIR version                                             | Pre-migration                                                                                                              | During FHIR server deployment                                                                                                                                                                                         |                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                            |
| Private Link                                             | Pre-migration Please follow instructions here (insert link for Private Link instructions – TODO Xoriant) for Private Link. | Please follow instructions here (insert link for Private Link instructions – TODO Xoriant) for Private Link.                                                                                                          |                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                            |
| Authentication: Authority, Audience, Allowed Object ID’s | Pre-migration                                                                                                              | During FHIR server deployment                                                                                                                                                                                         |                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                            |
| RBAC and Custom Authority                                | Post-migration                                                                                                             | In Azure Portal under “Access control (IAM)” tab                                                                                                                                                                      |                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                            |
| SMART on FHIR                                            | Either pre- or post-migration                                                                                              | Pre: During FHIR server deployment Post: In Azure Portal  Please refer to [SMART on FHIR - Azure Health Data Services \| Microsoft Learn](https://learn.microsoft.com/en-us/azure/healthcare-apis/fhir/smart-on-fhir) | Note: SMART on FHIR proxy is being deprecated. You will need to use the new SMARt on FHIR capability. Please see [SMART on FHIR - Azure Health Data Services \| Microsoft Learn](https://learn.microsoft.com/en-us/azure/healthcare-apis/fhir/smart-on-fhir) for more information.                                                                                                                                                                                                                                                                         |
| Provisioned throughout (RU/s)                            | N/A, this is only applicable to Azure API for FHIR                                                                         |                                                                                                                                                                                                                       |                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                            |
| Customer Managed Key                                     | N/A, this is not available in Azure Health Data Services FHIR Service yet (in the roadmap)                                 |                                                                                                                                                                                                                       | Note: CMK is not available in Azure Health Data Services FHIR Service yet (in the roadmap)                                                                                                                                                                                                                                                                                                                                                                                                                                                                 |
| Tags                                                     | Either pre- or post-migration                                                                                              | Pre: During FHIR server deployment Post: In “Tags” tab                                                                                                                                                                |                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                            |
| Versioning Policy                                        | Pre-migration                                                                                                              | During FHIR server deployment                                                                                                                                                                                         | Note: The migration tool currently does not support migrating historical versions (in the roadmap)                                                                                                                                                                                                                                                                                                                                                                                                                                                         |
| CORS                                                     | Post-migration                                                                                                             | In Azure Portal under “CORS” tab                                                                                                                                                                                      |                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                            |
| Managed Identity                                         | Post-migration                                                                                                             | In Azure Portal under “Identity” tab                                                                                                                                                                                  |                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                            |
| Storage account for export                               | Post-migration                                                                                                             | In Azure Portal in the “Export” tab                                                                                                                                                                                   |                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                            |
| ACR Container                                            | Post-migration                                                                                                             | In Azure Portal under “Artifacts” tab                                                                                                                                                                                 |                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                            |
| Profiles                                                 | Post-migration                                                                                                             | Re-upload profiles into new FHIR server                                                                                                                                                                               |                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                            |
| Custom search parameters                                 | Post-migration (see note)                                                                                                  |                                                                                                                                                                                                                       | Note: Custom search parameters that are in the source FHIR server prior to the migration will be automatically brought over to Azure Health Data Services FHIR Service with the migration tool If you add any custom search parameters after the migration tool has started, it will not be brought over. You will need to query Azure API for FHIR for the new search parameters as a bundle, POST those to Azure Health Data Services FHIR Service, then run a reindex for them to work.                                                                 |
| Client Registration                                      | Post-migration                                                                                                             | In Azure Portal under “Access control (IAM)” tab                                                                                                                                                                      |                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                            |
| Eventing                                                 | Post-migration                                                                                                             | Please see Eventing overview here [What are Events? - Azure Health Data Services \| Microsoft Learn](https://learn.microsoft.com/en-us/azure/healthcare-apis/events/events-overview)                                  | During migration, eventing in Azure API for FHIR can continue. Please make sure to see our note on Eventing above (link to above eventing note) Make sure eventing in Azure Health Data Services FHIR Service server is turned off during migration. You can turn on eventing in Azure Health Data Services FHIR Service only after the cutover and migration has finished. Please refer here to learn more about [eventing](https://learn.microsoft.com/en-us/azure/healthcare-apis/events/events-overview) in Azure Health Data Services FHIR Service.   |
| Sync Agent for Dataverse                                 |                                                                                                                            |                                                                                                                                                                                                                       | Sync Agent is being deprecated. If you were using Sync Agent to connect to Dataverse, please see [Overview of Data integration toolkit \| Microsoft Learn](https://learn.microsoft.com/en-us/dynamics365/industry/healthcare/data-integration-toolkit-overview?toc=%2Findustry%2Fhealthcare%2Ftoc.json&bc=%2Findustry%2Fbreadcrumb%2Ftoc.json)                                                                                                                                                                                                             |

## 

# Concepts

TODO more in depth technical overview
