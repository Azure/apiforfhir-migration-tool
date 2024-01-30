// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using ApiForFhirMigrationTool.Function.Configuration;
using ApiForFhirMigrationTool.Function.ExceptionHelper;
using ApiForFhirMigrationTool.Function.Models;
using ApiForFhirMigrationTool.Function.OrchestrationHelper;
using ApiForFhirMigrationTool.Function.Processors;
using Azure;
using Azure.Data.Tables;
using Microsoft.ApplicationInsights;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Abstractions;
using Newtonsoft.Json.Linq;

namespace ApiForFhirMigrationTool.Function
{
    public class ImportStatusOrchestrator
    {
        private readonly IFhirProcessor _importProcessor;
        private readonly MigrationOptions _options;
        private readonly IAzureTableClientFactory _azureTableClientFactory;
        private readonly IMetadataStore _azureTableMetadataStore;
        private readonly IOrchestrationHelper _orchestrationHelper;
        private readonly TelemetryClient _telemetryClient;

        public ImportStatusOrchestrator(IFhirProcessor importProcessor, MigrationOptions options, IAzureTableClientFactory azureTableClientFactory, IMetadataStore azureTableMetadataStore, IOrchestrationHelper orchestrationHelper, TelemetryClient telemetryClient)
        {
            _importProcessor = importProcessor;
            _options = options;
            _azureTableClientFactory = azureTableClientFactory;
            _azureTableMetadataStore = azureTableMetadataStore;
            _orchestrationHelper = orchestrationHelper;
            _telemetryClient = telemetryClient;
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

            try
            {
                TableClient exportTableClient = _azureTableClientFactory.Create(_options.ExportTableName);
                TableClient chunktableClient = _azureTableClientFactory.Create(_options.ChunkTableName);
                Pageable<TableEntity> jobListimportRunning = exportTableClient.Query<TableEntity>(filter: ent => ent.GetString("IsImportRunning") == "Started" || ent.GetString("IsImportRunning") == "Running");
                if (jobListimportRunning.Count() > 0)
                {
                    foreach (TableEntity item in jobListimportRunning)
                    {
                        while (isComplete == false)
                        {
                            statusUrl = item.GetString("importContentLocation");
                            ResponseModel response = await context.CallActivityAsync<ResponseModel>(nameof(ProcessImportStatusCheck), statusUrl);

                            if (response.Status == ResponseStatus.Accepted)
                            {
                                logger?.LogInformation($"Import Status check returned: InProgress.");
                                logger?.LogInformation($"Waiting for next status check for {_options.ScheduleInterval} minutes.");
                                DateTime waitTime = context.CurrentUtcDateTime.Add(TimeSpan.FromMinutes(
                            Convert.ToDouble(_options.ScheduleInterval)));
                                TableEntity exportEntity = _azureTableMetadataStore.GetEntity(exportTableClient, _options.PartitionKey, item.RowKey);
                                exportEntity["IsImportRunning"] = "Running";
                                _azureTableMetadataStore.UpdateEntity(exportTableClient, exportEntity);
                                _telemetryClient.TrackEvent(
                                "Import",
                                new Dictionary<string, string>()
                                {
                                    { "ImportId", _orchestrationHelper.GetProcessId(statusUrl) },
                                    { "StatusUrl", statusUrl },
                                    { "ImportStatus", "Running" },
                                });
                                await context.CreateTimer(waitTime, CancellationToken.None);
                            }
                            else if (response.Status == ResponseStatus.Completed)
                            {
                                string? resContent = response.Content;
                                var resourceCount = string.Empty;
                                if (!string.IsNullOrEmpty(resContent))
                                {
                                    JObject objResponse = JObject.Parse(resContent);
                                    var objOutput = objResponse["output"] as JArray;
                                    if (objOutput != null && objOutput.Any())
                                    {
                                        resourceCount = _orchestrationHelper.CalculateSumOfResources(objOutput).ToString();
                                    }
                                }
                                logger?.LogInformation($"Import Status check returned: Success.");
                                TableEntity exportEntity = _azureTableMetadataStore.GetEntity(exportTableClient, _options.PartitionKey, item.RowKey);
                                exportEntity["IsImportComplete"] = true;
                                exportEntity["IsImportRunning"] = "Completed";
                                exportEntity["EndTime"] = DateTime.UtcNow;
                                exportEntity["TotalImportResourceCount"] = resourceCount;
                                _azureTableMetadataStore.UpdateEntity(exportTableClient, exportEntity);

                                TableEntity qEntitynew = _azureTableMetadataStore.GetEntity(chunktableClient, _options.PartitionKey, _options.RowKey);

                                qEntitynew["since"] = exportEntity["Till"];
                                _azureTableMetadataStore.UpdateEntity(chunktableClient, qEntitynew);
                                _telemetryClient.TrackEvent(
                                "Import",
                                new Dictionary<string, string>()
                                {
                                    { "ImportId", _orchestrationHelper.GetProcessId(statusUrl) },
                                    { "StatusUrl", statusUrl },
                                    { "ImportStatus", "Completed" },
                                    { "TotalResources", resourceCount },
                                });
                                isComplete = true;
                            }
                            else
                            {
                                logger?.LogInformation($"Import Status check returned: Unsuccessful.");
                                TableEntity exportEntity = _azureTableMetadataStore.GetEntity(exportTableClient, _options.PartitionKey, item.RowKey);
                                exportEntity["IsImportComplete"] = true;
                                exportEntity["IsImportRunning"] = "Failed";
                                exportEntity["EndTime"] = DateTime.UtcNow;
                                _azureTableMetadataStore.UpdateEntity(exportTableClient, exportEntity);
                                isComplete = true;
                                _telemetryClient.TrackEvent(
                                "Import",
                                new Dictionary<string, string>()
                                {
                                    { "ImportId", _orchestrationHelper.GetProcessId(statusUrl) },
                                    { "StatusUrl", statusUrl },
                                    { "ImportStatus", "Failed" },
                                });
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
