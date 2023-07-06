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

namespace FhirMigrationTool.DeepCheck
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

        public async Task<string> Execute()
        {
            int resourceCount = _options.DeepCheckCount;
            var baseUri = _options.SourceFhirUri;
            var desbaseUri = _options.DestinationFhirUri;

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
                    RequestUri = new Uri(baseUri, string.Format("/?_count={0}", resourceCount)),
                };
                HttpResponseMessage srcTask = await _fhirClient.Send(request, baseUri, sourceFhirEndpoint);

                // var response = srcTask.Result;
                var objResponse = JObject.Parse(srcTask.Content.ReadAsStringAsync().Result);
                JToken? entry = objResponse["entry"];

                if (entry != null)
                {
                    foreach (JToken item in entry)
                    {
                        var gen1Response = (JObject?)item["resource"];
                        if (gen1Response != null)
                        {
                            gen1Response.Remove("meta");

                            // Getting resource from Gen2 server.
                            var desrequest = new HttpRequestMessage
                            {
                                Method = HttpMethod.Get,
                                RequestUri = new Uri(desbaseUri, string.Format("{0}/{1}", gen1Response.GetValue("resourceType"), gen1Response.GetValue("id"))),
                            };

                            HttpResponseMessage desTask = await _fhirClient.Send(desrequest, desbaseUri, destinationFhirEndpoint);

                            // var desResponse = desTask.Result;
                            var gen2Response = JObject.Parse(desTask.Content.ReadAsStringAsync().Result);
                            gen2Response.Remove("meta");

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
