﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using Azure;
using Azure.Data.Tables;
using FhirMigrationTool.Configuration;
using FhirMigrationTool.ExceptionHelper;
using FhirMigrationTool.Models;
using FhirMigrationTool.Processors;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace FhirMigrationTool
{
    public class ImportOrchestrator
    {
        private readonly IFhirProcessor _importProcessor;
        private readonly MigrationOptions _options;
        private readonly IAzureTableClientFactory _azureTableClientFactory;
        private readonly IMetadataStore _azureTableMetadataStore;

        public ImportOrchestrator(IFhirProcessor importProcessor, MigrationOptions options, IAzureTableClientFactory azureTableClientFactory, IMetadataStore azureTableMetadataStore)
        {
            _importProcessor = importProcessor;
            _options = options;
            _azureTableClientFactory = azureTableClientFactory;
            _azureTableMetadataStore = azureTableMetadataStore;
        }

        [Function(nameof(ImportOrchestration))]
        public async Task<string> ImportOrchestration(
            [OrchestrationTrigger] TaskOrchestrationContext context, string requestContent)
        {
            ILogger logger = context.CreateReplaySafeLogger(nameof(ImportOrchestration));
            logger.LogInformation("Starting import activities.");
            var statusRespose = new HttpResponseMessage();
            var statusUrl = string.Empty;
            TableClient exportTableClient = _azureTableClientFactory.Create(_options.ExportTableName);

            try
            {
                Pageable<TableEntity> jobListimport = exportTableClient.Query<TableEntity>(filter: ent => ent.GetBoolean("IsExportComplete") == true && ent.GetString("ImportRequest") != string.Empty && ent.GetString("IsImportRunning") == "Not Started");
                if (jobListimport.Count() > 0)
                {
                    foreach (TableEntity item in jobListimport)
                    {
                        ResponseModel importResponse = await context.CallActivityAsync<ResponseModel>(nameof(ProcessImport), item.GetString("ImportRequest"));
                        if (importResponse.Status == ResponseStatus.Completed)
                        {
                            logger?.LogInformation($"Import  returned: Success.");
                            statusUrl = importResponse.Content;
                            TableEntity exportEntity = _azureTableMetadataStore.GetEntity(exportTableClient, _options.PartitionKey, item.RowKey);
                            exportEntity["IsImportComplete"] = false;
                            exportEntity["IsImportRunning"] = "Started";
                            exportEntity["importContentLocation"] = importResponse.Content;
                            _azureTableMetadataStore.UpdateEntity(exportTableClient, exportEntity);
                        }
                        else
                        {
                            logger?.LogInformation($"Import Status check returned: Unsuccessful.");
                            TableEntity exportEntity = _azureTableMetadataStore.GetEntity(exportTableClient, _options.PartitionKey, item.RowKey);
                            exportEntity["IsImportComplete"] = false;
                            exportEntity["IsImportRunning"] = "Failed";
                            _azureTableMetadataStore.UpdateEntity(exportTableClient, exportEntity);
                            throw new HttpFailureException($"Response: {importResponse.Content} ");
                        }
                    }
                }
            }
            catch
            {
                throw;
            }

            return "completed";
        }

        [Function(nameof(ProcessImport))]
        public async Task<ResponseModel> ProcessImport([ActivityTrigger] string requestContent, FunctionContext executionContext)
        {
            try
            {
                HttpMethod method = HttpMethod.Post;
                ResponseModel importResponse = await _importProcessor.CallProcess(method, requestContent, _options.DestinationUri, "/$import",  _options.DestinationHttpClient);
                return importResponse;
            }
            catch
            {
                throw;
            }
        }
    }
}
