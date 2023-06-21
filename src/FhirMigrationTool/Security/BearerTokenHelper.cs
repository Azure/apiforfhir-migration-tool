// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using Azure.Core;
using Azure.Identity;
using FhirMigrationTool.Configuration;

namespace FhirMigrationTool.Security
{
    public class BearerTokenHelper : IBearerTokenHelper
    {
        private readonly SemaphoreSlim _semaphoreSlim = new(1, 1);
        private AccessToken? _accessToken = null;
        private DateTimeOffset _accessTokenExpiration;
        private readonly MigrationOptions _options;

        public BearerTokenHelper(MigrationOptions options)
        {
            _options = options;
        }

        public async Task<AccessToken> GetTokenAsync(string[] scopes, CancellationToken cancellationToken, string nullAccessToken = "")
        {
            if (!string.IsNullOrEmpty(nullAccessToken))
            {
                _accessToken = null;
            }

            await _semaphoreSlim.WaitAsync(cancellationToken).ConfigureAwait(false);
            var tokenRefreshOffset = TimeSpan.FromMinutes(5);
            var tokenRefreshRetryDelay = TimeSpan.FromSeconds(30);
            TokenCredential tokenCredential = new DefaultAzureCredential();

            try
            {
                if (_accessToken is null || _accessTokenExpiration <= DateTimeOffset.UtcNow + tokenRefreshOffset)
                {
                    try
                    {
                        _accessToken = await tokenCredential.GetTokenAsync(new TokenRequestContext(scopes), cancellationToken).ConfigureAwait(false);
                        _accessTokenExpiration = _accessToken.Value.ExpiresOn;
                    }
                    catch (AuthenticationFailedException)
                    {
                        // If the token acquisition fails, retry after the delay.
                        await Task.Delay(tokenRefreshRetryDelay, cancellationToken).ConfigureAwait(false);
                        _accessToken = await tokenCredential.GetTokenAsync(new TokenRequestContext(scopes), cancellationToken).ConfigureAwait(false);
                        _accessTokenExpiration = _accessToken.Value.ExpiresOn;
                    }
                    catch
                    {
                        throw;
                    }
                }

                return _accessToken.Value;
            }
            finally
            {
                _semaphoreSlim.Release();
            }
        }
    }
}
