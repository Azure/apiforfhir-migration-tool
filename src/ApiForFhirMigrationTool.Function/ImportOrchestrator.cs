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
using Newtonsoft.Json.Linq;

namespace ApiForFhirMigrationTool.Function
{
    public class ImportOrchestrator
    {
        private readonly IFhirProcessor _importProcessor;
        private readonly MigrationOptions _options;
        private readonly IAzureTableClientFactory _azureTableClientFactory;
        private readonly IMetadataStore _azureTableMetadataStore;
        private readonly IOrchestrationHelper _orchestrationHelper;
        private readonly TelemetryClient _telemetryClient;

        public ImportOrchestrator(IFhirProcessor importProcessor, MigrationOptions options, IAzureTableClientFactory azureTableClientFactory, IMetadataStore azureTableMetadataStore, IOrchestrationHelper orchestrationHelper, TelemetryClient telemetryClient)
        {
            _importProcessor = importProcessor;
            _options = options;
            _azureTableClientFactory = azureTableClientFactory;
            _azureTableMetadataStore = azureTableMetadataStore;
            _orchestrationHelper = orchestrationHelper;
            _telemetryClient = telemetryClient;
        }

        [Function(nameof(ImportOrchestration))]
        public async Task<string> ImportOrchestration(
            [OrchestrationTrigger] TaskOrchestrationContext context, string requestContent)
        {
            ILogger logger = context.CreateReplaySafeLogger(nameof(ImportOrchestration));
            logger.LogInformation("Starting import activities.");
            var statusRespose = new HttpResponseMessage();
            var statusUrl = string.Empty;

            try
            {
                TableClient exportTableClient = _azureTableClientFactory.Create(_options.ExportTableName);
                Pageable<TableEntity> jobListimport = exportTableClient.Query<TableEntity>(filter: ent => ent.GetBoolean("IsExportComplete") == true && ent.GetString("ImportRequest") == "Yes" && ent.GetString("IsImportRunning") == "Not Started");
                if (jobListimport.Count() > 0)
                {
                    foreach (TableEntity item in jobListimport)
                    {
                        string? statusUrl_new = item.GetString("exportContentLocation");
                        ResponseModel response = await context.CallActivityAsync<ResponseModel>("ProcessExportStatusCheck", statusUrl_new);

                        var import_body = _orchestrationHelper.CreateImportRequest(response.Content, _options.ImportMode);

                        ResponseModel importResponse = await context.CallActivityAsync<ResponseModel>(nameof(ProcessImport), import_body);
                        if (importResponse.Status == ResponseStatus.Accepted)
                        {
                            logger?.LogInformation($"Import  returned: Success.");
                            statusUrl = importResponse.Content;
                            TableEntity exportEntity = _azureTableMetadataStore.GetEntity(exportTableClient, _options.PartitionKey, item.RowKey);
                            exportEntity["IsImportComplete"] = false;
                            exportEntity["IsImportRunning"] = "Started";
                            exportEntity["importContentLocation"] = importResponse.Content;
                            exportEntity["ImportStartTime"] = DateTime.UtcNow;
                            _azureTableMetadataStore.UpdateEntity(exportTableClient, exportEntity);
                            _telemetryClient.TrackEvent(
                            "Import",
                            new Dictionary<string, string>()
                            {
                                { "ImportId", _orchestrationHelper.GetProcessId(statusUrl) },
                                { "StatusUrl", statusUrl },
                                { "ImportStatus", "Started" },
                            });
                        }
                        else
                        {
                            logger?.LogInformation($"Import Status check returned: Unsuccessful.");
                            string diagnosticsValue = JObject.Parse(importResponse.Content)?["issue"]?[0]?["diagnostics"]?.ToString() ?? "N/A";
                            TableEntity exportEntity = _azureTableMetadataStore.GetEntity(exportTableClient, _options.PartitionKey, item.RowKey);
                            exportEntity["IsImportComplete"] = false;
                            exportEntity["IsImportRunning"] = "Failed";
                            exportEntity["FailureReason"] = diagnosticsValue;
                            _azureTableMetadataStore.UpdateEntity(exportTableClient, exportEntity);
                            _telemetryClient.TrackEvent(
                            "Import",
                            new Dictionary<string, string>()
                            {
                                { "ImportId", _orchestrationHelper.GetProcessId(statusUrl) },
                                { "StatusUrl", statusUrl },
                                { "ImportStatus", "Failed" },
                                { "FailureReason", diagnosticsValue }
                            });
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
                ResponseModel importResponse = await _importProcessor.CallProcess(method, requestContent, _options.DestinationUri, "/$import", _options.DestinationHttpClient);
                return importResponse;
            }
            catch
            {
                throw;
            }
        }
    }
}
