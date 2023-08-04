// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Net;
using System.Text;
using FhirMigrationToolE2E.Configuration;
using FhirMigrationToolE2E.FhirOperation;
using FhirMigrationToolE2E.OrchestrationHelper;
using FhirMigrationToolE2E.Processors;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace FhirMigrationToolE2E.E2E
{
    public class E2ETest
    {
        private readonly IFhirProcessor _exportProcessor;
        private readonly MigrationOptions _options;
        private readonly ILogger _logger;
        private readonly IOrchestrationHelper _orchestrationHelper;
        private readonly IFhirClient _fhirClient;

        public E2ETest(IFhirProcessor exportProcessor, MigrationOptions options, ILogger logger, IOrchestrationHelper orchestrationHelper, IFhirClient fhirClient)
        {
            _exportProcessor = exportProcessor;
            _options = options;
            _logger = logger;
            _orchestrationHelper = orchestrationHelper;
            _fhirClient = fhirClient;
        }

        [Function(nameof(E2ETestActivity))]
        public async Task<string> E2ETestActivity([ActivityTrigger] int count)
        {
            try
            {
                var json = string.Empty;
                if (count == 2)
                {
                    json = await File.ReadAllTextAsync("../../../E2E/Gen1_import_body.json");
                }
                else
                {
                    json = await File.ReadAllTextAsync("../../../E2E/Gen1_import_body_2.json");
                }

                var method = HttpMethod.Post;
                var request = new HttpRequestMessage
                {
                    Method = method,
                    RequestUri = new Uri(_options.SourceUri, string.Empty),
                    Headers =
                    {
                        { HttpRequestHeader.Accept.ToString(), "application/json" },
                    },
                    Content = new StringContent(json, Encoding.UTF8, "application/fhir+json"),
                };

                HttpResponseMessage fhirResponse = await _fhirClient.Send(request, _options.SourceUri, _options.SourceHttpClient);

                // ResponseModel response = await _exportProcessor.CallProcess(method, json, _options.SourceUri, string.Empty, _options.SourceHttpClient);
                if (fhirResponse.StatusCode == HttpStatusCode.OK)
                {
                    return fhirResponse.Content.ReadAsStringAsync().Result;
                }
                else
                {
                    return "failed";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error occurred during test: {ex.Message}");
                return "failed";
            }
        }
    }
}
