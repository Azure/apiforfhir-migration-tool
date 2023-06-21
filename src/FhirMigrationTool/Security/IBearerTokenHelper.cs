// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using Azure.Core;

namespace FhirMigrationTool.Security
{
    public interface IBearerTokenHelper
    {
        /// <summary>
        /// To fetch bearer token
        /// </summary>
        /// <param name="scopes"></param>
        /// <param name="cancellationToken"></param>
        /// <param name="nullAccessToken"></param>
        /// <returns>Auth Token</returns>
        Task<AccessToken> GetTokenAsync(string[] scopes, CancellationToken cancellationToken, string nullAccessToken = "");
    }
}
