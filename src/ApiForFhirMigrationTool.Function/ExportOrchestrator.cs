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
using Microsoft.ApplicationInsights;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace ApiForFhirMigrationTool.Function
{
    public class ExportOrchestrator
    {
        private readonly IFhirProcessor _exportProcessor;
        private readonly MigrationOptions _options;
        private readonly IFhirClient _fhirClient;
        private readonly IAzureTableClientFactory _azureTableClientFactory;
        private readonly IMetadataStore _azureTableMetadataStore;
        private readonly IOrchestrationHelper _orchestrationHelper;
        private readonly TelemetryClient _telemetryClient;

        public ExportOrchestrator(IFhirProcessor exportProcessor, MigrationOptions options, IAzureTableClientFactory azureTableClientFactory, IMetadataStore azureTableMetadataStore, IFhirClient fhirClient, IOrchestrationHelper orchestrationHelper, TelemetryClient telemetryClient)
        {
            _exportProcessor = exportProcessor;
            _options = options;
            _fhirClient = fhirClient;
            _azureTableClientFactory = azureTableClientFactory;
            _azureTableMetadataStore = azureTableMetadataStore;
            _orchestrationHelper = orchestrationHelper;
            _telemetryClient = telemetryClient;
        }

        [Function(nameof(ExportOrchestration))]
        public async Task<string> ExportOrchestration(
            [OrchestrationTrigger] TaskOrchestrationContext context)
        {
            ILogger logger = context.CreateReplaySafeLogger(nameof(ExportOrchestration));
            logger.LogInformation("Starting export activities.");
            var statusRespose = new HttpResponseMessage();
            var statusUrl = string.Empty;
            var import_body = string.Empty;

            try
            {
                TableClient chunktableClient = _azureTableClientFactory.Create(_options.ChunkTableName);
                TableClient exportTableClient = _azureTableClientFactory.Create(_options.ExportTableName);

                Pageable<TableEntity> jobList = exportTableClient.Query<TableEntity>(filter: ent => ent.GetString("IsExportRunning") == "Running" || ent.GetString("IsExportRunning") == "Started" || ent.GetString("IsImportRunning") == "Running" || ent.GetString("IsImportRunning") == "Started" || ent.GetString("IsImportRunning") == "Not Started");
                if (jobList.Count() <= 0)
                {
                    ResponseModel exportResponse = await context.CallActivityAsync<ResponseModel>(nameof(ProcessExport));
                }
            }
            catch
            {
                throw;
            }

            return "Completed";
        }

        [Function(nameof(ProcessExport))]
        public async Task<ResponseModel> ProcessExport([ActivityTrigger] string name, FunctionContext executionContext)
        {
            ResponseModel exportResponse = new ResponseModel();
            ILogger logger = executionContext.GetLogger("ProcessExport");
            try
            {
                HttpMethod method = HttpMethod.Get;
                string query = GetQueryStringForExport();
                string sinceValue = string.Empty;
                string tillValue = string.Empty;
                exportResponse = await _exportProcessor.CallProcess(method, string.Empty, _options.SourceUri, query, _options.SourceHttpClient);

                TableClient chunktableClient = _azureTableClientFactory.Create(_options.ChunkTableName);
                TableClient exportTableClient = _azureTableClientFactory.Create(_options.ExportTableName);
                var statusUrl = string.Empty;

                if (_options.ExportWithHistory == true || _options.ExportWithDelete == true)
                {
                    int sinceStartIndex = query.IndexOf("_since=") + 7;
                    int tillStartIndex = query.IndexOf("_till=") + 6;
                    sinceValue = query.Substring(sinceStartIndex, query.IndexOf("&", sinceStartIndex) - sinceStartIndex);
                    tillValue = query.Substring(tillStartIndex);
                }
                else
                {
                    int sinceStartIndex = query.IndexOf("=") + 1;
                    int tillStartIndex = query.IndexOf("_till=") + 6;
                    sinceValue = query.Substring(sinceStartIndex, query.IndexOf("&") - sinceStartIndex);
                    tillValue = query.Substring(tillStartIndex);
                }


                if (exportResponse.Status == ResponseStatus.Accepted)
                {

                    statusUrl = exportResponse.Content;

                    TableEntity qEntity = _azureTableMetadataStore.GetEntity(chunktableClient, _options.PartitionKey, _options.RowKey);
                    if (qEntity["JobId"] != null)
                    {
                        int jobId = (int)qEntity["JobId"];
                        string rowKey = _options.RowKey + jobId++;

                        var tableEntity = new TableEntity(_options.PartitionKey, rowKey)
                            {
                                { "exportContentLocation", statusUrl },
                                { "importContentLocation", string.Empty },
                                { "IsExportComplete", false },
                                { "IsExportRunning", "Started" },
                                { "IsImportComplete", false },
                                { "IsImportRunning", "Not Started" },
                                { "ImportRequest", string.Empty },
                                { "Since", sinceValue },
                                { "Till", tillValue },
                                { "StartTime", DateTime.UtcNow },
                            };
                        _azureTableMetadataStore.AddEntity(exportTableClient, tableEntity);
                        TableEntity qEntitynew = _azureTableMetadataStore.GetEntity(chunktableClient, _options.PartitionKey, _options.RowKey);

                        // qEntitynew["since"] = tillValue;
                        qEntitynew["JobId"] = jobId++;
                        _azureTableMetadataStore.UpdateEntity(chunktableClient, qEntitynew);
                        _telemetryClient.TrackEvent(
                        "Export",
                        new Dictionary<string, string>()
                        {
                            { "ExportId", _orchestrationHelper.GetProcessId(statusUrl) },
                            { "StatusUrl", statusUrl },
                            { "ExportStatus", "Started" },
                            { "Since", sinceValue },
                            { "Till", tillValue },
                        });
                    }
                }
                else
                {
                    TableEntity qEntity = _azureTableMetadataStore.GetEntity(chunktableClient, _options.PartitionKey, _options.RowKey);
                    if (qEntity["JobId"] != null)
                    {
                        int jobId = (int)qEntity["JobId"];
                        string rowKey = _options.RowKey + jobId++;
                        string diagnosticsValue = JObject.Parse(exportResponse.Content)?["issue"]?[0]?["diagnostics"]?.ToString() ?? "For more information check Content location.";
                        logger?.LogInformation($"Export check returned: Unsuccessful. Reason : {diagnosticsValue}");
                        var tableEntity = new TableEntity(_options.PartitionKey, rowKey)
                            {
                                { "exportContentLocation", statusUrl },
                                { "importContentLocation", string.Empty },
                                { "IsExportComplete", false },
                                { "IsExportRunning", "Failed" },
                                { "IsImportComplete", false },
                                { "IsImportRunning", "Failed" },
                                { "ImportRequest", string.Empty },
                                { "FailureReason",diagnosticsValue }
                            };
                        _azureTableMetadataStore.AddEntity(exportTableClient, tableEntity);
                        TableEntity qEntitynew = _azureTableMetadataStore.GetEntity(chunktableClient, _options.PartitionKey, _options.RowKey);
                        qEntitynew["JobId"] = jobId++;

                        _azureTableMetadataStore.UpdateEntity(chunktableClient, qEntitynew);

                        _telemetryClient.TrackEvent(
                       "Export",
                       new Dictionary<string, string>()
                       {
                            { "ExportId", _orchestrationHelper.GetProcessId(statusUrl) },
                            { "StatusUrl", statusUrl },
                            { "ExportStatus", "Failed" },
                            { "Since", sinceValue },
                            { "Till", tillValue },
                           { "FailureReason", diagnosticsValue }
                       });

                        throw new HttpFailureException($"Status: {exportResponse.Status} Response: {exportResponse.Content} ");
                    }
                }
            }
            catch
            {
                throw;
            }

            return exportResponse;
        }

        private string GetQueryStringForExport()
        {
            var since = string.Empty;
            var till = string.Empty;
            var since_new = default(DateTimeOffset);
            var updateSinceDate = default(DateTimeOffset);
            TableClient chunktableClient = _azureTableClientFactory.Create(_options.ChunkTableName);
            TableClient exportTableClient = _azureTableClientFactory.Create(_options.ExportTableName);
            TableEntity qEntity = _azureTableMetadataStore.GetEntity(chunktableClient, _options.PartitionKey, _options.RowKey);
            since = (string)qEntity["since"];
            var duration = _options.ExportChunkDuration;

            if (_options.StartDate == DateTime.MinValue && string.IsNullOrEmpty(since))
            {
                var sinceDate = SinceDate();
                since_new = sinceDate.Result;          
            }
            else if (string.IsNullOrEmpty(since))
            {
                since_new = _options.StartDate;              
            }
            else
            {
                since_new = DateTimeOffset.Parse(since);           
            }

            if (duration != null)
            {
                if (duration == "Days")
                {
                    updateSinceDate = since_new.AddDays(_options.ExportChunkTime);
                }
                else if (duration == "Hours")
                {
                    updateSinceDate = since_new.AddHours(_options.ExportChunkTime);
                }
                else
                {
                    updateSinceDate = since_new.AddMinutes(_options.ExportChunkTime);
                }
            }

            if (updateSinceDate > DateTimeOffset.UtcNow)
            {
                updateSinceDate = DateTimeOffset.UtcNow;
            }

            since = since_new.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            till = updateSinceDate.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            string query= string.Empty;

            if (_options.ExportWithHistory == true && _options.ExportWithDelete == true)
                query = string.Format("?includeAssociatedData=_history,_deleted&_since={0}&_till={1}", since, till);
            else if (_options.ExportWithHistory == true)
                query = string.Format("?includeAssociatedData=_history&_since={0}&_till={1}", since, till);
            else if (_options.ExportWithDelete == true)
                query = string.Format("?includeAssociatedData=_deleted&_since={0}&_till={1}", since, till);
            else
                query = string.Format("?_since={0}&_till={1}", since, till); 

            return $"/$export{query}";
        }

        private async Task<DateTimeOffset> SinceDate()
        {
            var since = string.Empty;
            Uri baseUri = _options.SourceUri;
            string sourceFhirEndpoint = _options.SourceHttpClient;
            var firstResource = new object();
            var sinceDate = default(DateTimeOffset);
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri(baseUri, "/?_sort=_lastUpdated&_count=1"),
            };
            HttpResponseMessage srcTask = await _fhirClient.Send(request, baseUri, sourceFhirEndpoint);

            if (srcTask != null)
            {
                var objResponse = JObject.Parse(srcTask.Content.ReadAsStringAsync().Result);
                JToken? entry = objResponse["entry"];

                if (entry != null)
                {
                    foreach (JToken item in entry)
                    {
                        var gen1Response = (JObject?)item["resource"];
                        if (gen1Response is not null)
                        {
                            var meta = (JObject?)gen1Response.GetValue("meta");
                            if (meta is not null)
                            {
                                firstResource = meta["lastUpdated"];
                            }
                        }
                    }
                }

                var settings = new JsonSerializerSettings
                {
                    DateFormatString = "yyyy-MM-ddTH:mm:ss.fffZ",
                    DateTimeZoneHandling = DateTimeZoneHandling.Utc,
                };

                var firstTimestamp = JsonConvert.SerializeObject(firstResource, settings).Trim('"');
                sinceDate = DateTimeOffset.ParseExact(firstTimestamp, "yyyy-MM-ddTH:mm:ss.fffZ", null);
            }

            // since = sinceDate.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            return sinceDate;
        }
    }
}
