// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using Azure.Core;
using Azure.Identity;
using EnsureThat;

namespace FhirMigrationTool.Security
{
    public class BearerTokenHelperClass
    {
        private readonly string[] _scopes;
        private readonly AccessTokenCache _accessTokenCache;

        /// <summary>
        /// Creates bearer token helper with default token cache settings.
        /// </summary>
        /// <param name="tokenCredential">Credential used to create tokens/</param>
        /// <param name="baseAddress">Base address for the client using the credential. Used for resource based scoping via {{baseAddress}}/.default</param>
        /// <param name="scopes">Optional scopes if you want to override the `.default` resource scope.</param>
        public BearerTokenHelperClass(TokenCredential tokenCredential, Uri baseAddress)
        {
            EnsureArg.IsNotNull(tokenCredential, nameof(tokenCredential));
            EnsureArg.IsNotNull(baseAddress);
            _scopes = GetDefaultScopes(baseAddress);
            _accessTokenCache = new AccessTokenCache(tokenCredential, TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(30));
        }

        /// <summary>
        /// Sends the request with the bearer token header.
        /// </summary>
        /// <param name="request">Incoming request message.</param>
        /// <param name="cancellationToken">Incoming cancellation token.</param>
        /// <returns>Response message from request.</returns>
        public async Task<string> GetBearerToken(Uri baseAddress, CancellationToken cancellationToken)
        {
            if (baseAddress.Scheme != Uri.UriSchemeHttps && baseAddress.Host != "localhost")
            {
                throw new InvalidOperationException("Bearer token authentication is not permitted for non TLS protected (https) endpoints.");
            }

            var scopes = _scopes;
            if (scopes is null or { Length: 0 })
            {
                scopes = GetDefaultScopes(baseAddress);
            }

            AccessToken cachedToken = await _accessTokenCache.GetTokenAsync(scopes, cancellationToken).ConfigureAwait(false);
            return cachedToken.Token;
        }

        private static string[] GetDefaultScopes(Uri requestUri)
        {
            var baseAddress = requestUri.GetLeftPart(UriPartial.Authority);
            return new string[] { $"{baseAddress.TrimEnd('/')}/.default" };
        }

        private class AccessTokenCache
        {
            private readonly SemaphoreSlim _semaphoreSlim = new(1, 1);
            private readonly TokenCredential _tokenCredential;
            private readonly TimeSpan _tokenRefreshOffset;
            private readonly TimeSpan _tokenRefreshRetryDelay;
            private AccessToken? _accessToken = null;
            private DateTimeOffset _accessTokenExpiration;

            public AccessTokenCache(
                           TokenCredential tokenCredential,
                           TimeSpan tokenRefreshOffset,
                           TimeSpan tokenRefreshRetryDelay)
            {
                _tokenCredential = tokenCredential;
                _tokenRefreshOffset = tokenRefreshOffset;
                _tokenRefreshRetryDelay = tokenRefreshRetryDelay;
            }

            public async Task<AccessToken> GetTokenAsync(string[] scopes, CancellationToken cancellationToken)
            {
                await _semaphoreSlim.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    if (_accessToken is null || _accessTokenExpiration <= DateTimeOffset.UtcNow + _tokenRefreshOffset)
                    {
                        try
                        {
                            _accessToken = await _tokenCredential.GetTokenAsync(new TokenRequestContext(scopes), cancellationToken).ConfigureAwait(false);
                            _accessTokenExpiration = _accessToken.Value.ExpiresOn;
                        }
                        catch (AuthenticationFailedException)
                        {
                            // If the token acquisition fails, retry after the delay.
                            await Task.Delay(_tokenRefreshRetryDelay, cancellationToken).ConfigureAwait(false);
                            _accessToken = await _tokenCredential.GetTokenAsync(new TokenRequestContext(scopes), cancellationToken).ConfigureAwait(false);
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
}
