// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Net;
using System.Text;
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
        private readonly IFhirClient _fhirClient;
        private readonly ILogger _logger;

        public ImportStatusOrchestrator(IFhirProcessor importProcessor, MigrationOptions options, IAzureTableClientFactory azureTableClientFactory, IMetadataStore azureTableMetadataStore, IOrchestrationHelper orchestrationHelper, TelemetryClient telemetryClient, IFhirClient fhirClient, ILogger<ImportStatusOrchestrator> logger)
        {
            _importProcessor = importProcessor;
            _options = options;
            _azureTableClientFactory = azureTableClientFactory;
            _azureTableMetadataStore = azureTableMetadataStore;
            _orchestrationHelper = orchestrationHelper;
            _telemetryClient = telemetryClient;
            _fhirClient = fhirClient;
            _logger = logger;
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
            string? resContent = string.Empty;
            var resourceCount = string.Empty;
            var errorImportCount = string.Empty;
            JArray? objOutput = new JArray();
            JArray? objError = new JArray();

            try
            {
                logger.LogInformation("Creating table clients");
                TableClient exportTableClient = _azureTableClientFactory.Create(_options.ExportTableName);
                TableClient chunktableClient = _azureTableClientFactory.Create(_options.ChunkTableName);
                logger.LogInformation("Table clients created successfully.");

                logger.LogInformation(" Query the export table to check for running or started import jobs.");
                Pageable<TableEntity> jobListimportRunning = exportTableClient.Query<TableEntity>(filter: ent => ent.GetString("IsImportRunning") == "Started" || ent.GetString("IsImportRunning") == "Running");
                logger?.LogInformation("Query completed");

                if (jobListimportRunning.Count() > 0)
                {
                    var item = jobListimportRunning.First();
                    while (isComplete == false)
                    {
                        logger?.LogInformation("Retrieving import content location.");
                        statusUrl = item.GetString("importContentLocation");
                        logger?.LogInformation("Import content location retrieved successfully.");

                        logger?.LogInformation("Calling ProcessImportStatusCheck function");
                        ResponseModel response = await context.CallActivityAsync<ResponseModel>(nameof(ProcessImportStatusCheck), statusUrl);
                        logger?.LogInformation("ProcessImportStatusCheck function has completed.");


                        if (response.Status == ResponseStatus.Accepted)
                        {
                            logger?.LogInformation($"Import Status check returned: InProgress.");
                            logger?.LogInformation($"Waiting for next status check for {_options.ScheduleInterval} minutes.");
                            DateTime waitTime = context.CurrentUtcDateTime.Add(TimeSpan.FromMinutes(
                            Convert.ToDouble(_options.ScheduleInterval)));
                            logger?.LogInformation("Successfully retrieved entities from the export table.");
                            TableEntity exportEntity = _azureTableMetadataStore.GetEntity(exportTableClient, _options.PartitionKey, item.RowKey);
                            exportEntity["IsImportRunning"] = "Running";
                            logger?.LogInformation("Updated 'IsImportRunning' status of the entity to 'Running'.");

                            Tuple<Uri, string> source = new Tuple<Uri, string>(_options.SourceUri, _options.SourceHttpClient);
                            Tuple<Uri, string> destination = new Tuple<Uri, string>(_options.DestinationUri, _options.DestinationHttpClient);

                            var azureApiForFhirTotal = await context.CallActivityAsync<Tuple<int?, string>>(nameof(GetTotalFromFhirAsync), source);
                            logger?.LogInformation("Retrieved the total number of resources from the source Azure API For FHIR.");

                            var fhirServiceTotal = await context.CallActivityAsync<Tuple<int?, string>>(nameof(GetTotalFromFhirAsync), destination);
                            logger?.LogInformation("Retrieved the total number of resources from the destination FHIR Service.");


                            if (azureApiForFhirTotal.Item2 != string.Empty)
                            {
                                exportEntity["SourceError"] = azureApiForFhirTotal.Item2.ToString();
                            }
                            else
                            {
                                exportEntity["SourceResourceCount"] = azureApiForFhirTotal.Item1.ToString();
                            }
                            if (fhirServiceTotal.Item2 != string.Empty)
                            {
                                exportEntity["DestinationError"] = fhirServiceTotal.Item2.ToString();
                            }
                            else
                            {
                                exportEntity["DestinationResourceCount"] = fhirServiceTotal.Item1.ToString();
                            }

                            logger?.LogInformation("Starting update of the export table.");
                            _azureTableMetadataStore.UpdateEntity(exportTableClient, exportEntity);
                            logger?.LogInformation("Completed update of the export table.");

                            logger?.LogInformation("Updating logs in Application Insights.");
                            _telemetryClient.TrackEvent(
                                "Import",
                                new Dictionary<string, string>()
                                {
                                        { "ImportId", _orchestrationHelper.GetProcessId(statusUrl) },
                                        { "StatusUrl", statusUrl },
                                        { "ImportStatus", "Running" },
                                        { "SourceResourceCount", azureApiForFhirTotal.Item1.HasValue ? azureApiForFhirTotal.Item1.Value.ToString() : " " },
                                        { "DestinationResourceCount", fhirServiceTotal.Item1.HasValue ? fhirServiceTotal.Item1.Value.ToString() : " " },
                                        { "SourceError", azureApiForFhirTotal.Item2 ?? " " },
                                        { "DestinationError", fhirServiceTotal.Item2 ?? " " },
                                });
                            logger?.LogInformation("Logs updated successfully in Application Insights.");
                            await context.CreateTimer(waitTime, CancellationToken.None);
                        }
                        else if (response.Status == ResponseStatus.Completed)
                        {

                            resContent = response.Content;
                            if (!string.IsNullOrEmpty(resContent))
                            {
                                JObject objResponse = JObject.Parse(resContent);
                                objOutput = objResponse["output"] as JArray;
                                objError = objResponse["error"] as JArray;

                                if (objOutput != null && objOutput.Any())
                                {
                                    resourceCount = _orchestrationHelper.CalculateSumOfResources(objOutput).ToString();
                                    if (objError != null && objError.Any())
                                    {
                                        errorImportCount = _orchestrationHelper.CalculateSumOfResources(objError).ToString();
                                    }
                                }
                            }

                            logger?.LogInformation($"Import Status check returned: Success.");

                            var failedImportType = (objError?.HasValues ?? false) ? string.Join(", ", objError.Select(e => $"{e["type"]?.ToString()}:{e["count"]?.ToString()}")) : string.Empty;

                            TableEntity exportEntity = _azureTableMetadataStore.GetEntity(exportTableClient, _options.PartitionKey, item.RowKey);
                            exportEntity["IsImportComplete"] = true;
                            exportEntity["IsImportRunning"] = "Completed";
                            exportEntity["EndTime"] = DateTime.UtcNow;
                            exportEntity["TotalImportResourceCount"] = resourceCount;
                            exportEntity["TotalFailedImportCount"] = errorImportCount;
                            exportEntity["FailedImportResources"] = failedImportType;

                            Tuple<Uri, string> source = new Tuple<Uri, string>(_options.SourceUri, _options.SourceHttpClient);
                            Tuple<Uri, string> destination = new Tuple<Uri, string>(_options.DestinationUri, _options.DestinationHttpClient);

                            var azureApiForFhirTotal = await context.CallActivityAsync<Tuple<int?, string>>(nameof(GetTotalFromFhirAsync), source);
                            logger?.LogInformation("Retrieved the total number of resources from the source Azure API For FHIR.");
                            var fhirServiceTotal = await context.CallActivityAsync<Tuple<int?, string>>(nameof(GetTotalFromFhirAsync), destination);
                            logger?.LogInformation("Retrieved the total number of resources from the destination FHIR Service.");

                            if (azureApiForFhirTotal.Item2 != string.Empty)
                            {
                                exportEntity["SourceError"] = azureApiForFhirTotal.Item2.ToString();
                            }
                            else
                            {
                                exportEntity["SourceResourceCount"] = azureApiForFhirTotal.Item1.ToString();
                            }
                            if (fhirServiceTotal.Item2 != string.Empty)
                            {
                                exportEntity["DestinationError"] = fhirServiceTotal.Item2.ToString();
                            }
                            else
                            {
                                exportEntity["DestinationResourceCount"] = fhirServiceTotal.Item1.ToString();
                            }
                            logger?.LogInformation("Starting to update the total resource count in the export table.");
                            _azureTableMetadataStore.UpdateEntity(exportTableClient, exportEntity);
                            logger?.LogInformation("Successfully updated the total resource count in the export table.");

                            Pageable<TableEntity> jobListimport = exportTableClient.Query<TableEntity>(filter: ent => ent.GetBoolean("IsExportComplete") == true && ent.GetString("ImportRequest") == "Yes" && ent.GetBoolean("IsProcessed") == false && ent.GetBoolean("IsFirst") == true);
                            if (jobListimport.Count() == 1)
                            {
                                foreach (TableEntity jobImport in jobListimport)
                                {
                                    TableEntity exportEntity1 = _azureTableMetadataStore.GetEntity(exportTableClient, _options.PartitionKey, jobImport.RowKey);
#pragma warning disable CS8629 // Nullable value type may be null.
                                    int payloadCount = (int)jobImport.GetInt32("PayloadCount");
                                    int completeCount = (int)jobImport.GetInt32("CompletedCount");
#pragma warning restore CS8629 // Nullable value type may be null.
                                    completeCount++;
                                    if (payloadCount == completeCount)
                                    {
                                        exportEntity1["IsProcessed"] = true;
                                        if (_options.IsParallel == true)
                                        {
                                            TableEntity qEntitynew = _azureTableMetadataStore.GetEntity(chunktableClient, _options.PartitionKey, _options.RowKey);
                                            qEntitynew["since"] = exportEntity["Till"];
                                            qEntitynew["globalSinceExportType"] = exportEntity["Till"];
                                            qEntitynew["maxExportRetries"] = 0;
                                            logger?.LogInformation("Starting update of the chunk table.");
                                            _azureTableMetadataStore.UpdateEntity(chunktableClient, qEntitynew);
                                            logger?.LogInformation("Completed update of the chunk table.");

                                            logger?.LogInformation("Updating logs in Application Insights.");
                                            _telemetryClient.TrackEvent(
                                            "ImportTill",
                                            new Dictionary<string, string>()
                                            {
                                                { "Till", exportEntity["Till"].ToString() }
                                            });
                                            logger?.LogInformation("Logs updated successfully in Application Insights.");

                                        }
                                        else
                                        {
                                            TableEntity qEntityResourceType = _azureTableMetadataStore.GetEntity(chunktableClient, _options.PartitionKey, _options.RowKey);
                                            if (qEntityResourceType["multiExport"].ToString() != "Running")
                                            {
                                                if ((int)qEntityResourceType["noOfResources"] - 1 == (int)qEntityResourceType["resourceTypeIndex"])
                                                {
                                                    qEntityResourceType = _azureTableMetadataStore.GetEntity(chunktableClient, _options.PartitionKey, _options.RowKey);
                                                    qEntityResourceType["globalSinceExportType"] = qEntityResourceType["globalTillExportType"];
                                                    qEntityResourceType["since"] = qEntityResourceType["globalTillExportType"];
                                                    qEntityResourceType["globalTillExportType"] = "";
                                                    qEntityResourceType["resourceTypeIndex"] = 0; // all the import will done so will reset index
                                                    qEntityResourceType["subSinceExportType"] = "";
                                                    qEntityResourceType["subTillExportType"] = "";
                                                    qEntityResourceType["maxExportRetries"] = 0;
                                                    logger?.LogInformation("Starting update of the chunk table.");
                                                    _azureTableMetadataStore.UpdateEntity(chunktableClient, qEntityResourceType);
                                                    logger?.LogInformation("Completed update of the chunk table.");

                                                    logger?.LogInformation("Updating logs in Application Insights.");
                                                    _telemetryClient.TrackEvent(
                                                    "ImportTill",
                                                    new Dictionary<string, string>()
                                                    {
                                                        {"Till", qEntityResourceType["globalTillExportType"].ToString() }
                                                    });
                                                    logger?.LogInformation("Logs updated successfully in Application Insights.");

                                                }
                                                else
                                                {
                                                    qEntityResourceType["resourceTypeIndex"] = (int)qEntityResourceType["resourceTypeIndex"] + 1; //   import done then increment counter index
                                                    qEntityResourceType["maxExportRetries"] = 0;
                                                    logger?.LogInformation("Starting update of the chunk table.");
                                                    _azureTableMetadataStore.UpdateEntity(chunktableClient, qEntityResourceType);
                                                    logger?.LogInformation("Completed update of the chunk table.");
                                                }
                                            }
                                            else
                                            {
                                                // check for all sub export done or not
                                                TableEntity qEntityResourceTypenew = _azureTableMetadataStore.GetEntity(chunktableClient, _options.PartitionKey, _options.RowKey);
                                                if (qEntityResourceTypenew["subTillExportType"].ToString() == qEntityResourceTypenew["globalTillExportType"].ToString())
                                                {
                                                    if ((int)qEntityResourceTypenew["noOfResources"] - 1 == (int)qEntityResourceTypenew["resourceTypeIndex"])
                                                    {
                                                        //Its last run to reset value and assigning till to since
                                                        qEntityResourceTypenew["globalSinceExportType"] = qEntityResourceTypenew["globalTillExportType"];
                                                        qEntityResourceTypenew["since"] = qEntityResourceTypenew["globalTillExportType"];
                                                        qEntityResourceTypenew["globalTillExportType"] = "";
                                                        qEntityResourceTypenew["resourceTypeIndex"] = 0; // all the import will done so will reset index
                                                        qEntityResourceTypenew["multiExport"] = "";
                                                        qEntityResourceTypenew["subSinceExportType"] = "";
                                                        qEntityResourceTypenew["subTillExportType"] = "";
                                                        qEntityResourceTypenew["maxExportRetries"] = 0;
                                                        logger?.LogInformation("Starting update of the chunk table.");
                                                        _azureTableMetadataStore.UpdateEntity(chunktableClient, qEntityResourceTypenew);
                                                        logger?.LogInformation("Completed update of the chunk table.");


                                                        logger?.LogInformation("Updating logs in Application Insights.");
                                                        _telemetryClient.TrackEvent(
                                                        "ImportTill",
                                                        new Dictionary<string, string>()
                                                        {
                                                        {"Till", qEntityResourceTypenew["globalTillExportType"].ToString() }
                                                        });
                                                        logger?.LogInformation("Logs updated successfully in Application Insights.");

                                                    }
                                                    else
                                                    {
                                                        qEntityResourceTypenew["multiExport"] = ""; // if global and sub till date matches for this all export done for those chunk  and increment the counter
                                                        qEntityResourceTypenew["resourceTypeIndex"] = (int)qEntityResourceTypenew["resourceTypeIndex"] + 1;
                                                        qEntityResourceTypenew["maxExportRetries"] = 0;
                                                        logger?.LogInformation("Starting update of the chunk table.");
                                                        _azureTableMetadataStore.UpdateEntity(chunktableClient, qEntityResourceTypenew);
                                                        logger?.LogInformation("Completed update of the chunk table.");
                                                    }

                                                }
                                                else
                                                {
                                                    // multiexport run and completed sub export then assigining till to since and global till to sub till
                                                    qEntityResourceTypenew["subSinceExportType"] = qEntityResourceTypenew["subTillExportType"];
                                                    qEntityResourceTypenew["subTillExportType"] = qEntityResourceTypenew["globalTillExportType"];
                                                    qEntityResourceTypenew["maxExportRetries"] = 0;
                                                    logger?.LogInformation("Starting update of the chunk table.");
                                                    _azureTableMetadataStore.UpdateEntity(chunktableClient, qEntityResourceTypenew);
                                                    logger?.LogInformation("Completed update of the chunk table.");
                                                }
                                            }

                                        }
                                    }
                                    exportEntity1["CompletedCount"] = completeCount;
                                    logger?.LogInformation("Starting update of the export table.");
                                    _azureTableMetadataStore.UpdateEntity(exportTableClient, exportEntity1);
                                    logger?.LogInformation("Completed update of the export table.");

                                }
                            }
                            logger?.LogInformation("Updating logs in Application Insights.");
                            _telemetryClient.TrackEvent(
                                "Import",
                                new Dictionary<string, string>()
                                {
                                        { "ImportId", _orchestrationHelper.GetProcessId(statusUrl) },
                                        { "StatusUrl", statusUrl },
                                        { "ImportStatus", "Completed" },
                                        { "TotalImportResources", resourceCount },
                                        { "TotalExportResources", item.GetString("TotalExportResourceCount") },
                                        { "SourceResourceCount", azureApiForFhirTotal.Item1.HasValue ? azureApiForFhirTotal.Item1.Value.ToString() : " " },
                                        { "DestinationResourceCount", fhirServiceTotal.Item1.HasValue ? fhirServiceTotal.Item1.Value.ToString() : " " },
                                        { "SourceError", azureApiForFhirTotal.Item2 ?? " " },
                                        { "DestinationError", fhirServiceTotal.Item2 ?? " " },
                                        { "FailedImportCount", errorImportCount },
                                        { "FailedResourceType", failedImportType },
                                        { "SearchParameterCount", item.GetInt64("SearchParameterCount")?.ToString() ?? " " },
                                        { "FailedExportCount", item.GetString("TotalFailedExportCount") },
                                        { "FailedExportType", item.GetString("FailedExports")}
                                });
                            logger?.LogInformation("Logs updated successfully in Application Insights.");
                            isComplete = true;
                        }
                        else
                        {
                            string diagnosticsValue = JObject.Parse(response.Content)?["issue"]?[0]?["diagnostics"]?.ToString() ?? "For more information check Content location.";
                            logger?.LogInformation($"Import Status check returned: Unsuccessful. Reason : {diagnosticsValue}");
                            TableEntity exportEntity = _azureTableMetadataStore.GetEntity(exportTableClient, _options.PartitionKey, item.RowKey);
                            exportEntity["IsImportComplete"] = true;
                            exportEntity["IsImportRunning"] = "Failed";
                            exportEntity["EndTime"] = DateTime.UtcNow;

                            Tuple<Uri, string> source = new Tuple<Uri, string>(_options.SourceUri, _options.SourceHttpClient);
                            Tuple<Uri, string> destination = new Tuple<Uri, string>(_options.DestinationUri, _options.DestinationHttpClient);


                            var azureApiForFhirTotal = await context.CallActivityAsync<Tuple<int?, string>>(nameof(GetTotalFromFhirAsync), source);
                            logger?.LogInformation("Retrieved the total number of resources from the source Azure API For FHIR.");

                            var fhirServiceTotal = await context.CallActivityAsync<Tuple<int?, string>>(nameof(GetTotalFromFhirAsync), destination);
                            logger?.LogInformation("Retrieved the total number of resources from the destination FHIR Service.");


                            if (azureApiForFhirTotal.Item2 != string.Empty)
                            {
                                exportEntity["SourceError"] = azureApiForFhirTotal.Item2.ToString();
                            }
                            else
                            {
                                exportEntity["SourceResourceCount"] = azureApiForFhirTotal.Item1.ToString();
                            }
                            if (fhirServiceTotal.Item2 != string.Empty)
                            {
                                exportEntity["DestinationError"] = fhirServiceTotal.Item2.ToString();
                            }
                            else
                            {
                                exportEntity["DestinationResourceCount"] = fhirServiceTotal.Item1.ToString();
                            }

                            exportEntity["FailureReason"] = diagnosticsValue;
                            logger?.LogInformation("Starting to update the total resource count in the export table.");
                            _azureTableMetadataStore.UpdateEntity(exportTableClient, exportEntity);
                            logger?.LogInformation("Successfully updated the total resource count in the export table.");

                            Pageable<TableEntity> jobListimport = exportTableClient.Query<TableEntity>(filter: ent => ent.GetBoolean("IsExportComplete") == true && ent.GetString("ImportRequest") == "Yes" && ent.GetBoolean("IsProcessed") == false && ent.GetBoolean("IsFirst") == true);
                            if (jobListimport.Count() == 1)
                            {
                                foreach (TableEntity jobImport in jobListimport)
                                {
                                    TableEntity exportEntity1 = _azureTableMetadataStore.GetEntity(exportTableClient, _options.PartitionKey, jobImport.RowKey);
#pragma warning disable CS8629 // Nullable value type may be null.
                                    int payloadCount = (int)jobImport.GetInt32("PayloadCount");
                                    int completeCount = (int)jobImport.GetInt32("CompletedCount");
#pragma warning restore CS8629 // Nullable value type may be null.
                                    completeCount++;
                                    if (payloadCount == completeCount)
                                    {
                                        exportEntity1["IsProcessed"] = true;
                                    }

                                    exportEntity1["CompletedCount"] = completeCount;
                                    logger?.LogInformation("Starting update of the export table.");
                                    _azureTableMetadataStore.UpdateEntity(exportTableClient, exportEntity1);
                                    logger?.LogInformation("Completed update of the export table.");
                                }
                            }

                            TableEntity chunkEntity = _azureTableMetadataStore.GetEntity(chunktableClient, _options.PartitionKey, _options.RowKey);
                            var maxExportRetries = (int)chunkEntity["maxExportRetries"];
                            chunkEntity["maxExportRetries"] = maxExportRetries + 1;

                            logger?.LogInformation("Starting update of the chunk table.");
                            _azureTableMetadataStore.UpdateEntity(chunktableClient, chunkEntity);
                            logger?.LogInformation("Completed update of the chunk table.");

                            isComplete = true;
                            logger?.LogInformation("Updating logs in Application Insights.");
                            _telemetryClient.TrackEvent(
                            "Import",
                            new Dictionary<string, string>()
                            {
                                    { "ImportId", _orchestrationHelper.GetProcessId(statusUrl) },
                                    { "StatusUrl", statusUrl },
                                    { "ImportStatus", "Failed" },
                                    { "SourceResourceCount", azureApiForFhirTotal.Item1.HasValue ? azureApiForFhirTotal.Item1.Value.ToString() : " " },
                                    { "DestinationResourceCount", fhirServiceTotal.Item1.HasValue ? fhirServiceTotal.Item1.Value.ToString() : " " },
                                    { "SourceError", azureApiForFhirTotal.Item2 ?? " " },
                                    { "DestinationError", fhirServiceTotal.Item2 ?? " " },
                                    { "FailureReason", diagnosticsValue}
                            });
                            logger?.LogInformation("Logs updated successfully in Application Insights.");
                            throw new HttpFailureException($"StatusCode: {statusRespose.StatusCode}, Response: {statusRespose.Content.ReadAsStringAsync()} ");
                        }
                    }

                    isComplete = false;
                }
            }
            catch
            {
                throw;
            }
            logger?.LogInformation("Completed import status activities.");
            return "completed";
        }
        [Function(nameof(GetTotalFromFhirAsync))]
        public async Task<Tuple<int?, string>> GetTotalFromFhirAsync([ActivityTrigger] Tuple<Uri, string> tuple)
        {
            try
            {
                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = _options.ExportWithHistory || _options.ExportWithDelete ? new Uri(tuple.Item1, "/_history?_summary=count") : new Uri(tuple.Item1, "?_summary=Count"),
                    Headers =
                    {
                        {
                            HttpRequestHeader.Accept.ToString(), "application/json" },
                        }
                };

                HttpResponseMessage fhirResponse = await _fhirClient.Send(request, tuple.Item1, tuple.Item2);
                if (fhirResponse.IsSuccessStatusCode)
                {
                    var objFhirResponse = JObject.Parse(await fhirResponse.Content.ReadAsStringAsync());
                    int total = objFhirResponse.Value<int>("total");
                    return Tuple.Create<int?, string>(total, string.Empty);

                }
                else
                {
                    var objFhirResponse = JObject.Parse(await fhirResponse.Content.ReadAsStringAsync());
                    string error = objFhirResponse["issue"]?[0]?["diagnostics"]?.ToString() ?? "";
                    return Tuple.Create<int?, string>(null, error);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return Tuple.Create<int?, string>(null, ex.Message);
            }
        }

        [Function(nameof(ProcessImportStatusCheck))]
        public async Task<ResponseModel> ProcessImportStatusCheck([ActivityTrigger] string importStatusUrl, FunctionContext executionContext)
        {
            _logger?.LogInformation("ProcessImportStatusCheck Started");
            try
            {
                if (!string.IsNullOrEmpty(importStatusUrl))
                {
                    ResponseModel importStatusResponse = await _importProcessor.CheckProcessStatus(importStatusUrl, _options.DestinationUri, _options.DestinationHttpClient);
                    _logger?.LogInformation("ProcessImportStatusCheck Finished");
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
