// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using ApiForFhirMigrationTool.Function.Configuration;
using ApiForFhirMigrationTool.Function.ExceptionHelper;
using ApiForFhirMigrationTool.Function.FhirOperation;
using ApiForFhirMigrationTool.Function.Models;
using ApiForFhirMigrationTool.Function.OrchestrationHelper;
using ApiForFhirMigrationTool.Function.Processors;
using Azure;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace ApiForFhirMigrationTool.Function
{
    public class ExportStatusOrchestrator
    {
        private readonly IFhirProcessor _exportProcessor;
        private readonly MigrationOptions _options;
        private readonly IFhirClient _fhirClient;
        private readonly IAzureTableClientFactory _azureTableClientFactory;
        private readonly IMetadataStore _azureTableMetadataStore;
        private readonly IOrchestrationHelper _orchestrationHelper;

        public ExportStatusOrchestrator(IFhirProcessor exportProcessor, MigrationOptions options, IAzureTableClientFactory azureTableClientFactory, IMetadataStore azureTableMetadataStore, IFhirClient fhirClient, IOrchestrationHelper orchestrationHelper)
        {
            _exportProcessor = exportProcessor;
            _options = options;
            _fhirClient = fhirClient;
            _azureTableClientFactory = azureTableClientFactory;
            _azureTableMetadataStore = azureTableMetadataStore;
            _orchestrationHelper = orchestrationHelper;
        }

        [Function(nameof(ExportStatusOrchestration))]
        public async Task<string> ExportStatusOrchestration(
            [OrchestrationTrigger] TaskOrchestrationContext context)
        {
            ILogger logger = context.CreateReplaySafeLogger(nameof(ExportStatusOrchestration));
            logger.LogInformation("Starting export status check activities.");
            var statusRespose = new HttpResponseMessage();
            var statusUrl = string.Empty;
            var import_body = string.Empty;
            bool isComplete = false;
            TableClient chunktableClient = _azureTableClientFactory.Create(_options.ChunkTableName);
            TableClient exportTableClient = _azureTableClientFactory.Create(_options.ExportTableName);

            try
            {
                Pageable<TableEntity> exportRunningjobList = exportTableClient.Query<TableEntity>(filter: ent => ent.GetString("IsExportRunning") == "Started" || ent.GetString("IsExportRunning") == "Running");
                if (exportRunningjobList.Count() > 0)
                {
                    foreach (TableEntity item in exportRunningjobList)
                    {
                        while (isComplete == false)
                        {
                            string? statusUrl_new = item.GetString("exportContentLocation");
                            ResponseModel response = await context.CallActivityAsync<ResponseModel>(nameof(ProcessExportStatusCheck), statusUrl_new);
                            if (response.Status == ResponseStatus.Accepted)
                            {
                                logger?.LogInformation($"Export Status check returned: InProgress.");
                                logger?.LogInformation($"Waiting for next status check for {_options.ScheduleInterval} minutes.");
                                DateTime waitTime = context.CurrentUtcDateTime.Add(TimeSpan.FromMinutes(_options.ScheduleInterval));
                                TableEntity exportEntity = _azureTableMetadataStore.GetEntity(exportTableClient, _options.PartitionKey, item.RowKey);
                                exportEntity["IsExportComplete"] = false;
                                exportEntity["IsExportRunning"] = "Running";
                                _azureTableMetadataStore.UpdateEntity(exportTableClient, exportEntity);
                                await context.CreateTimer(waitTime, CancellationToken.None);
                            }
                            else if (response.Status == ResponseStatus.Completed)
                            {
                                logger?.LogInformation($"Export Status check returned: Success.");
                                import_body = response.Content;
                                string? resContent = response.Content;
                                if (!string.IsNullOrEmpty(resContent))
                                {
                                    JObject objResponse = JObject.Parse(resContent);
                                    var objOutput = objResponse["output"];
                                    if (objOutput != null && objOutput.Any())
                                    {
                                        // import_body = _orchestrationHelper.CreateImportRequest(resContent, _options.ImportMode);
                                        TableEntity exportEntity = _azureTableMetadataStore.GetEntity(exportTableClient, _options.PartitionKey, item.RowKey);
                                        exportEntity["IsExportComplete"] = true;
                                        exportEntity["IsExportRunning"] = "Completed";
                                        exportEntity["ImportRequest"] = "Yes";
                                        _azureTableMetadataStore.UpdateEntity(exportTableClient, exportEntity);
                                    }
                                    else
                                    {
                                        logger?.LogInformation($"Output is null. No Output content in export:{statusUrl_new}");
                                        import_body = string.Empty;
                                        TableEntity exportEntity = _azureTableMetadataStore.GetEntity(exportTableClient, _options.PartitionKey, item.RowKey);
                                        exportEntity["IsExportComplete"] = true;
                                        exportEntity["IsExportRunning"] = "Completed";
                                        exportEntity["IsImportComplete"] = true;
                                        exportEntity["IsImportRunning"] = "Completed";
                                        exportEntity["ImportRequest"] = import_body;
                                        _azureTableMetadataStore.UpdateEntity(exportTableClient, exportEntity);
                                    }
                                }

                                isComplete = true;
                            }
                            else
                            {
                                logger?.LogInformation($"Export Status check returned: Unsuccessful.");
                                import_body = string.Empty;
                                TableEntity exportEntity = _azureTableMetadataStore.GetEntity(exportTableClient, _options.PartitionKey, item.RowKey);
                                exportEntity["IsExportComplete"] = true;
                                exportEntity["IsExportRunning"] = "Failed";
                                exportEntity["IsImportComplete"] = false;
                                exportEntity["IsImportRunning"] = "Failed";
                                exportEntity["ImportRequest"] = import_body;
                                isComplete = true;
                                _azureTableMetadataStore.UpdateEntity(exportTableClient, exportEntity);
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

            return "Completed";
        }

        [Function(nameof(ProcessExportStatusCheck))]
        public async Task<ResponseModel> ProcessExportStatusCheck([ActivityTrigger] string exportStatusUrl, FunctionContext executionContext)
        {
            try
            {
                if (!string.IsNullOrEmpty(exportStatusUrl))
                {
                    ResponseModel exportStatusResponse = await _exportProcessor.CheckProcessStatus(exportStatusUrl, _options.SourceUri, _options.SourceHttpClient);
                    return exportStatusResponse;
                }
                else
                {
                    throw new ArgumentException($"Url to check export status was empty.");
                }
            }
            catch
            {
                throw;
            }
        }
    }
}
