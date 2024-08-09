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
                logger.LogInformation("Creating table client");
                TableClient chunktableClient = _azureTableClientFactory.Create(_options.ChunkTableName);
                TableClient exportTableClient = _azureTableClientFactory.Create(_options.ExportTableName);
                logger.LogInformation("Table client created successfully.");

                logger?.LogInformation("Querying the chunk table to check if SearchParameter migration is completed.");
                Pageable<TableEntity> jobListSeacrh = chunktableClient.Query<TableEntity>(filter: ent => ent.GetBoolean("SearchParameterMigration") == false);
                logger?.LogInformation("SearchParameter migration status retrieved from the chunk table.");
                if (jobListSeacrh.Count() > 0)
                {
                    // Run Activity for Search Parameter
                    logger?.LogInformation("Calling SearchParameterMigration function");
                    await context.CallActivityAsync("SearchParameterMigration");
                    logger?.LogInformation("SearchParameterMigration function has completed.");

                    TableEntity qEntitynew = _azureTableMetadataStore.GetEntity(chunktableClient, _options.PartitionKey, _options.RowKey);
                    qEntitynew["SearchParameterMigration"] = true;
                    logger?.LogInformation("Starting update of the chunk table.");
                    _azureTableMetadataStore.UpdateEntity(chunktableClient, qEntitynew);
                    logger?.LogInformation("Completed update of the chunk table.");
                }
                else
                {
                    logger?.LogInformation("Search parameter migration is a one-time activity and has already been completed.");

                }
            }
            catch
            {
                throw;
            }
            logger?.LogInformation("Finished Search Parameter activities.");
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
