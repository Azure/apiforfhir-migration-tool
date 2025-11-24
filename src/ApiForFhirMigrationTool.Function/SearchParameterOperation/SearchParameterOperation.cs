// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Net;
using System.Text;
using ApiForFhirMigrationTool.Function.Configuration;
using ApiForFhirMigrationTool.Function.ExceptionHelper;
using ApiForFhirMigrationTool.Function.FhirOperation;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace ApiForFhirMigrationTool.Function.SearchParameterOperation
{
    public class SearchParameterOperation : ISearchParameterOperation
    {
        private readonly ILogger _logger;
        private readonly MigrationOptions _options;
        private readonly IFhirClient _fhirClient;

        public SearchParameterOperation(MigrationOptions options, IFhirClient fhirClient, ILogger<SearchParameterOperation> logger)
        {
            _options = options;
            _logger = logger;
            _fhirClient = fhirClient;
        }

        public async Task<JObject> GetSearchParameters()
        {
            _logger.LogInformation($"GetSearchParameters Started");

            try
            {
                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri(_options.SourceUri, "/SearchParameter?_count=1000"),
                    Headers =
                    {
                        { HttpRequestHeader.Accept.ToString(), "application/json" },
                    },
                    Content = new StringContent(string.Empty, Encoding.UTF8, "application/json"),
                };

                HttpResponseMessage response = await _fhirClient.Send(request, _options.SourceUri, _options.SourceHttpClient);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"GetSearchParameters Finished");
                    return JObject.Parse(response.Content.ReadAsStringAsync().Result);
                }
                else
                {
                    throw new HttpFailureException($"Status: {response.StatusCode} Response: {response.Content.ReadAsStringAsync()} ");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"GetSearchParameters() Exception: {ex.Message}");
                throw;
            }
        }

        public string TransformObject(JObject searchParameterObject)
        {
            _logger.LogInformation($"TransformObject Started");

            try
            {
                searchParameterObject.Remove("meta");
                searchParameterObject.Remove("link");
                searchParameterObject["type"] = "batch";

                JToken? entryToken = searchParameterObject["entry"];

                JArray? entryArray = entryToken?.ToObject<JArray>();

                if (entryArray != null)
                {
                    foreach (JObject resource in entryArray)
                    {
                        resource.Remove("fullUrl");
                        JObject requestObject = new JObject();
#pragma warning disable CS8602 // Dereference of a possibly null reference.
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
                        requestObject["url"] = $"SearchParameter/{(string)resource["resource"]["id"]}";
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                        requestObject["method"] = "POST";
                        resource["request"] = requestObject;
                    }

                    searchParameterObject["entry"] = entryArray;
                }

                _logger.LogInformation($"TransformObject Finished");

                return searchParameterObject.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError($"TransformObject() Exception: {ex.Message}");
                throw;
            }
        }

        public async Task PostSearchParameters(string requestContent)
        {
            _logger.LogInformation($"PostSearchParameters Started");

            try
            {
                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri(_options.DestinationUri.ToString()),
                    Headers =
                    {
                        { HttpRequestHeader.Accept.ToString(), "application/json" },
                    },
                    Content = new StringContent(requestContent, Encoding.UTF8, "application/json"),
                };

                HttpResponseMessage response = await _fhirClient.Send(request, _options.DestinationUri, _options.DestinationHttpClient);

                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpFailureException($"Status: {response.StatusCode} Response: {response.Content.ReadAsStringAsync()} ");
                }

                _logger.LogInformation($"PostSearchParameters Finished");
            }
            catch (Exception ex)
            {
                _logger.LogError($"PostSearchParameters() Exception: {ex.Message}");
                throw;
            }
        }
    }
}
