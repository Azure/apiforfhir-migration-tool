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
            JObject res = new JObject();
            JArray passResource = new JArray();
            JArray errorResource = new JArray();

            if (_options.SurfaceCheckResources != null)
            {
                List<string> surfaceCheckResource = new List<string>(_options.SurfaceCheckResources);
                var baseUri = new Uri(_options.SourceFhirUri);
                Uri desbaseUri = new Uri(_options.DestinationFhirUri);
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
                        var srcTask = await _fhirClient.Send(request, baseUri);

                        JObject objResponse = JObject.Parse(srcTask.Content.ReadAsStringAsync().Result);
                        var srctotalCount = objResponse["total"];

                        // Destination count
                        var desrequest = new HttpRequestMessage
                        {
                            Method = HttpMethod.Get,
                            RequestUri = new Uri(desbaseUri, string.Format("{0}?_summary=Count", item)),
                        };

                        var desTask = await _fhirClient.Send(desrequest, desbaseUri, "newToken");

                        JObject desobjResponse = JObject.Parse(desTask.Content.ReadAsStringAsync().Result);
                        var destotalCount = desobjResponse["total"];

                        // Comparing Count
                        if (destotalCount != null)
                        {
                            if (destotalCount.Equals(srctotalCount))
                            {
                                JObject inputFormat = new JObject();
                                inputFormat.Add("Resource", item);
                                inputFormat.Add("SourceCount", srctotalCount);
                                inputFormat.Add("DestinationCount", destotalCount);

                                passResource.Add(inputFormat);
                            }
                            else
                            {
                                JObject errorFormat = new JObject();
                                errorFormat.Add("Resource", item);
                                errorFormat.Add("SourceCount", srctotalCount);
                                errorFormat.Add("DestinationCount", destotalCount);
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
