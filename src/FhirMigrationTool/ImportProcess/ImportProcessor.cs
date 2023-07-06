// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Net;
using System.Text;
using FhirMigrationTool.Configuration;
using FhirMigrationTool.FhirOperation;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Logging;

namespace FhirMigrationTool.ImportProcess
{
    public class ImportProcessor : IImportProcessor
    {
        private readonly ILogger _logger;
        private readonly MigrationOptions _options;
        private readonly IFhirClient _fhirClient;
        private readonly TelemetryClient _telemetryClient;

        public ImportProcessor(IFhirClient fhirClient, MigrationOptions options, TelemetryClient telemetryClient, ILogger<ImportProcessor> logger)
        {
            _logger = logger;
            _options = options;
            _fhirClient = fhirClient;
            _telemetryClient = telemetryClient;
        }

        public async Task<HttpResponseMessage> CallImport(string requestContent)
        {
            HttpResponseMessage importResponse;
            Uri baseUri = _options.DestinationFhirUri;
            string destinationFhirEndpoint = _options.DestinationHttpClient;
            try
            {
                _logger?.LogInformation($"Import Function start");

                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri(baseUri, "/$import"),
                    Headers =
                    {
                        { HttpRequestHeader.Accept.ToString(), "application/fhir+json" },
                        { "Prefer", "respond-async" },
                    },
                    Content = new StringContent(requestContent, Encoding.UTF8, "application/fhir+json"),
                };

                importResponse = await _fhirClient.Send(request, baseUri, destinationFhirEndpoint);
                _logger?.LogInformation($"Import Function completed.");
            }
            catch
            {
                _logger?.LogError($"Error occurred at ImportProcessor:Execute().");
                throw;
            }

            return importResponse;
        }

        public async Task<HttpResponseMessage> CheckImportStatus(string statusUrl)
        {
            var importStatusResponse = new HttpResponseMessage();
            Uri baseUri = _options.DestinationFhirUri;
            string destinationFhirEndpoint = _options.DestinationHttpClient;
            _logger?.LogInformation($"Import Status check started.");

            try
            {
                if (!string.IsNullOrEmpty(statusUrl))
                {
                    var statusRequest = new HttpRequestMessage
                    {
                        Method = HttpMethod.Get,
                        RequestUri = new Uri(statusUrl),
                    };

                    importStatusResponse = await _fhirClient.Send(statusRequest, baseUri, destinationFhirEndpoint);
                    _logger?.LogInformation($"Import Status check completed.");
                }
            }
            catch
            {
                _logger?.LogError($"Error occurred at ImportProcessor:CheckImportStatus().");
                throw;
            }

            return importStatusResponse;
        }
    }
}
