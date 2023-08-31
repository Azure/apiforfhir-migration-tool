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
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;

namespace ApiForFhirMigrationTool.Function.SearchParameterOperation
{
    public class SearchParameterOperation : ISearchParameterOperation
    {
        private readonly ILogger _logger;
        private readonly MigrationOptions _options;
        private readonly IFhirClient _fhirClient;

        public SearchParameterOperation(IOptions<MigrationOptions> options, IFhirClient fhirClient, ILogger<SearchParameterOperation> logger)
        {
            _options = options.Value;
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
                    RequestUri = new Uri(_options.SourceFhirUri!, "/SearchParameter"),
                    Headers =
                    {
                        { HttpRequestHeader.Accept.ToString(), "application/json" },
                    },
                    Content = new StringContent(string.Empty, Encoding.UTF8, "application/json"),
                };

                HttpResponseMessage response = await _fhirClient.Send(request, _options.SourceFhirUri!, _options.SourceHttpClient);

                if (response.IsSuccessStatusCode)
                {
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
                        if (resource["resource"] == null || resource["resource"]!["id"] == null)
                        {
                            throw new HttpFailureException($"Resource or Resource Id is null");
                        }

                        resource.Remove("fullUrl");
                        JObject requestObject = new JObject();
                        requestObject["url"] = $"SearchParameter/{resource["resource"]!["id"]!}";
                        requestObject["method"] = "PUT";
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
                    RequestUri = _options.TargetFhirUri!,
                    Headers =
                    {
                        { HttpRequestHeader.Accept.ToString(), "application/json" },
                    },
                    Content = new StringContent(requestContent, Encoding.UTF8, "application/json"),
                };

                HttpResponseMessage response = await _fhirClient.Send(request, _options.TargetFhirUri!, _options.DestinationHttpClient);

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
