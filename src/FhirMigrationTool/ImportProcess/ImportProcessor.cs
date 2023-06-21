// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Net;
using System.Net.Http.Headers;
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

        public async Task<string> Execute(string requestContent)
        {
            var baseUri = new Uri(_options.DestinationFhirUri);
            string importStatusUrl = string.Empty;
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
                        { HttpRequestHeader.UserAgent.ToString(), "fhir-migration-tool" },
                    },
                    Content = new StringContent(requestContent, Encoding.UTF8, "application/fhir+json"),
                };

                var importResponse = await _fhirClient.Send(request, baseUri);

                if (importResponse.IsSuccessStatusCode)
                {
                    _logger?.LogInformation($"Import Function returned success.");

                    HttpHeaders headers = importResponse.Content.Headers;
                    IEnumerable<string> values;

                    if (headers.GetValues("Content-Location") != null)
                    {
                        values = headers.GetValues("Content-Location");
                        importStatusUrl = values.First();
                    }
                }
                else
                {
                    _logger?.LogInformation($"Import returned: Unsuccessful.");
                    throw new Exception($"StatusCode: {importResponse.StatusCode}, Response: {importResponse.Content.ReadAsStringAsync()} ");
                }

                _logger?.LogInformation($"Import Function completed.");
            }
            catch
            {
                throw;
            }

            return importStatusUrl;
        }

        public async Task<string> CheckImportStatus(string statusUrl)
        {
            string importStatus = string.Empty;
            var baseUri = new Uri(_options.DestinationFhirUri);
            _logger?.LogInformation($"Import Status check started.");

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
                            Headers =
                            {
                                { HttpRequestHeader.UserAgent.ToString(), "fhir-migration-tool" },
                            },
                        };

                        var importStatusResponse = await _fhirClient.Send(statusRequest, baseUri);

                        if (importStatusResponse.StatusCode == HttpStatusCode.OK)
                        {
                            _logger?.LogInformation($"Import Status check returned success.");
                            importStatus = "Completed";
                            break;
                        }
                        else if (importStatusResponse.StatusCode == HttpStatusCode.Accepted)
                        {
                            _logger?.LogInformation($"Import Status check returned: InProgress.");
                            Thread.Sleep(TimeSpan.FromMinutes(Convert.ToInt32(_options.ScheduleInterval)));
                        }
                        else
                        {
                            _logger?.LogInformation($"Import Status check returned: Unsuccessful.");
                            throw new Exception($"StatusCode: {importStatusResponse.StatusCode}, Response: {importStatusResponse.Content.ReadAsStringAsync()} ");
                        }
                    }

                    _logger?.LogInformation($"Import Status check completed.");
                }
            }
            catch
            {
                throw;
            }

            return importStatus;
        }
    }
}
