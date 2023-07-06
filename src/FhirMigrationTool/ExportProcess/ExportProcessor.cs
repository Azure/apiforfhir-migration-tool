// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Net;
using FhirMigrationTool.Configuration;
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

        public async Task<HttpResponseMessage> CallExport()
        {
            HttpResponseMessage exportResponse;
            Uri baseUri = _options.SourceFhirUri;
            string sourceFhirEndpoint = _options.SourceHttpClient;
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

                exportResponse = await _fhirClient.Send(request, baseUri, sourceFhirEndpoint);
                _logger?.LogInformation($"Export Function completed.");
            }
            catch
            {
                _logger?.LogError($"Error occurred at ExportProcessor:Execute().");
                throw;
            }

            return exportResponse;
        }

        public async Task<HttpResponseMessage> CheckExportStatus(string statusUrl)
        {
            var exportStatusResponse = new HttpResponseMessage();
            Uri baseUri = _options.SourceFhirUri;
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
