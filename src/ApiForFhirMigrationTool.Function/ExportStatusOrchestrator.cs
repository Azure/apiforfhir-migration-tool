// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Globalization;
using ApiForFhirMigrationTool.Function.Configuration;
using ApiForFhirMigrationTool.Function.ExceptionHelper;
using ApiForFhirMigrationTool.Function.FhirOperation;
using ApiForFhirMigrationTool.Function.Models;
using ApiForFhirMigrationTool.Function.OrchestrationHelper;
using ApiForFhirMigrationTool.Function.Processors;
using Azure;
using Azure.Data.Tables;
using Microsoft.ApplicationInsights;
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
        private readonly TelemetryClient _telemetryClient;

        public ExportStatusOrchestrator(IFhirProcessor exportProcessor, MigrationOptions options, IAzureTableClientFactory azureTableClientFactory, IMetadataStore azureTableMetadataStore, IFhirClient fhirClient, IOrchestrationHelper orchestrationHelper, TelemetryClient telemetryClient)
        {
            _exportProcessor = exportProcessor;
            _options = options;
            _fhirClient = fhirClient;
            _azureTableClientFactory = azureTableClientFactory;
            _azureTableMetadataStore = azureTableMetadataStore;
            _orchestrationHelper = orchestrationHelper;
            _telemetryClient = telemetryClient;
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

            try
            {
                TableClient chunktableClient = _azureTableClientFactory.Create(_options.ChunkTableName);
                TableClient exportTableClient = _azureTableClientFactory.Create(_options.ExportTableName);
                Pageable<TableEntity> exportRunningjobList = exportTableClient.Query<TableEntity>(filter: ent => ent.GetString("IsExportRunning") == "Started" || ent.GetString("IsExportRunning") == "Running");
                if (exportRunningjobList.Count() > 0)
                {
                    foreach (TableEntity item in exportRunningjobList)
                    {
                        while (isComplete == false)
                        {
                            statusUrl = item.GetString("exportContentLocation");
                            ResponseModel response = await context.CallActivityAsync<ResponseModel>(nameof(ProcessExportStatusCheck), statusUrl);
                            if (response.Status == ResponseStatus.Accepted)
                            {
                                logger?.LogInformation($"Export Status check returned: InProgress.");
                                logger?.LogInformation($"Waiting for next status check for {_options.ScheduleInterval} minutes.");
                                DateTime waitTime = context.CurrentUtcDateTime.Add(TimeSpan.FromMinutes(_options.ScheduleInterval));
                                TableEntity exportEntity = _azureTableMetadataStore.GetEntity(exportTableClient, _options.PartitionKey, item.RowKey);
                                exportEntity["IsExportComplete"] = false;
                                exportEntity["IsExportRunning"] = "Running";
                                _azureTableMetadataStore.UpdateEntity(exportTableClient, exportEntity);
                                _telemetryClient.TrackEvent(
                                    "Export",
                                    new Dictionary<string, string>()
                                    {
                                        { "ExportId", _orchestrationHelper.GetProcessId(statusUrl) },
                                        { "StatusUrl", statusUrl },
                                        { "ExportStatus", "Running" },
                                        { "Since", string.Empty },
                                        { "Till", string.Empty },
                                    });
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
                                    var objOutput = objResponse["output"] as JArray;
                                    if (objOutput != null && objOutput.Any())
                                    {
                                        // import_body = _orchestrationHelper.CreateImportRequest(resContent, _options.ImportMode);
                                        var resourceCount = _orchestrationHelper.CalculateSumOfResources(objOutput).ToString(CultureInfo.InvariantCulture);
                                        TableEntity exportEntity = _azureTableMetadataStore.GetEntity(exportTableClient, _options.PartitionKey, item.RowKey);
                                        exportEntity["IsExportComplete"] = true;
                                        exportEntity["IsExportRunning"] = "Completed";
                                        exportEntity["ImportRequest"] = "Yes";
                                        exportEntity["ExportEndTime"] = DateTime.UtcNow;
                                        exportEntity["TotalExportResourceCount"] = resourceCount;
                                        _azureTableMetadataStore.UpdateEntity(exportTableClient, exportEntity);
                                        _telemetryClient.TrackEvent(
                                            "Export",
                                            new Dictionary<string, string>()
                                            {
                                                { "ExportId", _orchestrationHelper.GetProcessId(statusUrl) },
                                                { "StatusUrl", statusUrl },
                                                { "ExportStatus", "Completed" },
                                                { "Since", string.Empty },
                                                { "Till", string.Empty },
                                                { "TotalResources", resourceCount },
                                            });
                                    }
                                    else
                                    {
                                        logger?.LogInformation($"Output is null. No Output content in export:{statusUrl}");

                                        // import_body = string.Empty;
                                        TableEntity exportEntity = _azureTableMetadataStore.GetEntity(exportTableClient, _options.PartitionKey, item.RowKey);
                                        exportEntity["IsExportComplete"] = true;
                                        exportEntity["IsExportRunning"] = "Completed";
                                        exportEntity["IsImportComplete"] = true;
                                        exportEntity["IsImportRunning"] = "Completed";
                                        exportEntity["ImportRequest"] = "No";
                                        exportEntity["EndTime"] = DateTime.UtcNow;
                                        _azureTableMetadataStore.UpdateEntity(exportTableClient, exportEntity);
                                        _telemetryClient.TrackEvent(
                                        "Export",
                                        new Dictionary<string, string>()
                                        {
                                            { "ExportId", _orchestrationHelper.GetProcessId(statusUrl) },
                                            { "StatusUrl", statusUrl },
                                            { "ExportStatus", "Completed" },
                                            { "Since", string.Empty },
                                            { "Till", string.Empty },
                                            { "TotalResources", string.Empty },
                                        });

                                        TableEntity qEntitynew = _azureTableMetadataStore.GetEntity(chunktableClient, _options.PartitionKey, _options.RowKey);

                                        qEntitynew["since"] = exportEntity["Till"];
                                        _azureTableMetadataStore.UpdateEntity(chunktableClient, qEntitynew);
                                    }
                                }

                                isComplete = true;
                            }
                            else
                            {
                                logger?.LogInformation($"Export Status check returned: Unsuccessful.");
                                import_body = string.Empty;
                                string diagnosticsValue = JObject.Parse(response.Content)?["issue"]?[0]?["diagnostics"]?.ToString() ?? "N/A";
                                TableEntity exportEntity = _azureTableMetadataStore.GetEntity(exportTableClient, _options.PartitionKey, item.RowKey);
                                exportEntity["IsExportComplete"] = true;
                                exportEntity["IsExportRunning"] = "Failed";
                                exportEntity["IsImportComplete"] = false;
                                exportEntity["IsImportRunning"] = "Failed";
                                exportEntity["ImportRequest"] = import_body;
                                exportEntity["FailureReason"] = diagnosticsValue;
                                isComplete = true;
                                _azureTableMetadataStore.UpdateEntity(exportTableClient, exportEntity);
                                _telemetryClient.TrackEvent(
                                        "Export",
                                        new Dictionary<string, string>()
                                        {
                                            { "ExportId", _orchestrationHelper.GetProcessId(statusUrl) },
                                            { "StatusUrl", statusUrl },
                                            { "ExportStatus", "Failed" },
                                            { "Since", string.Empty },
                                            { "Till", string.Empty },
                                            { "TotalResources", string.Empty },
                                            { "FailureReason",diagnosticsValue }
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
