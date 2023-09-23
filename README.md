# Azure API for FHIR to Azure Health Data Services FHIR Service Migration Tool

## Introduction

This repo is a set of OSS tools that helps you migrate your data from Azure API for FHIR to the new evolved version, Azure Health Data Services FHIR Service. Please find more information here:

-   Learn more about Azure Health Data Services capabilities [here](https://azure.microsoft.com/en-us/products/health-data-services/?ef_id=_k_d0ffa03c8f79199459fec443f0510019_k_&OCID=AIDcmm5edswduu_SEM__k_d0ffa03c8f79199459fec443f0510019_k_&msclkid=d0ffa03c8f79199459fec443f0510019).
-   Learn more about the overall recommended migration approach here <mark>(link to above Azure doc)</mark> prior to starting migration.

## Migration Patterns
We recommend the following migration patterns. Depending on your organization’s tolerance for downtime, you may choose to use certain migration patterns and tools to help facilitate your migration.

| Migration Pattern | Details                                                                                                                                                                                          | How?                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                        |
|-------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Lift and Shift    | The simplest pattern. Ideal if your data pipelines can afford large downtime.                                                                                                                    |Choose the option that works best for your organization: <br> • Configure a workflow to [\$export](https://learn.microsoft.com/azure/healthcare-apis/azure-api-for-fhir/export-data) your data on Azure API for FHIR, then [\$import](https://learn.microsoft.com/azure/healthcare-apis/fhir/configure-import-data) into Azure Health Data Services FHIR Service. You can create your own tool to migrate the data using \$export and \$import.<br> •  The Github repo has some tips on running these commands, as well as a script to help automate creating the \$import payload [here](https://github.com/Azure/apiforfhir-migration-tool/blob/main/v0/V0_README.md).  |
| Incremental copy  | CContinuous version of lift and shift, with less downtime. Ideal for large amounts of data that take longer to copy, or if you want to continue running Azure API for FHIR during the migration.  | Choose the option that works best for your organization: <br> • You can create your own tool to migrate the data in an incremental fashion. <br> • We have created an [OSS migration tool](https://github.com/Azure/apiforfhir-migration-tool) that can help with this migration pattern.                                                                            

This repo provides resources for each of these migration patterns. 

##  Lift and Shift
### How to use
1. Review overall migration guidance <mark>(link to above Azure doc)</mark>.
2. Review configurations that you may want to set up in your new Azure Health Data Services FHIR Server [here](/incremental-copy-docs/Appendix.md#configurations-to-set-in-your-new-azure-health-data-services-fhir-server).
3. Configure a workflow to [\$export](https://learn.microsoft.com/en-us/azure/healthcare-apis/azure-api-for-fhir/export-data) your data on Azure API for FHIR, then [\$import](https://learn.microsoft.com/en-us/azure/healthcare-apis/fhir/configure-import-data) into Azure Health Data Services FHIR Service. The Github repo has some tips on running these commands, as well as a script to help automate creating the \$import payload in the "Lift and Shift" folder [here](/lift-and-shift-resources/Liftandshiftresources_README.md).  Or, you can create your own tool to migrate the data using \$export and \$import.

## Incremental Copy
### Incremental Copy Migration Tool
The OSS migration tool is an Azure Durable Function-based tool layered on top of existing FHIR server \$export and \$import functionality to orchestrate one-way migration of FHIR data. It continuously migrates new data to give you time to test your new FHIR server with your data, and flexibility to align your cutover with your organization’s existing maintenance windows.


### Migration tool capabilities

-   Customer-managed tool: Deploy and execute the migration tool in your own environment
-   Automates using existing FHIR server capabilities \$export and \$import
-   Does incremental copies: continuously migrates new data until you stop the tool, giving time to to test your new Azure Health Data Services FHIR server, and flexibility to cutover when you want
-   Migration tool copies data from your source Azure API for FHIR to an intermediate storage account, then imports that data into a new Azure Health Data Services FHIR server, giving you control over when to delete data from your original FHIR server.
-   Monitor status using Azure Monitor

### Migration tool limitations

Please take note of the following limitations of the migration tool before choosing to utilize the tool:

-   Since you are copying data to a new Azure Health Data Service FHIR server, you will have a new FHIR server URL and will need to manually point your applications to a new FHIR URL.
-   The migration tool only copies FHIR resources. It will not copy over your FHIR server configurations, you will need to manually set those configurations again in your new FHIR server. See here <mark>(insert link to below section  
    Configurations to set in your new Azure Health Data Services FHIR Server)</mark> for more information on setting configurations.
-   Our current \$export and \$import do not support historical versions and soft deletes. This is in the roadmap.
-   After the initial migration, migration tool only incrementally copies new and edited FHIR resources. If a FHIR resource is hard-deleted from the source Azure API for FHIR server during the migration (after the initial migration), you will need to manually keep track of deletes that happen during the migration, and manually delete them from your new server post-migration.

### Key Concepts

<mark>TODO technical overview and architecture diagram</mark>

### How to use
1. Review overall migration guidance <mark>(link to above Azure doc)</mark>.
2. Review configurations that you may want to set up in your new Azure Health Data Services FHIR Server [here](/incremental-copy-docs/Appendix.md#configurations-to-set-in-your-new-azure-health-data-services-fhir-server).
3. The incremental copy migration tool user guide is available [here](/incremental-copy-docs/README.md).

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

