// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using ApiForFhirMigrationTool.Function.Configuration;
using ApiForFhirMigrationTool.Function.ExceptionHelper;
using ApiForFhirMigrationTool.Function.FhirOperation;
using ApiForFhirMigrationTool.Function.Models;
using ApiForFhirMigrationTool.Function.OrchestrationHelper;
using ApiForFhirMigrationTool.Function.Processors;
using Azure;
using Azure.Core;
using Azure.Data.Tables;
using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Http;
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
                logger.LogInformation("Checking whether the chunk and export table exists or not");
                TableClient chunktableClient = _azureTableClientFactory.Create(_options.ChunkTableName);
                TableClient exportTableClient = _azureTableClientFactory.Create(_options.ExportTableName);
                logger.LogInformation(" Query the export table to check for running or incomplete jobs.");
               Pageable <TableEntity> jobList = exportTableClient.Query<TableEntity>(filter: ent => ent.GetString("IsExportRunning") == "Running" || ent.GetString("IsExportRunning") == "Started" || ent.GetString("IsImportRunning") == "Running" || ent.GetString("IsImportRunning") == "Started" || ent.GetString("IsImportRunning") == "Not Started" || ent.GetBoolean("IsProcessed") == false);
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
            ILogger logger = executionContext.GetLogger("ProcessExport");
            logger?.LogInformation($"Export process Started");
            ResponseModel exportResponse = new ResponseModel();
            try
            {
                HttpMethod method = HttpMethod.Get;
                logger?.LogInformation($"Getting Query for export operation");
                string query = GetQueryStringForExport();
                if (!string.IsNullOrEmpty(query))
                {
                    string sinceValue = string.Empty;
                    string tillValue = string.Empty;
                    string resourceTypeValue = string.Empty;
                     exportResponse = await _exportProcessor.CallProcess(method, string.Empty, _options.SourceUri, query, _options.SourceHttpClient);

                    TableClient chunktableClient = _azureTableClientFactory.Create(_options.ChunkTableName);
                    TableClient exportTableClient = _azureTableClientFactory.Create(_options.ExportTableName);
                    var statusUrl = string.Empty;

                    string pattern = @"_since=(.*?)&_till=(.*?)($|&)";
                    Match match = Regex.Match(query, pattern);
                    if (match.Success)
                    {
                        sinceValue = match.Groups[1].Value;
                        tillValue = match.Groups[2].Value;
                    }
                    if (!_options.IsParallel)
                    {
                        string patternResourceType = @"[?&]_type=([^&]+)";
                        // Match the pattern against the URL
                        Match matchResourceType = Regex.Match(query, patternResourceType);

                        // Check if a match is found
                        if (matchResourceType.Success)
                        {
                            resourceTypeValue = matchResourceType.Groups[1].Value;
                        }
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
                                    {"resourceTypeValue",resourceTypeValue }
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
                            //ILogger logger = executionContext.GetLogger("ProcessExport");
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
            }
            catch
            {
                throw;
            }
            logger?.LogInformation($"Export process Finished");
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
            since = _options.IsParallel == true ? (string)qEntity["since"] : (string)qEntity["globalSinceExportType"];
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
                    updateSinceDate = _options.IsParallel == true ? since_new.AddDays(_options.ExportChunkTime) : since_new.AddDays(_options.ResourceExportChunkTime);
                }
                else if (duration == "Hours")
                {
                    updateSinceDate = _options.IsParallel == true ? since_new.AddHours(_options.ExportChunkTime) : since_new.AddHours(_options.ResourceExportChunkTime);
                }
                else
                {
                    updateSinceDate = _options.IsParallel == true ? since_new.AddMinutes(_options.ExportChunkTime) : since_new.AddMinutes(_options.ResourceExportChunkTime);
                }
            }

            if (updateSinceDate > DateTimeOffset.UtcNow)
            {
                if (_options.IsParallel == true)
                {
                    updateSinceDate = DateTimeOffset.UtcNow;
                }
                else
                {
                    updateSinceDate = qEntity["globalTillExportType"]?.ToString() != ""
                    ? DateTimeOffset.Parse((string)qEntity["globalTillExportType"])
                    : DateTimeOffset.UtcNow;
                }
            }

            since = since_new.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            till = updateSinceDate.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

            //need to check count whether getting data from gen1
            string? resourceType = string.Empty;
            string setUrl = string.Empty;
            List<string>? completedResourceTypeList = new List<string>();

            if (_options.IsParallel == true)
            {
                var checkValidRequest = CheckResourceCount(since, till, _options.ExportChunkTime, _options.ExportChunkDuration);
                till = checkValidRequest.Result.ToString();
                if (_options.IsExportDeidentified == true)
                {
                    string configFileName = Path.GetFileNameWithoutExtension(_options.ConfigFile);
                    setUrl = $"/$export?_isParallel={_options.IsParallel.ToString().ToLower()}&_container=anonymization&_anonymizationConfig={configFileName}.json";
                }
                else
                {
                    setUrl = $"/$export?_isParallel={_options.IsParallel.ToString().ToLower()}";
                }
            }
            else
            {
                bool IsLastRun = false;
                int tot = 0;
                TableEntity qEntityGetResourceIndex = _azureTableMetadataStore.GetEntity(chunktableClient, _options.PartitionKey, _options.RowKey);
                int index = (int)qEntityGetResourceIndex["resourceTypeIndex"];  // get index from DB

                // mark global since and till
                if (index == 0) // 
                {
                    qEntityGetResourceIndex["globalSinceExportType"] = since;
                    qEntityGetResourceIndex["globalTillExportType"] = till;
                    _azureTableMetadataStore.UpdateEntity(chunktableClient, qEntityGetResourceIndex);
                }
                do
                {
                    resourceType = _options.ResourceTypes?[index];
                    if (qEntityGetResourceIndex?["multiExport"].ToString() == "Running")
                    {
                        since = qEntityGetResourceIndex["subSinceExportType"].ToString();
                        till = qEntityGetResourceIndex["subTillExportType"].ToString();
                    }
                    var response = CheckResourceTypeCount(since!, till!, resourceType!, _options.ResourceExportChunkTime, _options.ExportChunkDuration);
                    tot = response.Result;
                    qEntityGetResourceIndex = _azureTableMetadataStore.GetEntity(chunktableClient, _options.PartitionKey, _options.RowKey);
                    if (tot > 0 && index != (int)qEntityGetResourceIndex["resourceTypeIndex"])
                    {

                        qEntityGetResourceIndex["resourceTypeIndex"] = index; // 
                        _azureTableMetadataStore.UpdateEntity(chunktableClient, qEntityGetResourceIndex);
                    }
                    IsLastRun = CheckLastCount(index);
                    if (tot == 0 && qEntityGetResourceIndex?["multiExport"].ToString() == "Running")
                    {
                        // multiexport run and no data to export then assigining till to since and global till to sub till
                        qEntityGetResourceIndex["subSinceExportType"] = qEntityGetResourceIndex["subTillExportType"];
                        qEntityGetResourceIndex["subTillExportType"] = qEntityGetResourceIndex["globalTillExportType"];
                        _azureTableMetadataStore.UpdateEntity(chunktableClient, qEntityGetResourceIndex);
                    }
                    if (qEntityGetResourceIndex?["multiExport"].ToString() != "Running" && tot == 0 && index < _options.ResourceTypes?.Count() - 1)
                    {
                        index++;
                    }

                } while (tot == 0 && IsLastRun == false);
                if (qEntityGetResourceIndex?["multiExport"].ToString() == "Running")
                {
                    qEntityGetResourceIndex = _azureTableMetadataStore.GetEntity(chunktableClient, _options.PartitionKey, _options.RowKey);
                    since = qEntityGetResourceIndex["subSinceExportType"].ToString(); // assigning till to since as next round trip withing subexport
                    till = qEntityGetResourceIndex["subTillExportType"].ToString();
                }
                if (tot == 0 && IsLastRun == true)
                {
                    TableEntity qEntitynew = _azureTableMetadataStore.GetEntity(chunktableClient, _options.PartitionKey, _options.RowKey);
                    qEntitynew["globalSinceExportType"] = qEntitynew["globalTillExportType"];
                    qEntitynew["globalTillExportType"] = "";
                    qEntitynew["resourceTypeIndex"] = 0; // all the import will done so will reset index
                    qEntitynew["multiExport"] = "";
                    qEntitynew["subSinceExportType"] = "";
                    qEntitynew["subTillExportType"] = "";
                    _azureTableMetadataStore.UpdateEntity(chunktableClient, qEntitynew);
                    return "";
                }
                else
                {
                    setUrl = $"/$export?_type={resourceType}";
                }
            }

            string query = string.Empty;

            if (_options.ExportWithHistory == true && _options.ExportWithDelete == true)
                query = string.Format("includeAssociatedData=_history,_deleted&_since={0}&_till={1}", since, till);
            else if (_options.ExportWithHistory == true)
                query = string.Format("includeAssociatedData=_history&_since={0}&_till={1}", since, till);
            else if (_options.ExportWithDelete == true)
                query = string.Format("includeAssociatedData=_deleted&_since={0}&_till={1}", since, till);
            else
                query = string.Format("_since={0}&_till={1}", since, till);

            return $"{setUrl}&{query}";
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
            return sinceDate;
        }

        private async Task<string> CheckResourceCount(string since, string till, int chunkTimeDuration, string chunckDuration)
        {
            try
            {
                Uri baseUri = _options.SourceUri;
                string sourceFhirEndpoint = _options.SourceHttpClient;

                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri(baseUri, $"/?_summary=count&_lastUpdated=ge{since}&_lastUpdated=lt{till}"),
                };
                HttpResponseMessage response = await _fhirClient.Send(request, baseUri, sourceFhirEndpoint);

                if (response != null && response.IsSuccessStatusCode)
                {
                    var objResponse = JObject.Parse(response.Content.ReadAsStringAsync().Result);
                    int? total = (int?)objResponse.GetValue("total");
                    if (total <= _options.ChunkLimit)
                    {
                        return till;
                    }
                    else
                    {
                        // if data>100M in one day then chunktime reduced in hours, till one hour diffrence between till and since 
                        if ((chunkTimeDuration == 1 && chunckDuration == "Days") || chunkTimeDuration > 1)
                        {
                            DateTimeOffset till_reduced;
                            DateTimeOffset sinceDateTime = DateTimeOffset.Parse(since);
                            if (chunkTimeDuration == 1 && chunckDuration == "Days")
                            {
                                chunckDuration = "Hours";
                                chunkTimeDuration = 24;
                            }
                            chunkTimeDuration = chunkTimeDuration / 2;
                            if (chunckDuration == "Days")
                            {
                                till_reduced = sinceDateTime.AddDays(chunkTimeDuration);
                            }
                            else if (chunckDuration == "Hours")
                            {
                                till_reduced = sinceDateTime.AddHours(chunkTimeDuration);
                            }
                            else
                            {
                                till_reduced = sinceDateTime.AddMinutes(chunkTimeDuration);
                            }
                            till = till_reduced.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
                            return await CheckResourceCount(since, till, chunkTimeDuration, chunckDuration);
                        }
                    }
                }
                return till;
            }
            catch
            {
                // _logger.LogError($"Error occurred during migration process: {ex.Message}");
                return till;
                throw;
            }
        }

        private async Task<int> CheckResourceTypeCount(string since, string till, string resourceType, int chunkTimeDuration, string chunckDuration)
        {
            int? total = 0;
            try
            {
                Uri baseUri = _options.SourceUri;
                string sourceFhirEndpoint = _options.SourceHttpClient;
                // global since and till 
                TableClient chunktableClient = _azureTableClientFactory.Create(_options.ChunkTableName);
                TableEntity qEntityIndex = _azureTableMetadataStore.GetEntity(chunktableClient, _options.PartitionKey, _options.RowKey);
                string? globalTill = string.Empty;
                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri(baseUri, $"/?_type={resourceType}&_summary=count&_lastUpdated=ge{since}&_lastUpdated=lt{till}"),
                };
                HttpResponseMessage response = await _fhirClient.Send(request, baseUri, sourceFhirEndpoint);

                if (response != null && response.IsSuccessStatusCode)
                {
                    var objResponse = JObject.Parse(response.Content.ReadAsStringAsync().Result);
                    total = (int?)objResponse.GetValue("total");

                    qEntityIndex = _azureTableMetadataStore.GetEntity(chunktableClient, _options.PartitionKey, _options.RowKey);
                    globalTill = qEntityIndex["globalTillExportType"].ToString();
                    if (total <= _options.ChunkLimit)
                    {

                        if (!string.IsNullOrEmpty(globalTill) && DateTimeOffset.Parse(till) == DateTimeOffset.Parse(globalTill)) // if till match with global till then done for resource type
                        {
                            if (qEntityIndex["multiExport"].ToString() == "Running")
                            {
                                qEntityIndex["subSinceExportType"] = since;
                                qEntityIndex["subTillExportType"] = till;
                                _azureTableMetadataStore.UpdateEntity(chunktableClient, qEntityIndex);
                            }
                        }
                        else if (!string.IsNullOrEmpty(globalTill) && DateTimeOffset.Parse(till) < DateTimeOffset.Parse(globalTill))
                        {
                            // mark sub since and till 
                            qEntityIndex["subSinceExportType"] = since;
                            qEntityIndex["subTillExportType"] = till;
                            qEntityIndex["multiExport"] = "Running";
                            _azureTableMetadataStore.UpdateEntity(chunktableClient, qEntityIndex);
                        }
                        return (int)total;
                    }
                    else
                    {
                        if ((chunkTimeDuration == 1 && chunckDuration == "Days") || chunkTimeDuration > 1)
                        {
                            DateTimeOffset till_reduced;
                            DateTimeOffset sinceDateTime = DateTimeOffset.Parse(since);
                            if (chunkTimeDuration == 1 && chunckDuration == "Days")
                            {
                                chunckDuration = "Hours";
                                chunkTimeDuration = 24;
                            }
                            chunkTimeDuration = chunkTimeDuration / 2;
                            if (chunckDuration == "Days")
                            {
                                till_reduced = sinceDateTime.AddDays(chunkTimeDuration);
                            }
                            else if (chunckDuration == "Hours")
                            {
                                till_reduced = sinceDateTime.AddHours(chunkTimeDuration);
                            }
                            else
                            {
                                till_reduced = sinceDateTime.AddMinutes(chunkTimeDuration);
                            }
                            if (!string.IsNullOrEmpty(globalTill) && till_reduced > DateTimeOffset.Parse(globalTill))
                            {
                                till_reduced = DateTimeOffset.Parse(globalTill);
                            }


                            till = till_reduced.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
                            return await CheckResourceTypeCount(since, till, resourceType, chunkTimeDuration, chunckDuration);
                        }
                    }

                }
                total = total != null ? (int)total : 0;
                return (int)total;

            }
            catch
            {
                total = total != null ? (int)total : 0;
                return (int)total;
                throw;
            }
        }
        private bool CheckLastCount(int index)
        {
            try
            {
                TableClient chunktableClient = _azureTableClientFactory.Create(_options.ChunkTableName);
                TableEntity qEntityGetResourceIndex = _azureTableMetadataStore.GetEntity(chunktableClient, _options.PartitionKey, _options.RowKey);
                if (qEntityGetResourceIndex?["multiExport"].ToString() == "Running" && (qEntityGetResourceIndex["subTillExportType"].ToString() == qEntityGetResourceIndex["globalTillExportType"].ToString()))
                {
                    return true;
                }
                else if (qEntityGetResourceIndex?["multiExport"].ToString() != "Running" && index == _options.ResourceTypes?.Count() - 1)
                {
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}
