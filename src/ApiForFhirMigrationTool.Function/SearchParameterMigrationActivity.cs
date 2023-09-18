// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using ApiForFhirMigrationTool.Function.SearchParameterOperation;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace ApiForFhirMigrationTool.Function.Migration
{
    public class SearchParameterMigrationActivity
    {
        private readonly ISearchParameterOperation _searchParameterOperation;
        private readonly ILogger _logger;

        public SearchParameterMigrationActivity(ISearchParameterOperation searchParameterOperation, ILogger<SearchParameterMigrationActivity> logger)
        {
            _searchParameterOperation = searchParameterOperation;
            _logger = logger;
        }

        [Function(nameof(SearchParameterMigration))]
        public async Task SearchParameterMigration([ActivityTrigger] FunctionContext executionContext)
        {
            _logger.LogInformation($"SearchParameterMigration Started");

            try
            {
                // Get search parameters from Gen1
                JObject jObjectResponse = await _searchParameterOperation.GetSearchParameters();

                // If resource present in bundle then transform it into batch and Post to Gen2
                if (jObjectResponse.ContainsKey("entry"))
                {
                    // Transform to batch and add request object
                    string transformedObject = _searchParameterOperation.TransformObject(jObjectResponse);

                    // Post serach parametes to Gen2
                    await _searchParameterOperation.PostSearchParameters(transformedObject);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"SearchParameterMigration() Exception:  {ex.Message}");
                throw;
            }

            _logger.LogInformation($"SearchParameterMigration Finished");
        }
    }
}
