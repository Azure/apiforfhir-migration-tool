// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using ApiForFhirMigrationTool.Function.Configuration;
using ApiForFhirMigrationTool.Function.ExceptionHelper;
using ApiForFhirMigrationTool.Function.Models;
using ApiForFhirMigrationTool.Function.Processors;
using Azure;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace ApiForFhirMigrationTool.Function
{
    public class ImportStatusOrchestrator
    {
        private readonly IFhirProcessor _importProcessor;
        private readonly MigrationOptions _options;
        private readonly IAzureTableClientFactory _azureTableClientFactory;
        private readonly IMetadataStore _azureTableMetadataStore;

        public ImportStatusOrchestrator(IFhirProcessor importProcessor, MigrationOptions options, IAzureTableClientFactory azureTableClientFactory, IMetadataStore azureTableMetadataStore)
        {
            _importProcessor = importProcessor;
            _options = options;
            _azureTableClientFactory = azureTableClientFactory;
            _azureTableMetadataStore = azureTableMetadataStore;
        }

        [Function(nameof(ImportStatusOrchestration))]
        public async Task<string> ImportStatusOrchestration(
            [OrchestrationTrigger] TaskOrchestrationContext context, string requestContent)
        {
            ILogger logger = context.CreateReplaySafeLogger(nameof(ImportStatusOrchestration));
            logger.LogInformation("Starting import status activities.");
            var statusRespose = new HttpResponseMessage();
            var statusUrl = string.Empty;
            bool isComplete = false;
            TableClient exportTableClient = _azureTableClientFactory.Create(_options.ExportTableName);

            try
            {
                Pageable<TableEntity> jobListimportRunning = exportTableClient.Query<TableEntity>(filter: ent => ent.GetString("IsImportRunning") == "Started" || ent.GetString("IsImportRunning") == "Running");
                if (jobListimportRunning.Count() > 0)
                {
                    foreach (TableEntity item in jobListimportRunning)
                    {
                        while (isComplete == false)
                        {
                            ResponseModel response = await context.CallActivityAsync<ResponseModel>(nameof(ProcessImportStatusCheck), item.GetString("importContentLocation"));

                            if (response.Status == ResponseStatus.Accepted)
                            {
                                logger?.LogInformation($"Import Status check returned: InProgress.");
                                logger?.LogInformation($"Waiting for next status check for {_options.ScheduleInterval} minutes.");
                                DateTime waitTime = context.CurrentUtcDateTime.Add(TimeSpan.FromMinutes(
                            Convert.ToDouble(_options.ScheduleInterval)));
                                TableEntity exportEntity = _azureTableMetadataStore.GetEntity(exportTableClient, _options.PartitionKey, item.RowKey);
                                exportEntity["IsImportRunning"] = "Running";
                                _azureTableMetadataStore.UpdateEntity(exportTableClient, exportEntity);
                                await context.CreateTimer(waitTime, CancellationToken.None);
                            }
                            else if (response.Status == ResponseStatus.Completed)
                            {
                                logger?.LogInformation($"Import Status check returned: Success.");
                                TableEntity exportEntity = _azureTableMetadataStore.GetEntity(exportTableClient, _options.PartitionKey, item.RowKey);
                                exportEntity["IsImportComplete"] = true;
                                exportEntity["IsImportRunning"] = "Completed";
                                _azureTableMetadataStore.UpdateEntity(exportTableClient, exportEntity);
                                isComplete = true;
                            }
                            else
                            {
                                logger?.LogInformation($"Import Status check returned: Unsuccessful.");
                                TableEntity exportEntity = _azureTableMetadataStore.GetEntity(exportTableClient, _options.PartitionKey, item.RowKey);
                                exportEntity["IsImportComplete"] = true;
                                exportEntity["IsImportRunning"] = "Failed";
                                _azureTableMetadataStore.UpdateEntity(exportTableClient, exportEntity);
                                isComplete = true;
                                throw new HttpFailureException($"StatusCode: {statusRespose.StatusCode}, Response: {statusRespose.Content.ReadAsStringAsync()} ");
                            }
                        }

                        isComplete = false;
                    }
                }
            }
            catch
            {
                throw;
            }

            return "completed";
        }

        [Function(nameof(ProcessImportStatusCheck))]
        public async Task<ResponseModel> ProcessImportStatusCheck([ActivityTrigger] string importStatusUrl, FunctionContext executionContext)
        {
            try
            {
                if (!string.IsNullOrEmpty(importStatusUrl))
                {
                    ResponseModel importStatusResponse = await _importProcessor.CheckProcessStatus(importStatusUrl, _options.DestinationUri, _options.DestinationHttpClient);
                    return importStatusResponse;
                }
                else
                {
                    throw new ArgumentException($"Url to check import status was empty.");
                }
            }
            catch
            {
                throw;
            }
        }
    }
}
