// -------------------------------------------------------------------------------------------------
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

        public ImportOrchestrator(IFhirProcessor importProcessor, MigrationOptions options, IAzureTableClientFactory azureTableClientFactory, IMetadataStore azureTableMetadataStore, IOrchestrationHelper orchestrationHelper, TelemetryClient telemetryClient, IAzureBlobClientFactory azureBlobClientFactory)
        {
            _importProcessor = importProcessor;
            _options = options;
            _azureTableClientFactory = azureTableClientFactory;
            _azureTableMetadataStore = azureTableMetadataStore;
            _orchestrationHelper = orchestrationHelper;
            _telemetryClient = telemetryClient;
            _azureBlobClientFactory = azureBlobClientFactory;
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
                TableClient chunktableClient = _azureTableClientFactory.Create(_options.ChunkTableName);
               
                Pageable<TableEntity> jobListimportRunning = exportTableClient.Query<TableEntity>(filter: ent => ent.GetString("IsImportRunning") == "Started" || ent.GetString("IsImportRunning") == "Running");
                Pageable<TableEntity> jobListimport = exportTableClient.Query<TableEntity>(filter: ent => ent.GetBoolean("IsExportComplete") == true && ent.GetString("ImportRequest") == "Yes" && ent.GetBoolean("IsProcessed") == false && ent.GetBoolean("IsFirst") == true && jobListimportRunning.Count() == 0);


                if (jobListimport.Count() > 0)
                {
                    foreach (TableEntity item in jobListimport)
                    {
                        var importResponse = await context.CallActivityAsync<ResponseModel>(nameof(ProcessImport));

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
            ResponseModel importResponse = new ResponseModel();
            HttpMethod method = HttpMethod.Post;
            try
            {
                TableClient exportTableClient = _azureTableClientFactory.Create(_options.ExportTableName);
                TableClient chunktableClient = _azureTableClientFactory.Create(_options.ChunkTableName);
               
                Pageable<TableEntity> jobListimport = exportTableClient.Query<TableEntity>(filter: ent => ent.GetBoolean("IsExportComplete") == true && ent.GetString("ImportRequest") == "Yes" && ent.GetBoolean("IsProcessed") == false && ent.GetBoolean("IsFirst") == true);

                if (jobListimport != null && jobListimport.Count() == 1)
                {
                    ILogger logger = executionContext.GetLogger(nameof(ProcessImport));
                    logger.LogInformation("Starting import activities.");
                    var item = jobListimport.First();

                    string? statusUrl_new = item.GetString("exportContentLocation");
                    string statusId = GetProcessId(statusUrl_new);
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
                                importResponse = await _importProcessor.CallProcess(method, content, _options.DestinationUri, "/$import", _options.DestinationHttpClient);
                                if (importResponse.Status == ResponseStatus.Accepted)
                                {
                                    logger?.LogInformation($"Import  returned: Success.");
                                   
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
                                            _azureTableMetadataStore.AddEntity(exportTableClient, tableEntity);

                                            TableEntity qEntitynew = _azureTableMetadataStore.GetEntity(chunktableClient, _options.PartitionKey, _options.RowKey);
                                            qEntitynew["ImportId"] = importId++;
                                            _azureTableMetadataStore.UpdateEntity(chunktableClient, qEntitynew);

                                            _telemetryClient.TrackEvent(
                                        "Import",
                                        new Dictionary<string, string>()
                                        {
                                    { "ImportId", _orchestrationHelper.GetProcessId(statusUrl) },
                                    { "StatusUrl", statusUrl },
                                    { "ImportStatus", "Started" },
                                        });

                                        }
                                    }
                                }
                                else
                                {
                                    logger?.LogInformation($"Import  returned: Failure");
                                    Pageable<TableEntity> jobListimport1 = exportTableClient.Query<TableEntity>(filter: ent => ent.GetBoolean("IsExportComplete") == true && ent.GetString("ImportRequest") == "Yes" && ent.GetString("ExportId") == statusId && ent.GetString("IsImportRunning") == "Not Started" && ent.GetBoolean("IsFirst") == true);
                                    if (jobListimport1.Count() == 1)
                                    {
                                        string diagnosticsValue = JObject.Parse(importResponse.Content)?["issue"]?[0]?["diagnostics"]?.ToString() ?? "For more information check Content location.";
                                        logger?.LogInformation($"Import Status check returned: Unsuccessful. Reason : {diagnosticsValue}");
                                        TableEntity exportEntity = _azureTableMetadataStore.GetEntity(exportTableClient, _options.PartitionKey, item.RowKey);
                                        exportEntity["IsImportComplete"] = false;
                                        exportEntity["IsImportRunning"] = "failed";
                                        exportEntity["FailureReason"] = diagnosticsValue;
                                        exportEntity["ImportNo"] = blobItem.Name;
                                        exportEntity["IsProcessed"] = true;
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
                                        

                                    }
                                    else
                                    {
                                        TableEntity qEntity = _azureTableMetadataStore.GetEntity(chunktableClient, _options.PartitionKey, _options.RowKey);
                                        if (qEntity["ImportId"] != null)
                                        {
                                            int importId = (int)qEntity["ImportId"];
                                            string rowKey = _options.RowKey + statusId + importId++;
                                            string diagnosticsValue = JObject.Parse(importResponse.Content)?["issue"]?[0]?["diagnostics"]?.ToString() ?? "For more information check Content location.";
                                            logger?.LogInformation($"Import Status check returned: Unsuccessful. Reason : {diagnosticsValue}");

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
                                            _azureTableMetadataStore.AddEntity(exportTableClient, tableEntity);

                                            TableEntity qEntitynew = _azureTableMetadataStore.GetEntity(chunktableClient, _options.PartitionKey, _options.RowKey);
                                            qEntitynew["ImportId"] = importId++;
                                            _azureTableMetadataStore.UpdateEntity(chunktableClient, qEntitynew);

                                            _telemetryClient.TrackEvent(
                                        "Import",
                                        new Dictionary<string, string>()
                                        {
                                    { "ImportId", _orchestrationHelper.GetProcessId(statusUrl) },
                                    { "StatusUrl", statusUrl },
                                    { "ImportStatus", "Failed" },
                                    { "FailureReason", diagnosticsValue }
                                        });
                                            
                                        }
                                    }
                                }

                            }
                            payloadCounter++;
                            string newContainerName = $"importprocessed-{statusId}";
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
            return importResponse;
        }
        public string GetProcessId(string statusUrl)
        {
            var array = statusUrl.Split('/');
            return array.Last();
        }
    }
}
