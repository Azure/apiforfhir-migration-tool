// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using ApiForFhirMigrationTool.Function.Configuration;
using ApiForFhirMigrationTool.Function.FhirOperation;
using ApiForFhirMigrationTool.Function.Models;
using ApiForFhirMigrationTool.Function.OrchestrationHelper;
using ApiForFhirMigrationTool.Function.Processors;
using Microsoft.ApplicationInsights;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace ApiForFhirMigrationTool.Function
{
    public class FhirMigrationToolE2E
    {
        private readonly MigrationOptions _options;
        private readonly ILogger _logger;
        private readonly IOrchestrationHelper _orchestrationHelper;
        private readonly IAzureTableClientFactory _azureTableClientFactory;
        private readonly IFhirProcessor _exportProcessor;
        private readonly IFhirClient _fhirClient;
        private readonly TelemetryClient _telemetryClient;

        public FhirMigrationToolE2E(MigrationOptions options, ILoggerFactory loggerFactory, IOrchestrationHelper orchestrationHelper, IAzureTableClientFactory azureTableClientFactory, IFhirProcessor exportProcessor, IFhirClient fhirClient, TelemetryClient telemetryClient)
        {
            _options = options;
            _logger = loggerFactory.CreateLogger<FhirMigrationToolE2E>();
            _orchestrationHelper = orchestrationHelper;
            _azureTableClientFactory = azureTableClientFactory;
            _exportProcessor = exportProcessor;
            _fhirClient = fhirClient;
            _telemetryClient = telemetryClient;
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

        [Function(nameof(E2ETestOrchestration))]
        public async Task<string> E2ETestOrchestration(
            [OrchestrationTrigger] TaskOrchestrationContext context)
        {
            ILogger logger = context.CreateReplaySafeLogger(nameof(E2ETestOrchestration));
            logger.LogInformation("Start E2E Test.");

            int count = 0;
            var resSurface = new JArray();
            var resDeep = new JArray();
            JObject check = new JObject();

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

                check.Add("SurfaceCheck", resSurface);
                check.Add("DeepCheck", resDeep);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.ToString());
            }

            string jsonString = check.ToString();
            return jsonString;
        }
    }
}
