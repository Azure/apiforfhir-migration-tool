// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using ApiForFhirMigrationTool.Function.Configuration;
using ApiForFhirMigrationTool.Function.FhirOperation;
using ApiForFhirMigrationTool.Function.Models;
using Azure.Data.Tables;
using Google.Protobuf.WellKnownTypes;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ApiForFhirMigrationTool.Function.SurfaceCheck
{
    public class SurfaceCheck : ISurfaceCheck
    {
        private readonly ILogger _logger;
        private readonly MigrationOptions _options;
        private readonly IFhirClient _fhirClient;
        private readonly TelemetryClient _telemetryClient;
        private readonly IAzureTableClientFactory _azureTableClientFactory;
        private readonly IMetadataStore _azureTableMetadataStore;

        public SurfaceCheck(IFhirClient fhirClient, MigrationOptions options, TelemetryClient telemetryClient, ILogger<SurfaceCheck> logger, IAzureTableClientFactory azureTableClientFactory, IMetadataStore azureTableMetadataStore)
        {
            _telemetryClient = telemetryClient;
            _options = options;
            _logger = logger;
            _fhirClient = fhirClient;
            _azureTableClientFactory = azureTableClientFactory;
            _azureTableMetadataStore = azureTableMetadataStore;
        }

        public async Task<string> Execute(string query)
        {
            var res = new JObject();
            var passResource = new JArray();
            var errorResource = new JArray();

            if (_options.ResourceTypes != null)
            {
                var surfaceCheckResource = new List<string>(_options.ResourceTypes);
                var baseUri = _options.SourceUri;
                var desbaseUri = _options.DestinationUri;
                string sourceFhirEndpoint = _options.SourceHttpClient;
                string destinationFhirEndpoint = _options.DestinationHttpClient;
                foreach (var item in surfaceCheckResource)
                {
                    _logger?.LogInformation($"Surface Check Function start for resource :{item}");
                    try
                    {
                        var request = new HttpRequestMessage
                        {
                            Method = HttpMethod.Get,
                            RequestUri = new Uri(baseUri, string.Format("{0}/{1}", item, query)),
                        };
                        HttpResponseMessage srcTask = await _fhirClient.Send(request, baseUri, sourceFhirEndpoint);

                        var objResponse = JObject.Parse(srcTask.Content.ReadAsStringAsync().Result);
                        JToken? srctotalCount = objResponse["total"];

                        // Destination count
                        var desrequest = new HttpRequestMessage
                        {
                            Method = HttpMethod.Get,
                            RequestUri = new Uri(desbaseUri, string.Format("{0}/{1}", item, query)),
                        };

                        HttpResponseMessage desTask = await _fhirClient.Send(desrequest, desbaseUri, destinationFhirEndpoint);

                        var desobjResponse = JObject.Parse(desTask.Content.ReadAsStringAsync().Result);
                        JToken? destotalCount = desobjResponse["total"];

                        // Comparing Count
                        if (destotalCount != null)
                        {
                            string srcTotal = string.Empty;
                            if (srctotalCount != null)
                            {
                                srcTotal = srctotalCount.ToString();
                            }

                            string destTotal = destotalCount.ToString();
                            if (destotalCount.Equals(srctotalCount))
                            {
                                var inputFormat = new JObject
                                {
                                    { "Resource", item },
                                    { "SourceCount", srctotalCount },
                                    { "DestinationCount", destotalCount },
                                };

                                passResource.Add(inputFormat);
                            }
                            else
                            {
                                var errorFormat = new JObject
                                {
                                    { "Resource", item },
                                    { "SourceCount", srctotalCount },
                                    { "DestinationCount", destotalCount },
                                };
                                errorResource.Add(errorFormat);
                            }

                            _logger?.LogInformation("Creating table clients");
                            TableClient chunktableClient = _azureTableClientFactory.Create(_options.ChunkTableName);
                            TableClient exportTableClient = _azureTableClientFactory.Create(_options.ExportTableName);
                            _logger?.LogInformation("Table clients created successfully.");


                            TableEntity qEntity = _azureTableMetadataStore.GetEntity(chunktableClient, _options.PartitionKey, _options.RowKey);
                            if (qEntity["SurfaceJobId"] != null)
                            {
                                int jobId = (int)qEntity["SurfaceJobId"];
                                string rowKey = _options.SurfaceRowKey + jobId++;

                                var tableEntity = new TableEntity(_options.PartitionKey, rowKey)
                                {
                                     { "Resource", item },
                                    { "SourceCount", srcTotal },
                                    { "DestinationCount", destTotal },
                                    { "Result", destotalCount.Equals(srctotalCount) ? "Pass" : "Fail" },
                                };
                                _logger?.LogInformation("Starting update of the export table.");
                                _azureTableMetadataStore.AddEntity(exportTableClient, tableEntity);
                                _logger?.LogInformation("Completed update of the export table.");

                                TableEntity qEntitynew = _azureTableMetadataStore.GetEntity(chunktableClient, _options.PartitionKey, _options.RowKey);

                                qEntitynew["SurfaceJobId"] = jobId++;

                                _logger?.LogInformation("Starting update of the chunk table.");
                                _azureTableMetadataStore.UpdateEntity(chunktableClient, qEntitynew);
                                _logger?.LogInformation("Completed update of the chunk table.");

                                _logger?.LogInformation("Updating logs in Application Insights.");

                                _telemetryClient.TrackEvent(
                                "SurfaceCheck",
                                new Dictionary<string, string>()
                                {
                                    { "Resource", item },
                                    { "SourceCount", srcTotal },
                                    { "DestinationCount", destTotal },
                                    { "Result", destotalCount.Equals(srctotalCount) ? "Pass" : "Fail" },
                                });
                            }
                        }
                    }
                    catch
                    {
                        throw;
                    }
                }
            }

            res.Add("ResourceMatched", passResource);
            res.Add("ResourceNotMatched", errorResource);
            var importRequestJson = res.ToString(Formatting.None);

            return importRequestJson;
        }
    }
}
