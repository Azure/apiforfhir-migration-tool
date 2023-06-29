// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Net;
using System.Net.Http.Headers;
using FhirMigrationTool.Configuration;
using FhirMigrationTool.ExceptionHelper;
using FhirMigrationTool.FhirOperation;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Logging;

namespace FhirMigrationTool.ExportProcess
{
    public class ExportProcessor : IExportProcessor
    {
        private readonly ILogger _logger;
        private readonly MigrationOptions _options;
        private readonly IFhirClient _fhirClient;
        private readonly TelemetryClient _telemetryClient;

        public ExportProcessor(IFhirClient fhirClient, MigrationOptions options, TelemetryClient telemetryClient, ILogger<ExportProcessor> logger)
        {
            _telemetryClient = telemetryClient;
            _options = options;
            _logger = logger;
            _fhirClient = fhirClient;
        }

        public async Task<string> Execute()
        {
            var baseUri = new Uri(_options.SourceFhirUri);
            string sourceFhirEndpoint = _options.SourceHttpClient;
            string exportStatusUrl = string.Empty;
            try
            {
                _logger?.LogInformation($"Export Function start");

                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri(baseUri, "/$export?_type=Patient"),
                    Headers =
                    {
                        { HttpRequestHeader.Accept.ToString(), "application/fhir+json" },
                        { "Prefer", "respond-async" },
                    },
                };

                HttpResponseMessage exportResponse = await _fhirClient.Send(request, baseUri, sourceFhirEndpoint);

                if (exportResponse.IsSuccessStatusCode)
                {
                    _logger?.LogInformation($"Export Function returned: Success.");

                    HttpHeaders headers = exportResponse.Content.Headers;
                    IEnumerable<string> values;

                    if (headers.GetValues("Content-Location") != null)
                    {
                        values = headers.GetValues("Content-Location");
                        exportStatusUrl = values.First();
                    }
                }
                else
                {
                    _logger?.LogInformation($"Export returned: Unsuccessful. StatusCode: {exportResponse.StatusCode}");
                    throw new HttpFailureException($"StatusCode: {exportResponse.StatusCode}, Response: {exportResponse.Content.ReadAsStringAsync()} ");
                }

                _logger?.LogInformation($"Export Function completed.");
            }
            catch
            {
                _logger?.LogError($"Error occurred at ExportProcessor:Execute().");
                throw;
            }

            return exportStatusUrl;
        }

        public async Task<HttpResponseMessage> CheckExportStatus(string statusUrl)
        {
            HttpResponseMessage exportStatusResponse = new HttpResponseMessage();
            var baseUri = new Uri(_options.SourceFhirUri);
            string sourceFhirEndpoint = _options.SourceHttpClient;
            _logger?.LogInformation($"Export Status check started.");

            try
            {
                if (!string.IsNullOrEmpty(statusUrl))
                {
                    var statusRequest = new HttpRequestMessage
                    {
                        Method = HttpMethod.Get,
                        RequestUri = new Uri(statusUrl),
                    };

                    exportStatusResponse = await _fhirClient.Send(statusRequest, baseUri, sourceFhirEndpoint);
                    _logger?.LogInformation($"Export Status check completed.");
                }
            }
            catch
            {
                _logger?.LogError($"Error occurred at ExportProcessor:CheckExportStatus().");
                throw;
            }

            return exportStatusResponse;
        }
    }
}
