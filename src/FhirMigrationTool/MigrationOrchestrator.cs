// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using Azure;
using Azure.Data.Tables;
using FhirMigrationTool.Configuration;
using FhirMigrationTool.Models;
using FhirMigrationTool.OrchestrationHelper;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace FhirMigrationTool
{
    public class MigrationOrchestrator
    {
        private readonly MigrationOptions _options;
        private readonly ILogger _logger;
        private readonly IOrchestrationHelper _orchestrationHelper;
        private readonly IAzureTableClientFactory _azureTableClientFactory;

        public MigrationOrchestrator(MigrationOptions options, ILoggerFactory loggerFactory, IOrchestrationHelper orchestrationHelper, IAzureTableClientFactory azureTableClientFactory)
        {
            _options = options;
            _logger = loggerFactory.CreateLogger<MigrationOrchestrator>();
            _orchestrationHelper = orchestrationHelper;
            _azureTableClientFactory = azureTableClientFactory;
        }

        [Function(nameof(MigrationOrchestration))]
        public async Task<List<string>> MigrationOrchestration(
            [OrchestrationTrigger] TaskOrchestrationContext context)
        {
            ILogger logger = context.CreateReplaySafeLogger(nameof(MigrationOrchestration));
            var outputs = new List<string>();

            try
            {
                _options.ValidateConfig();
                logger.LogInformation("Start MigrationOrchestration.");

                TableClient exportTableClient = _azureTableClientFactory.Create(_options.ExportTableName);

                // Run sub orchestration for export
                Pageable<TableEntity> jobList = exportTableClient.Query<TableEntity>(filter: ent => ent.GetString("IsExportRunning") == "Running" || ent.GetString("IsExportRunning") == "Started" || ent.GetString("IsImportRunning") == "Running" || ent.GetString("IsImportRunning") == "Started" || ent.GetString("IsImportRunning") == "Not Started");
                if (jobList.Count() <= 0)
                {
                    // Run Get and Post activity for search parameter
                    await context.CallActivityAsync("SearchParameterMigration");

                    var exportContent = await context.CallSubOrchestratorAsync<string>("ExportOrchestration");
                }

                Pageable<TableEntity> exportRunningjobList = exportTableClient.Query<TableEntity>(filter: ent => ent.GetString("IsExportRunning") == "Started" || ent.GetString("IsExportRunning") == "Running");
                if (exportRunningjobList.Count() > 0)
                {
                    var exportStatusContent = await context.CallSubOrchestratorAsync<string>("ExportStatusOrchestration");
                }

                // Run sub orchestration for Import
                Pageable<TableEntity> jobListimport = exportTableClient.Query<TableEntity>(filter: ent => ent.GetBoolean("IsExportComplete") == true && ent.GetString("ImportRequest") != string.Empty && ent.GetString("IsImportRunning") == "Not Started");
                if (jobListimport.Count() > 0)
                {
                    var import = await context.CallSubOrchestratorAsync<string>("ImportOrchestration");
                }

                Pageable<TableEntity> jobListimportRunning = exportTableClient.Query<TableEntity>(filter: ent => ent.GetString("IsImportRunning") == "Started" || ent.GetString("IsImportRunning") == "Running");
                if (jobListimportRunning.Count() > 0)
                {
                    var importStatus = await context.CallSubOrchestratorAsync<string>("ImportStatusOrchestration");
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"Error occurred during migration process: {ex.Message}");
            }

            return outputs;
        }

        [Function("MigrationSP_HttpStart")]
        public static async Task<HttpResponseData> SearchParamStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
            [DurableClient] DurableTaskClient client,
            FunctionContext executionContext)
        {
            string instanceId_new = "FhirMigrationTool";
            StartOrchestrationOptions options = new StartOrchestrationOptions(instanceId_new);
            var instanceId = await client.ScheduleNewOrchestrationInstanceAsync(nameof(MigrationOrchestration), options);
            return client.CreateCheckStatusResponse(req, instanceId);
        }

        [Function(nameof(CountCheckOrchestration))]
        public static async Task<string> CountCheckOrchestration(
            [OrchestrationTrigger] TaskOrchestrationContext context)
        {
            ILogger logger = context.CreateReplaySafeLogger(nameof(CountCheckOrchestration));
            logger.LogInformation("Start SurfaceCheckOrchestration.");
            var outputs = new List<string>();
            outputs.Add("Start Surface Check");

            var surfaceCheck = await context.CallSubOrchestratorAsync<string>("SurfaceCheckOrchestration");

            return surfaceCheck;
        }

        [Function(nameof(DeepCheckOrc))]
        public static async Task<string> DeepCheckOrc(
            [OrchestrationTrigger] TaskOrchestrationContext context)
        {
            ILogger logger = context.CreateReplaySafeLogger(nameof(DeepCheckOrc));
            logger.LogInformation("Start DeepCheckOrchestration.");
            var outputs = new List<string>();
            outputs.Add("Start Deep Check");

            string deepCheck = await context.CallSubOrchestratorAsync<string>("DeepCheckOrchestration");

            return deepCheck;
        }

        [Function("SurfaceCheckOrchestration_HttpStart")]
        public static async Task<HttpResponseData> SurfaceHttpCheck(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
            [DurableClient] DurableTaskClient client,
            FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger("SurfaceCheckOrchestration_HttpStart");

            // Function input comes from the request content.
            string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
                nameof(CountCheckOrchestration));

            logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);

            // Returns an HTTP 202 response with an instance management payload.
            // See https://learn.microsoft.com/azure/azure-functions/durable/durable-functions-http-api#start-orchestration
            return client.CreateCheckStatusResponse(req, instanceId);
        }

        [Function("DeepCheckOrchestration_HttpStart")]
        public static async Task<HttpResponseData> DeepHttpCheck(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
            [DurableClient] DurableTaskClient client,
            FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger("DeepCheckOrchestration_HttpStart");

            // Function input comes from the request content.
            string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
                nameof(DeepCheckOrc));

            logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);

            // Returns an HTTP 202 response with an instance management payload.
            // See https://learn.microsoft.com/azure/azure-functions/durable/durable-functions-http-api#start-orchestration
            return client.CreateCheckStatusResponse(req, instanceId);
        }
    }
}
