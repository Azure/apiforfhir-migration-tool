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
            var baseUri = new Uri(_options.SourceFhirUri);
            Uri desbaseUri = new Uri(_options.DestinationFhirUri);

            JObject res = new JObject();
            JArray passResource = new JArray();
            JArray errorResource = new JArray();

            _logger?.LogInformation($"Deep Check Function start");
            try
            {
                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri(baseUri, string.Format("/?_count={0}", resourceCount)),
                };
                var srcTask = await _fhirClient.Send(request, baseUri);

                // var response = srcTask.Result;
                JObject objResponse = JObject.Parse(srcTask.Content.ReadAsStringAsync().Result);
                var entry = objResponse["entry"];

                if (entry != null)
                {
                    foreach (var item in entry)
                    {
                        JObject? gen1Response = (JObject?)item["resource"];
                        if (gen1Response != null)
                        {
                            gen1Response.Remove("meta");

                            // Getting resource from Gen2 server.
                            var desrequest = new HttpRequestMessage
                            {
                                Method = HttpMethod.Get,
                                RequestUri = new Uri(desbaseUri, string.Format("{0}/{1}", gen1Response.GetValue("resourceType"), gen1Response.GetValue("id"))),
                            };

                            var desTask = await _fhirClient.Send(desrequest, desbaseUri, "newToken");

                            // var desResponse = desTask.Result;
                            JObject gen2Response = JObject.Parse(desTask.Content.ReadAsStringAsync().Result);
                            gen2Response.Remove("meta");

                            // Comparing the resource from Gen1 and Gen2 server.
                            if (JToken.DeepEquals(gen1Response, gen2Response))
                            {
                                JObject inputFormat = new JObject();
                                inputFormat.Add("Resource", gen1Response.GetValue("resourceType"));
                                inputFormat.Add("id", gen1Response.GetValue("id"));
                                inputFormat.Add("Compared", true);

                                passResource.Add(inputFormat);
                            }
                            else
                            {
                                JObject errorFormat = new JObject();
                                errorFormat.Add("Resource", gen1Response.GetValue("resourceType"));
                                errorFormat.Add("id", gen1Response.GetValue("id"));
                                errorFormat.Add("Compared", false);
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
