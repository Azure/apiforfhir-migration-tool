// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using ApiForFhirMigrationTool.Function.Configuration;
using ApiForFhirMigrationTool.Function.FhirOperation;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ApiForFhirMigrationTool.Function.DeepCheck
{
    public class DeepCheck : IDeepCheck
    {
        private readonly ILogger _logger;
        private readonly MigrationOptions _options;
        private readonly IFhirClient _fhirClient;
        private readonly TelemetryClient _telemetryClient;

        public DeepCheck(IFhirClient fhirClient, MigrationOptions options, TelemetryClient telemetryClient, ILogger<DeepCheck> logger)
        {
            _telemetryClient = telemetryClient;
            _options = options;
            _logger = logger;
            _fhirClient = fhirClient;
        }

        public async Task<string> Execute(string query)
        {
            int resourceCount = _options.DeepCheckCount;
            var baseUri = _options.SourceUri;
            var desbaseUri = _options.DestinationUri;

            string sourceFhirEndpoint = _options.SourceHttpClient;
            string destinationFhirEndpoint = _options.DestinationHttpClient;

            var res = new JObject();
            var passResource = new JArray();
            var errorResource = new JArray();

            _logger?.LogInformation($"Deep Check Function start");
            try
            {
                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri(baseUri, string.Format("/{0}{1}", query, resourceCount)),
                };
                HttpResponseMessage srcTask = await _fhirClient.Send(request, baseUri, sourceFhirEndpoint);

                // var response = srcTask.Result;
                var objResponse = JObject.Parse(srcTask.Content.ReadAsStringAsync().Result);
                JToken? entry = objResponse["entry"];
                JObject? gen2Response = null;
                JObject? gen1Response = null;
                if (entry != null)
                {
                    foreach (JToken item in entry)
                    {
                        gen1Response = (JObject?)item["resource"];

                        if (gen1Response != null)
                        {
                            JToken? version = null;
                            var metaToken = gen1Response["meta"];
                            if (metaToken != null && metaToken is JObject metaObject)
                            {
                                metaObject.Remove("lastUpdated");
                                version = metaObject.GetValue("versionId");
                            }
                            if (_options.ExportWithHistory == true || _options.ExportWithDelete == true)
                            {
                                var requestObject = (JObject?)item["request"];
                                if (requestObject != null)
                                {
                                    var methodValue = requestObject.GetValue("method")?.ToString();
                                    if (string.Equals(methodValue, "DELETE", StringComparison.OrdinalIgnoreCase))
                                    {
                                        continue;
                                    }
                                }
                                // Getting resource from Gen2 server.
                                var desrequest = new HttpRequestMessage
                                    {
                                        Method = HttpMethod.Get,
                                        RequestUri = new Uri(desbaseUri, string.Format("{0}/{1}/{2}/{3}", gen1Response.GetValue("resourceType"), gen1Response.GetValue("id"), "_history", version)),
                                    };

                                    HttpResponseMessage desTask = await _fhirClient.Send(desrequest, desbaseUri, destinationFhirEndpoint);

                                    // var desResponse = desTask.Result;
                                    gen2Response = JObject.Parse(desTask.Content.ReadAsStringAsync().Result);
                                    metaToken = gen2Response["meta"];
                                    if (metaToken != null && metaToken is JObject metaObject1)
                                    {
                                        metaObject1.Remove("lastUpdated");
                                    }
                            }
                            else
                            {
                                // Getting resource from Gen2 server.
                                var desrequest = new HttpRequestMessage
                                {
                                    Method = HttpMethod.Get,
                                    RequestUri = new Uri(desbaseUri, string.Format("{0}/{1}", gen1Response.GetValue("resourceType"), gen1Response.GetValue("id"))),
                                };

                                HttpResponseMessage desTask = await _fhirClient.Send(desrequest, desbaseUri, destinationFhirEndpoint);

                                // var desResponse = desTask.Result;
                                gen2Response = JObject.Parse(desTask.Content.ReadAsStringAsync().Result);
                                metaToken = gen2Response["meta"];
                                if (metaToken != null && metaToken is JObject metaObject1)
                                {
                                    metaObject1.Remove("lastUpdated");
                                }

                            }

                            // Comparing the resource from Gen1 and Gen2 server.
                            if (JToken.DeepEquals(gen1Response, gen2Response))
                            {
                                var inputFormat = new JObject
                                {
                                    { "Resource", gen1Response.GetValue("resourceType") },
                                    { "id", gen1Response.GetValue("id") },
                                    { "Compared", true },
                                };

                                passResource.Add(inputFormat);
                            }
                            else
                            {
                                var errorFormat = new JObject
                                {
                                    { "Resource", gen1Response.GetValue("resourceType") },
                                    { "id", gen1Response.GetValue("id") },
                                    { "Compared", false },
                                };
                                errorResource.Add(errorFormat);
                            }

                        }
                    }
                }
            }
            catch
            {
                throw;
            }

            res.Add("ResourceMatched", passResource);
            res.Add("ResourceNotMatched", errorResource);
            var importRequestJson = res.ToString(Formatting.None);

            return importRequestJson;
        }
    }
}
