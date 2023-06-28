// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Net;
using System.Net.Http.Headers;
using FhirMigrationTool.Configuration;
using FhirMigrationTool.FhirOperation;
using FhirMigrationTool.OrchestrationHelper;
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
        private readonly IOrchestrationHelper _orchestrationHelper;

        public ExportProcessor(IFhirClient fhirClient, MigrationOptions options, TelemetryClient telemetryClient, ILogger<ExportProcessor> logger, IOrchestrationHelper orchestrationHelper)
        {
            _telemetryClient = telemetryClient;
            _options = options;
            _logger = logger;
            _fhirClient = fhirClient;
            _orchestrationHelper = orchestrationHelper;
        }

        public async Task<string> Execute()
        {
            if (!_orchestrationHelper.ValidateConfig(_options))
            {
                throw new Exception($"Process exiting: Please check all the required configuration values are available.");
            }

            var baseUri = new Uri(_options.SourceFhirUri);
            string sourceFhirEndpoint = _options.SourceHttpClient;
            string exportStatusUrl = string.Empty;
            try
            {
                _logger?.LogInformation($"Export Function start");

                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri(baseUri, "/$export"),
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
                    throw new Exception($"StatusCode: {exportResponse.StatusCode}, Response: {exportResponse.Content.ReadAsStringAsync()} ");
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

        public async Task<string> CheckExportStatus(string statusUrl)
        {
            string import_body = string.Empty;
            var baseUri = new Uri(_options.SourceFhirUri);

            string sourceFhirEndpoint = _options.SourceHttpClient;

            _logger?.LogInformation($"Export Status check started.");

            try
            {
                if (!string.IsNullOrEmpty(statusUrl))
                {
                    while (true)
                    {
                        var statusRequest = new HttpRequestMessage
                        {
                            Method = HttpMethod.Get,
                            RequestUri = new Uri(statusUrl),
                        };

                        HttpResponseMessage exportStatusResponse = await _fhirClient.Send(statusRequest, baseUri, sourceFhirEndpoint);

                        if (exportStatusResponse.StatusCode == HttpStatusCode.OK)
                        {
                            _logger?.LogInformation($"Export Status check returned: Success.");
                            import_body = _orchestrationHelper.CreateImportRequest(exportStatusResponse, _options.ImportMode);
                            break;
                        }
                        else if (exportStatusResponse.StatusCode == HttpStatusCode.Accepted)
                        {
                            _logger?.LogInformation($"Export Status check returned: InProgress.");
                            Thread.Sleep(TimeSpan.FromMinutes(Convert.ToInt32(_options.ScheduleInterval)));
                        }
                        else
                        {
                            _logger?.LogInformation($"Export Status check returned: Unsuccessful.");
                            throw new Exception($"StatusCode: {exportStatusResponse.StatusCode}, Response: {exportStatusResponse.Content.ReadAsStringAsync()} ");
                        }
                    }

                    _logger?.LogInformation($"Export Status check completed.");
                }
            }
            catch
            {
                _logger?.LogError($"Error occurred at ExportProcessor:CheckExportStatus().");
                throw;
            }

            return import_body;
        }
    }
}
