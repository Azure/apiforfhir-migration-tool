// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using FhirMigrationTool.Configuration;
using FhirMigrationTool.Security;
using Microsoft.Extensions.Logging;

namespace FhirMigrationTool.FhirOperation
{
    public class FhirClient : IFhirClient
    {
        private readonly MigrationOptions _options;
        private readonly IHttpClientFactory _httpClient;
        private readonly IBearerTokenHelper _tokenCache;
        private readonly ILogger _logger;

        public FhirClient(IHttpClientFactory httpClient, IBearerTokenHelper tokenCache, ILogger<FhirClient> logger, MigrationOptions options)
        {
            _httpClient = httpClient;
            _tokenCache = tokenCache;
            _logger = logger;
            _options = options;
        }

        public async Task<HttpResponseMessage> Send(HttpRequestMessage request, Uri fhirUrl, string nullAccessToken = "")
        {
            HttpResponseMessage fhirResponse;
            try
            {
                HttpClient client = _httpClient == null ? new HttpClient() : _httpClient.CreateClient("FhirServer");
                client.BaseAddress = fhirUrl;

                CancellationToken cancellationToken = new CancellationTokenSource().Token;

                Azure.Core.AccessToken tokenResponse = await _tokenCache.GetTokenAsync(
                    GetDefaultScopes(requestUri: fhirUrl),
                    cancellationToken,
                    nullAccessToken);

                if (!client.DefaultRequestHeaders.Contains("Authorization"))
                {
                    object lockobj = new();

                    lock (lockobj)
                    {
                        client.DefaultRequestHeaders.Clear();
                        client.DefaultRequestHeaders.Accept.Clear();
                        client.DefaultRequestHeaders.Remove("Authorization");
                        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {tokenResponse.Token}");
                    }
                }

                fhirResponse = await client.SendAsync(request);
            }
            catch
            {
                throw;
            }

            return fhirResponse;
        }

        private static string[] GetDefaultScopes(Uri requestUri)
        {
            var baseAddress = requestUri.GetLeftPart(UriPartial.Authority);
            return new string[] { $"{baseAddress.TrimEnd('/')}/.default" };
        }
    }
}
