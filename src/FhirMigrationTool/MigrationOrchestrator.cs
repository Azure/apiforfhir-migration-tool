// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using Azure;
using Azure.Data.Tables;
using FhirMigrationTool.Configuration;
using FhirMigrationTool.FhirOperation;
using FhirMigrationTool.Models;
using FhirMigrationTool.OrchestrationHelper;
using FhirMigrationTool.Processors;
using Microsoft.ApplicationInsights;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace FhirMigrationTool
{
    public class MigrationOrchestrator
    {
        private readonly MigrationOptions _options;
        private readonly ILogger _logger;
        private readonly IOrchestrationHelper _orchestrationHelper;
        private readonly IAzureTableClientFactory _azureTableClientFactory;
        private readonly IFhirProcessor _exportProcessor;
        private readonly IFhirClient _fhirClient;
        private readonly TelemetryClient _telemetryClient;

        public MigrationOrchestrator(MigrationOptions options, ILoggerFactory loggerFactory, IOrchestrationHelper orchestrationHelper, IAzureTableClientFactory azureTableClientFactory, IFhirProcessor exportProcessor, IFhirClient fhirClient, TelemetryClient telemetryClient)
        {
            _options = options;
            _logger = loggerFactory.CreateLogger<MigrationOrchestrator>();
            _orchestrationHelper = orchestrationHelper;
            _azureTableClientFactory = azureTableClientFactory;
            _exportProcessor = exportProcessor;
            _fhirClient = fhirClient;
            _telemetryClient = telemetryClient;
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

        [Function("TimerOrchestration")]
        public async Task Run(
         [TimerTrigger("0 */5 * * * *")] TimerInfo myTimer,
         [DurableClient] DurableTaskClient client,
         FunctionContext executionContext)
        {
            string instanceId_new = "FhirMigrationTool";
            StartOrchestrationOptions options = new StartOrchestrationOptions(instanceId_new);
            var instanceId = await client.ScheduleNewOrchestrationInstanceAsync(nameof(MigrationOrchestration), options);
            _logger.LogInformation("Started: Timed {instanceId}...", instanceId);
        }

        [Function(nameof(E2ETestOrchestration))]
        public async Task<string> E2ETestOrchestration(
            [OrchestrationTrigger] TaskOrchestrationContext context)
        {
            ILogger logger = context.CreateReplaySafeLogger(nameof(E2ETestOrchestration));
            logger.LogInformation("Start E2E Test.");

            int count = 0;
            var resSurface = new JArray();
            var resDeep = new JArray();

            string? externalInput = context.GetInput<string>();
            if (!string.IsNullOrEmpty(externalInput))
            {
                JObject jsonObject = JObject.Parse(externalInput);

                // Get the specific value by property name
                if (jsonObject != null)
                {
                    count = (int)jsonObject["Count"]!;
                }
            }

            try
            {
                if (count == 2 || count == 3)
                {
                    string e2eImportGen1 = await context.CallActivityAsync<string>("E2ETestActivity", count);
                }

                var options = TaskOptions.FromRetryPolicy(new RetryPolicy(
                        maxNumberOfAttempts: 3,
                        firstRetryInterval: TimeSpan.FromSeconds(5)));

                var exportContent = await context.CallSubOrchestratorAsync<string>("ExportOrchestration", options: options);
                logger.LogInformation("E2E Test for export completed.");

                var exportStatusContent = await context.CallSubOrchestratorAsync<string>("ExportStatusOrchestration", options: options);
                logger.LogInformation("E2E Test for export status completed.");

                var import = await context.CallSubOrchestratorAsync<string>("ImportOrchestration", options: options);
                logger.LogInformation("E2E Test for import completed.");

                var importStatus = await context.CallSubOrchestratorAsync<string>("ImportStatusOrchestration", options: options);
                logger.LogInformation("E2E Test for import status completed.");
                if (_options.QuerySurface != null)
                {
                    var surfaceCheckQuery = new List<string>(_options.QuerySurface);

                    foreach (var item in surfaceCheckQuery)
                    {
                        // Run Surface test
                        var surfaceCheck = await context.CallActivityAsync<string>("Count", item);
                        JObject jsonObject = JObject.Parse(surfaceCheck);
                        resSurface.Add(jsonObject);
                    }
                }

                if (_options.QueryDeep != null)
                {
                    var deepCheckQuery = new List<string>(_options.QueryDeep);
                    foreach (var item in deepCheckQuery)
                    {
                        // Run Deep Check test
                        var deepCheck = await context.CallActivityAsync<string>("DeepResourceCheck", item);
                        JObject jsonObject = JObject.Parse(deepCheck);
                        resDeep.Add(jsonObject);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

            return "completed";
        }

        [Function("E2ETest_Http")]
        public static async Task<HttpResponseData> E2ETest_Http(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
            [DurableClient] DurableTaskClient client,
            FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger("E2ETest_Http");

            // Function input comes from the request content.
            string body = await new StreamReader(req.Body).ReadToEndAsync();
            string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
                nameof(E2ETestOrchestration), body);

            logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);

            // Returns an HTTP 202 response with an instance management payload.
            // See https://learn.microsoft.com/azure/azure-functions/durable/durable-functions-http-api#start-orchestration
            return client.CreateCheckStatusResponse(req, instanceId);
        }
    }
}
