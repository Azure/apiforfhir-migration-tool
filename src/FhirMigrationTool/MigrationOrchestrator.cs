// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using FhirMigrationTool.Configuration;
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

        public MigrationOrchestrator(MigrationOptions options, ILoggerFactory loggerFactory, IOrchestrationHelper orchestrationHelper)
        {
            _options = options;
            _logger = loggerFactory.CreateLogger<MigrationOrchestrator>();
            _orchestrationHelper = orchestrationHelper;
        }

        [Function(nameof(MigrationOrchestration))]
        public async Task<List<string>> MigrationOrchestration(
            [OrchestrationTrigger] TaskOrchestrationContext context)
        {
            ILogger logger = context.CreateReplaySafeLogger(nameof(MigrationOrchestration));
            if (!_orchestrationHelper.ValidateConfig(_options))
            {
                logger.LogError("Required configuration values are missing, Please provide all required configurations.");
                throw new ArgumentException($"Process exiting: Please check all the required configuration values are available.");
            }

            logger.LogInformation("Start MigrationOrchestration.");
            var outputs = new List<string>();
            try
            {
                // Run sub orchestration for export
                var export = await context.CallSubOrchestratorAsync<string>("ExportOrchestration");

                // Run sub orchestration for Import
                var import = await context.CallSubOrchestratorAsync<string>("ImportOrchestration", export);
            }
            catch (Exception ex)
            {
                logger.LogError($"Error occurred during migration process: {ex.Message}");
            }

            return outputs;
        }

        [Function("MigrationOrchestration_HttpStart")]
        public static async Task<HttpResponseData> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
            [DurableClient] DurableTaskClient client,
            FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger("MigrationOrchestration_HttpStart");

            // Function input comes from the request content.
            string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
                nameof(MigrationOrchestration));

            logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);

            // Returns an HTTP 202 response with an instance management payload.
            // See https://learn.microsoft.com/azure/azure-functions/durable/durable-functions-http-api#start-orchestration
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
