// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using ApiForFhirMigrationTool.Function.SearchParameterOperation;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Microsoft.DurableTask;
using ApiForFhirMigrationTool.Function.Models;
using Azure.Data.Tables;
using Azure;
using ApiForFhirMigrationTool.Function.Configuration;

namespace ApiForFhirMigrationTool.Function.Migration
{
    public class SearchParameterMigrationActivity
    {
        private readonly ISearchParameterOperation _searchParameterOperation;
        private readonly ILogger _logger;
        private readonly MigrationOptions _options;
        private readonly IAzureTableClientFactory _azureTableClientFactory;
        private readonly IMetadataStore _azureTableMetadataStore;

        public SearchParameterMigrationActivity(ISearchParameterOperation searchParameterOperation, IAzureTableClientFactory azureTableClientFactory, IMetadataStore azureTableMetadataStore, MigrationOptions options, ILogger<SearchParameterMigrationActivity> logger)
        {
            _searchParameterOperation = searchParameterOperation;
            _logger = logger;
            _options = options;
            _azureTableClientFactory = azureTableClientFactory;
            _azureTableMetadataStore = azureTableMetadataStore;
        }


        [Function(nameof(SearchParameterOrchestration))]
        public async Task<string> SearchParameterOrchestration(
            [OrchestrationTrigger] TaskOrchestrationContext context)
        {
            ILogger logger = context.CreateReplaySafeLogger(nameof(SearchParameterOrchestration));
            logger.LogInformation("Starting Search Parameter activities.");
            var statusRespose = new HttpResponseMessage();
            var statusUrl = string.Empty;
            var import_body = string.Empty;

            try
            {
                logger.LogInformation("Checking whether the chunk and export table exists or not");
                TableClient chunktableClient = _azureTableClientFactory.Create(_options.ChunkTableName);
                TableClient exportTableClient = _azureTableClientFactory.Create(_options.ExportTableName);

                Pageable<TableEntity> jobListSeacrh = chunktableClient.Query<TableEntity>(filter: ent => ent.GetBoolean("SearchParameterMigration") == false);
                if (jobListSeacrh.Count() > 0)
                {
                    // Run Activity for Search Parameter
                    await context.CallActivityAsync("SearchParameterMigration");

                    TableEntity qEntitynew = _azureTableMetadataStore.GetEntity(chunktableClient, _options.PartitionKey, _options.RowKey);
                    qEntitynew["SearchParameterMigration"] = true;
                    _azureTableMetadataStore.UpdateEntity(chunktableClient, qEntitynew);
                }
            }
            catch
            {
                throw;
            }

            return "Completed";
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
