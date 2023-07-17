// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using Azure;
using Azure.Data.Tables;
using FhirMigrationTool.Configuration;
using FhirMigrationTool.ExceptionHelper;
using FhirMigrationTool.FhirOperation;
using FhirMigrationTool.Models;
using FhirMigrationTool.OrchestrationHelper;
using FhirMigrationTool.Processors;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FhirMigrationTool
{
    public class ExportOrchestrator
    {
        private readonly IFhirProcessor _exportProcessor;
        private readonly MigrationOptions _options;
        private readonly IFhirClient _fhirClient;
        private readonly IAzureTableClientFactory _azureTableClientFactory;
        private readonly IMetadataStore _azureTableMetadataStore;
        private readonly IOrchestrationHelper _orchestrationHelper;

        public ExportOrchestrator(IFhirProcessor exportProcessor, MigrationOptions options, IAzureTableClientFactory azureTableClientFactory, IMetadataStore azureTableMetadataStore, IFhirClient fhirClient, IOrchestrationHelper orchestrationHelper)
        {
            _exportProcessor = exportProcessor;
            _options = options;
            _fhirClient = fhirClient;
            _azureTableClientFactory = azureTableClientFactory;
            _azureTableMetadataStore = azureTableMetadataStore;
            _orchestrationHelper = orchestrationHelper;
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
            TableClient chunktableClient = _azureTableClientFactory.Create(_options.ChunkTableName);
            TableClient exportTableClient = _azureTableClientFactory.Create(_options.ExportTableName);

            try
            {
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

            try
            {
                Pageable<TableEntity> exportRunningjobList = exportTableClient.Query<TableEntity>(filter: ent => ent.GetString("IsExportRunning") == "Started" || ent.GetString("IsExportRunning") == "Running");
                if (exportRunningjobList.Count() > 0)
                {
                    foreach (TableEntity item in exportRunningjobList)
                    {
                        while (true)
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
                                        import_body = _orchestrationHelper.CreateImportRequest(resContent, _options.ImportMode);
                                        TableEntity exportEntity = _azureTableMetadataStore.GetEntity(exportTableClient, _options.PartitionKey, item.RowKey);
                                        exportEntity["IsExportComplete"] = true;
                                        exportEntity["IsExportRunning"] = "Completed";
                                        exportEntity["ImportRequest"] = import_body;
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

                                break;
                            }
                            else
                            {
                                logger?.LogInformation($"Export Status check returned: Unsuccessful.");
                                throw new HttpFailureException($"StatusCode: {statusRespose.StatusCode}, Response: {statusRespose.Content.ReadAsStringAsync()} ");
                            }
                        }
                    }
                }
            }
            catch
            {
                throw;
            }

            return import_body;
        }

        [Function(nameof(ProcessExport))]
        public async Task<ResponseModel> ProcessExport([ActivityTrigger] string name, FunctionContext executionContext)
        {
            try
            {
                HttpMethod method = HttpMethod.Get;
                string query = GetQueryStringForExport();
                ResponseModel exportResponse = await _exportProcessor.CallProcess(method, string.Empty, _options.SourceUri, query, _options.SourceHttpClient);

                TableClient chunktableClient = _azureTableClientFactory.Create(_options.ChunkTableName);
                TableClient exportTableClient = _azureTableClientFactory.Create(_options.ExportTableName);
                var statusUrl = string.Empty;

                int sinceStartIndex = query.IndexOf("=") + 1;
                int tillStartIndex = query.IndexOf("_till=") + 6;

                string sinceValue = query.Substring(sinceStartIndex, query.IndexOf("&") - sinceStartIndex);
                string tillValue = query.Substring(tillStartIndex);

                if (exportResponse.Status == ResponseStatus.Completed)
                {
                    statusUrl = exportResponse.Content;

                    TableEntity qEntity = _azureTableMetadataStore.GetEntity(chunktableClient, "mypartitionkey", "myrowkey");
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
                            };
                    _azureTableMetadataStore.AddEntity(exportTableClient, tableEntity);
                    TableEntity qEntitynew = _azureTableMetadataStore.GetEntity(chunktableClient, "mypartitionkey", "myrowkey");
                    qEntitynew["since"] = tillValue;
                    qEntitynew["JobId"] = jobId++;
                    _azureTableMetadataStore.UpdateEntity(chunktableClient, qEntitynew);
                }
                else
                {
                    TableEntity qEntity = _azureTableMetadataStore.GetEntity(chunktableClient, "mypartitionkey", "myrowkey");
                    int jobId = (int)qEntity["JobId"];
                    string rowKey = _options.RowKey + jobId++;

                    var tableEntity = new TableEntity(_options.PartitionKey, rowKey)
                            {
                                { "exportContentLocation", statusUrl },
                                { "importContentLocation", string.Empty },
                                { "IsExportComplete", false },
                                { "IsExportRunning", "Failed" },
                                { "IsImportComplete", false },
                                { "IsImportRunning", "Not Started" },
                                { "ImportRequest", string.Empty },
                            };
                    _azureTableMetadataStore.AddEntity(exportTableClient, tableEntity);
                    TableEntity qEntitynew = _azureTableMetadataStore.GetEntity(chunktableClient, "mypartitionkey", "myrowkey");
                    qEntitynew["JobId"] = jobId++;

                    _azureTableMetadataStore.UpdateEntity(chunktableClient, qEntitynew);

                    throw new HttpFailureException($"Status: {exportResponse.Status} Response: {exportResponse.Content} ");
                }

                return exportResponse;
            }
            catch
            {
                throw;
            }
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

        private string GetQueryStringForExport()
        {
            var since = string.Empty;
            var till = string.Empty;
            var since_new = default(DateTimeOffset);
            TableClient chunktableClient = _azureTableClientFactory.Create(_options.ChunkTableName);
            TableClient exportTableClient = _azureTableClientFactory.Create(_options.ExportTableName);
            TableEntity qEntity = _azureTableMetadataStore.GetEntity(chunktableClient, "mypartitionkey", "myrowkey");
            since = (string)qEntity["since"];

            if (_options.StartDate == DateTime.MinValue && string.IsNullOrEmpty(since))
            {
                var sinceDate = SinceDate();
                since_new = sinceDate.Result;
                since = since_new.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
                var updateSinceDate = since_new.AddDays(_options.ExportChunkTime);
                till = updateSinceDate.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            }
            else if (string.IsNullOrEmpty(since))
            {
                since = _options.StartDate.ToString("yyyy-MM-ddTH:mm:ss.fffZ");
                var updateSinceDate = _options.StartDate.AddDays(_options.ExportChunkTime);
                till = updateSinceDate.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            }
            else
            {
                DateTimeOffset newSince = DateTimeOffset.Parse(since);
                var updateSinceDate = newSince.AddDays(_options.ExportChunkTime);
                till = updateSinceDate.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            }

            string query = string.Format("?_since={0}&_till={1}", since, till);

            return $"/$export{query}";
        }

        private async Task<DateTimeOffset> SinceDate()
        {
            var since = string.Empty;
            Uri baseUri = _options.SourceUri;
            string sourceFhirEndpoint = _options.SourceHttpClient;
            var firstResource = new object();
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri(baseUri, "/?_sort=_lastUpdated&_count=1"),
            };
            HttpResponseMessage srcTask = await _fhirClient.Send(request, baseUri, sourceFhirEndpoint);

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
            var sinceDate = DateTimeOffset.ParseExact(firstTimestamp, "yyyy-MM-ddTH:mm:ss.fffZ", null);

            // since = sinceDate.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            return sinceDate;
        }
    }
}
