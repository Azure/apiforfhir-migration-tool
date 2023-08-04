// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using FhirMigrationTool.Configuration;
using FhirMigrationTool.FhirOperation;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FhirMigrationTool.SurfaceCheck
{
    public class SurfaceCheck : ISurfaceCheck
    {
        private readonly ILogger _logger;
        private readonly MigrationOptions _options;
        private readonly IFhirClient _fhirClient;
        private readonly TelemetryClient _telemetryClient;

        public SurfaceCheck(IFhirClient fhirClient, MigrationOptions options, TelemetryClient telemetryClient, ILogger<SurfaceCheck> logger)
        {
            _telemetryClient = telemetryClient;
            _options = options;
            _logger = logger;
            _fhirClient = fhirClient;
        }

        public async Task<string> Execute()
        {
            var res = new JObject();
            var passResource = new JArray();
            var errorResource = new JArray();

            if (_options.SurfaceCheckResources != null)
            {
                var surfaceCheckResource = new List<string>(_options.SurfaceCheckResources);
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
                            RequestUri = new Uri(baseUri, string.Format("{0}?_summary=Count", item)),
                        };
                        HttpResponseMessage srcTask = await _fhirClient.Send(request, baseUri, sourceFhirEndpoint);

                        var objResponse = JObject.Parse(srcTask.Content.ReadAsStringAsync().Result);
                        JToken? srctotalCount = objResponse["total"];

                        // Destination count
                        var desrequest = new HttpRequestMessage
                        {
                            Method = HttpMethod.Get,
                            RequestUri = new Uri(desbaseUri, string.Format("{0}?_summary=Count", item)),
                        };

                        HttpResponseMessage desTask = await _fhirClient.Send(desrequest, desbaseUri, destinationFhirEndpoint);

                        var desobjResponse = JObject.Parse(desTask.Content.ReadAsStringAsync().Result);
                        JToken? destotalCount = desobjResponse["total"];

                        // Comparing Count
                        if (destotalCount != null)
                        {
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
