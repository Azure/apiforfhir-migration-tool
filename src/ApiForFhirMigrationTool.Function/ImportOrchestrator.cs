﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Text;
using ApiForFhirMigrationTool.Function.Configuration;
using ApiForFhirMigrationTool.Function.ExceptionHelper;
using ApiForFhirMigrationTool.Function.Models;
using ApiForFhirMigrationTool.Function.OrchestrationHelper;
using ApiForFhirMigrationTool.Function.Processors;
using Azure;
using Azure.Core;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.ApplicationInsights;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
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
        private readonly IAzureBlobClientFactory _azureBlobClientFactory;
        private readonly ILogger _logger;

        public ImportOrchestrator(IFhirProcessor importProcessor, MigrationOptions options, IAzureTableClientFactory azureTableClientFactory, IMetadataStore azureTableMetadataStore, IOrchestrationHelper orchestrationHelper, TelemetryClient telemetryClient, IAzureBlobClientFactory azureBlobClientFactory, ILogger<ImportOrchestrator> logger)
        {
            _importProcessor = importProcessor;
            _options = options;
            _azureTableClientFactory = azureTableClientFactory;
            _azureTableMetadataStore = azureTableMetadataStore;
            _orchestrationHelper = orchestrationHelper;
            _telemetryClient = telemetryClient;
            _azureBlobClientFactory = azureBlobClientFactory;
            _logger = logger;
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
                logger.LogInformation("Creating table clients");
                TableClient exportTableClient = _azureTableClientFactory.Create(_options.ExportTableName);
                TableClient chunktableClient = _azureTableClientFactory.Create(_options.ChunkTableName);
                logger.LogInformation("Table clients created successfully.");

                logger.LogInformation(" Query the export table to check for running or started import jobs.");
                Pageable<TableEntity> jobListimportRunning = exportTableClient.Query<TableEntity>(filter: ent => ent.GetString("IsImportRunning") == "Started" || ent.GetString("IsImportRunning") == "Running");
                
                Pageable<TableEntity> jobListimport = exportTableClient.Query<TableEntity>(filter: ent => ent.GetBoolean("IsExportComplete") == true && ent.GetString("ImportRequest") == "Yes" && ent.GetBoolean("IsProcessed") == false && ent.GetBoolean("IsFirst") == true && jobListimportRunning.Count() == 0);
                logger?.LogInformation("Query completed");

                if (jobListimport.Count() > 0)
                {
                    foreach (TableEntity item in jobListimport)
                    {
                        logger?.LogInformation("Calling ProcessImport function");
                        var importResponse = await context.CallActivityAsync<ResponseModel>(nameof(ProcessImport));
                        logger?.LogInformation("ProcessImport function has completed.");

                    }
                }
                else
                {
                    logger?.LogInformation("Currently, an import or export job is already running, so a new import cannot be started.");
                }
            }
            catch
            {
                throw;
            }
            logger?.LogInformation("Completed import activities.");
            return "completed";
        }

        [Function(nameof(ProcessImport))]
        public async Task<ResponseModel> ProcessImport([ActivityTrigger] string requestContent, FunctionContext executionContext)
        {
            _logger?.LogInformation("Import process Started");
            ResponseModel importResponse = new ResponseModel();
            HttpMethod method = HttpMethod.Post;
            try
            {
                _logger?.LogInformation("Creating table clients");
                TableClient exportTableClient = _azureTableClientFactory.Create(_options.ExportTableName);
                TableClient chunktableClient = _azureTableClientFactory.Create(_options.ChunkTableName);
                _logger?.LogInformation("Table clients created successfully.");

                _logger?.LogInformation("Querying the export table to check for completed export jobs.");
                Pageable<TableEntity> jobListimport = exportTableClient.Query<TableEntity>(filter: ent => ent.GetBoolean("IsExportComplete") == true && ent.GetString("ImportRequest") == "Yes" && ent.GetBoolean("IsProcessed") == false && ent.GetBoolean("IsFirst") == true);
                _logger?.LogInformation("Query completed");

                if (jobListimport != null && jobListimport.Count() == 1)
                { 
                    _logger?.LogInformation("Starting import activities.");
                    var item = jobListimport.First();

                    _logger?.LogInformation("Retrieving export content location.");
                    string? statusUrl_new = item.GetString("exportContentLocation");
                    _logger?.LogInformation("Export content location retrieved successfully.");

                    _logger?.LogInformation("Retrieving export process Id from export content location.");
                    string statusId = GetProcessId(statusUrl_new);
                    _logger?.LogInformation("Export process Id retrieved successfully.");

                    string containerName = $"import-{statusId}";
                    BlobContainerClient containerClient = _azureBlobClientFactory.GetBlobContainerClient(containerName);
                    int blobCount = containerClient.GetBlobs().Count();
                    int payloadCounter = 0;
                    foreach (BlobItem blobItem in containerClient.GetBlobs())
                    {
                        if (payloadCounter < _options.PayloadCount && containerClient.GetBlobs().Count() > 0)
                        {
                            BlobClient blobClient = containerClient.GetBlobClient(blobItem.Name);
                            BlobDownloadInfo download = blobClient.Download();

                            using (var streamReader = new StreamReader(download.Content))
                            {
                                string content = await streamReader.ReadToEndAsync();
                                string statusUrl = String.Empty;

                                method = HttpMethod.Post;
                                _logger?.LogInformation($"Retrieving the import payload from '{containerName}' and posting it to the FHIR service.");
                                importResponse = await _importProcessor.CallProcess(method, content, _options.DestinationUri, "/$import", _options.DestinationHttpClient);
                                _logger?.LogInformation("Successfully posted the import payload to the FHIR service.");

                                if (importResponse.Status == ResponseStatus.Accepted)
                                {
                                    _logger?.LogInformation($"Import  returned: Success.");
                                   
                                    Pageable<TableEntity> jobListimport1 = exportTableClient.Query<TableEntity>(filter: ent => ent.GetBoolean("IsExportComplete") == true && ent.GetString("ImportRequest") == "Yes" && ent.GetString("ExportId") == statusId && ent.GetString("IsImportRunning") == "Not Started" && ent.GetBoolean("IsFirst") == true);
                                    if (jobListimport1.Count() == 1)
                                    {
                                        statusUrl = importResponse.Content;
                                        TableEntity exportEntity = _azureTableMetadataStore.GetEntity(exportTableClient, _options.PartitionKey, item.RowKey);
                                        exportEntity["IsImportComplete"] = false;
                                        exportEntity["IsImportRunning"] = "Started";
                                        exportEntity["importContentLocation"] = importResponse.Content;
                                        exportEntity["ImportStartTime"] = DateTime.UtcNow;
                                        exportEntity["ImportNo"] = blobItem.Name;

                                        _logger?.LogInformation("Starting update of the export table.");
                                        _azureTableMetadataStore.UpdateEntity(exportTableClient, exportEntity);
                                        _logger?.LogInformation("Completed update of the export table.");

                                        _logger?.LogInformation("Updating logs in Application Insights.");
                                        _telemetryClient.TrackEvent(
                                        "Import",
                                        new Dictionary<string, string>()
                                        {
                                            { "ImportId", _orchestrationHelper.GetProcessId(statusUrl) },
                                            { "StatusUrl", statusUrl },
                                            { "ImportStatus", "Started" },
                                        });
                                        _logger?.LogInformation("Logs updated successfully in Application Insights.");
                                    }
                                    else
                                    {
                                        TableEntity qEntity = _azureTableMetadataStore.GetEntity(chunktableClient, _options.PartitionKey, _options.RowKey);
                                        if (qEntity["ImportId"] != null)
                                        {
                                            int importId = (int)qEntity["ImportId"];
                                            string rowKey = _options.RowKey + statusId + importId++;

                                            var tableEntity = new TableEntity(_options.PartitionKey, rowKey)
                                            {
                                                { "exportContentLocation", item.GetString("exportContentLocation") },
                                                { "importContentLocation", importResponse.Content },
                                                { "IsExportComplete", true },
                                                { "IsExportRunning", "Completed" },
                                                { "IsImportComplete", false },
                                                { "IsImportRunning", "Started" },
                                                { "ImportRequest", "Yes" },
                                                { "Since",item.GetString("Since") },
                                                { "Till", item.GetString("Till") },
                                                { "StartTime", item.GetDateTime("StartTime") },
                                                {"TotalExportResourceCount",item.GetString("TotalExportResourceCount") },
                                                { "ImportStartTime", DateTime.UtcNow },
                                                {"ExportEndTime",item.GetDateTime("ExportEndTime")  },
                                                { "ExportId",  statusId },
                                                { "ImportNo",blobItem.Name},
                                            };
                                            _logger?.LogInformation("Starting update of the export table.");
                                            _azureTableMetadataStore.AddEntity(exportTableClient, tableEntity);
                                            _logger?.LogInformation("Completed update of the export table.");

                                            TableEntity qEntitynew = _azureTableMetadataStore.GetEntity(chunktableClient, _options.PartitionKey, _options.RowKey);
                                            qEntitynew["ImportId"] = importId++;

                                            _logger?.LogInformation("Starting update of the chunk table.");
                                            _azureTableMetadataStore.UpdateEntity(chunktableClient, qEntitynew);
                                            _logger?.LogInformation("Completed update of the chunk table.");

                                            _logger?.LogInformation("Updating logs in Application Insights.");
                                            _telemetryClient.TrackEvent(
                                        "Import",
                                            new Dictionary<string, string>()
                                            {
                                                { "ImportId", _orchestrationHelper.GetProcessId(statusUrl) },
                                                { "StatusUrl", statusUrl },
                                                { "ImportStatus", "Started" },
                                                });
                                            _logger?.LogInformation("Logs updated successfully in Application Insights.");
                                        }
                                        
                                    }
                                }
                                else
                                {
                                    _logger?.LogInformation($"Import  returned: Failure");
                                    Pageable<TableEntity> jobListimport1 = exportTableClient.Query<TableEntity>(filter: ent => ent.GetBoolean("IsExportComplete") == true && ent.GetString("ImportRequest") == "Yes" && ent.GetString("ExportId") == statusId && ent.GetString("IsImportRunning") == "Not Started" && ent.GetBoolean("IsFirst") == true);
                                    if (jobListimport1.Count() == 1)
                                    {
                                        string diagnosticsValue = JObject.Parse(importResponse.Content)?["issue"]?[0]?["diagnostics"]?.ToString() ?? "For more information check Content location.";
                                        _logger?.LogInformation($"Import Status check returned: Unsuccessful. Reason : {diagnosticsValue}");
                                        TableEntity exportEntity = _azureTableMetadataStore.GetEntity(exportTableClient, _options.PartitionKey, item.RowKey);
                                        exportEntity["IsImportComplete"] = false;
                                        exportEntity["IsImportRunning"] = "failed";
                                        exportEntity["FailureReason"] = diagnosticsValue;
                                        exportEntity["ImportNo"] = blobItem.Name;
                                        exportEntity["IsProcessed"] = true;

                                        _logger?.LogInformation("Starting update of the export table.");
                                        _azureTableMetadataStore.UpdateEntity(exportTableClient, exportEntity);
                                        _logger?.LogInformation("Completed update of the export table.");

                                        _logger?.LogInformation("Updating logs in Application Insights.");
                                        _telemetryClient.TrackEvent(
                                        "Import",
                                        new Dictionary<string, string>()
                                        {
                                            { "ImportId", _orchestrationHelper.GetProcessId(statusUrl) },
                                            { "StatusUrl", statusUrl },
                                            { "ImportStatus", "Failed" },
                                            { "FailureReason", diagnosticsValue }
                                        });
                                        _logger?.LogInformation("Logs updated successfully in Application Insights.");

                                    }
                                    else
                                    {
                                        _logger?.LogInformation($"Import  returned: Failure");
                                        TableEntity qEntity = _azureTableMetadataStore.GetEntity(chunktableClient, _options.PartitionKey, _options.RowKey);
                                        if (qEntity["ImportId"] != null)
                                        {
                                            int importId = (int)qEntity["ImportId"];
                                            string rowKey = _options.RowKey + statusId + importId++;
                                            string diagnosticsValue = JObject.Parse(importResponse.Content)?["issue"]?[0]?["diagnostics"]?.ToString() ?? "For more information check Content location.";
                                            _logger?.LogInformation($"Import Status check returned: Unsuccessful. Reason : {diagnosticsValue}");

                                            var tableEntity = new TableEntity(_options.PartitionKey, rowKey)
                                            {
                                                { "exportContentLocation", item.GetString("exportContentLocation") },
                                                { "IsExportComplete", true },
                                                { "IsExportRunning", "Completed" },
                                                { "IsImportComplete", false },
                                                { "IsImportRunning", "failed" },
                                                { "FailureReason",diagnosticsValue},
                                                { "ImportRequest", "Yes" },
                                                { "Since",item.GetString("Since") },
                                                { "Till", item.GetString("Till") },
                                                { "StartTime", item.GetDateTime("StartTime") },
                                                {"TotalExportResourceCount",item.GetString("TotalExportResourceCount") },
                                                {"ExportEndTime",item.GetDateTime("ExportEndTime")  },
                                                { "ExportId",  statusId },
                                                { "ImportNo",blobItem.Name},
                                                { "IsProcessed",true }

                                            };
                                            _logger?.LogInformation("Starting update of the export table.");
                                            _azureTableMetadataStore.AddEntity(exportTableClient, tableEntity);
                                            _logger?.LogInformation("Completed update of the export table.");

                                            TableEntity qEntitynew = _azureTableMetadataStore.GetEntity(chunktableClient, _options.PartitionKey, _options.RowKey);
                                            qEntitynew["ImportId"] = importId++;

                                            _logger?.LogInformation("Starting update of the chunk table.");
                                            _azureTableMetadataStore.UpdateEntity(chunktableClient, qEntitynew);
                                            _logger?.LogInformation("Completed update of the chunk table.");

                                            _logger?.LogInformation("Updating logs in Application Insights.");
                                            _telemetryClient.TrackEvent(
                                        "Import",
                                            new Dictionary<string, string>()
                                            {
                                                { "ImportId", _orchestrationHelper.GetProcessId(statusUrl) },
                                                { "StatusUrl", statusUrl },
                                                { "ImportStatus", "Failed" },
                                                { "FailureReason", diagnosticsValue }
                                            });
                                            _logger?.LogInformation("Logs updated successfully in Application Insights.");

                                        }
                                    }
                                }

                            }
                            payloadCounter++;
                            string newContainerName = $"importprocessed-{statusId}";
                            _logger?.LogInformation($"Created container '{newContainerName}' for storing processed import payloads.");
                            BlobContainerClient newContainerClient = _azureBlobClientFactory.GetBlobContainerClient(newContainerName);
                            await newContainerClient.CreateIfNotExistsAsync();

                            string newBlobName = $"{blobItem.Name}";
                            BlobClient newBlobClient = newContainerClient.GetBlobClient(newBlobName);
                            BlobDownloadInfo download1 = await blobClient.DownloadAsync();
                            using (StreamReader reader = new StreamReader(download1.Content))
                            {
                                string content = await reader.ReadToEndAsync();
                                string jsonContent = content.ToString();
                                using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonContent)))
                                {
                                    await newBlobClient.UploadAsync(stream);
                                }
                            }
                            _logger?.LogInformation($"Successfully stored processed import payloads in container '{newContainerName}'.");
                            await blobClient.DeleteIfExistsAsync();
                        }

                    }
                }
                else
                {
                    importResponse = await _importProcessor.CallProcess(method, requestContent, _options.DestinationUri, "/$import", _options.DestinationHttpClient);
                }
            }

            catch
            {
                throw;
            }
            _logger?.LogInformation($"Import process Finished");
            return importResponse;
        }
        public string GetProcessId(string statusUrl)
        {
            var array = statusUrl.Split('/');
            return array.Last();
        }
    }
}
