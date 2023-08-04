// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FhirMigrationTool.FhirOperation;
using FhirMigrationTool.Models;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Logging;

namespace FhirMigrationTool.Processors
{
    public class FhirProcessor : IFhirProcessor
    {
        private readonly ILogger<FhirProcessor>? _logger;
        private readonly IFhirClient _fhirClient;
        private readonly TelemetryClient? _telemetryClient;

        public FhirProcessor(IFhirClient fhirClient, TelemetryClient? telemetryClient, ILogger<FhirProcessor>? logger)
        {
            _logger = logger;
            _fhirClient = fhirClient;
            _telemetryClient = telemetryClient;
        }

        public virtual async Task<ResponseModel> CallProcess(HttpMethod method, string requestContent, Uri baseUri, string queryString, string endpoint)
        {
            try
            {
                var request = new HttpRequestMessage
                {
                    Method = method,
                    RequestUri = new Uri(baseUri, queryString),
                    Headers =
                    {
                        { HttpRequestHeader.Accept.ToString(), "application/fhir+json" },
                        { "Prefer", "respond-async" },
                    },
                    Content = new StringContent(requestContent, Encoding.UTF8, "application/fhir+json"),
                };

                HttpResponseMessage fhirResponse = await _fhirClient.Send(request, baseUri, endpoint);
                ResponseModel processResponse = CreateProcessResponse(fhirResponse);
                return processResponse;
            }
            catch
            {
                _logger?.LogError($"Error occurred at FhirProcessor:CallProcess().");
                throw;
            }
        }

        public virtual async Task<ResponseModel> CheckProcessStatus(string statusUrl, Uri baseUri, string endpoint)
        {
            try
            {
                var statusRequest = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri(statusUrl),
                };

                HttpResponseMessage fhirResponse = await _fhirClient.Send(statusRequest, baseUri, endpoint);
                ResponseModel processResponse = CreateStatusResponse(fhirResponse);
                return processResponse;
            }
            catch
            {
                _logger?.LogError($"Error occurred at FhirProcessor:CheckProcessStatus().");
                throw;
            }
        }

        private ResponseModel CreateProcessResponse(HttpResponseMessage fhirResponse)
        {
            var response = new ResponseModel();
            var exportStatusUrl = string.Empty;
            if (fhirResponse.StatusCode == HttpStatusCode.Accepted)
            {
                response.Status = ResponseStatus.Accepted;
                HttpHeaders headers = fhirResponse.Content.Headers;
                IEnumerable<string> values;
                if (headers.GetValues("Content-Location") != null)
                {
                    values = headers.GetValues("Content-Location");
                    exportStatusUrl = values.First();
                }

                response.Content = exportStatusUrl;
                response.Status = ResponseStatus.Completed;
            }

            return response;
        }

        private ResponseModel CreateStatusResponse(HttpResponseMessage fhirResponse)
        {
            var response = new ResponseModel();
            switch (fhirResponse.StatusCode)
            {
                case HttpStatusCode.Accepted:
                    response.Status = ResponseStatus.Accepted;
                    break;
                case HttpStatusCode.OK:
                    response.Content = fhirResponse.Content.ReadAsStringAsync().Result;
                    response.Status = ResponseStatus.Completed;
                    break;
                default:
                    response.Status = ResponseStatus.Failed;
                    break;
            }

            return response;
        }
    }
}
