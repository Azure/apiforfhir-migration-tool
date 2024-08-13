// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using ApiForFhirMigrationTool.Function.Configuration;
using ApiForFhirMigrationTool.Function.FhirOperation;
using ApiForFhirMigrationTool.Function.Models;
using ApiForFhirMigrationTool.Function.OrchestrationHelper;
using ApiForFhirMigrationTool.Function.Processors;
using Azure;
using Azure.Data.Tables;
using Azure.Data.Tables.Models;
using Azure.Identity;
using Microsoft.ApplicationInsights;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace ApiForFhirMigrationTool.Function
{
    public class TableStorage
    {
        private readonly MigrationOptions _options;
        private readonly ILogger _logger;
        private readonly IOrchestrationHelper _orchestrationHelper;
        private readonly IAzureTableClientFactory _azureTableClientFactory;
        private readonly IFhirProcessor _exportProcessor;
        private readonly IFhirClient _fhirClient;
        private readonly TelemetryClient _telemetryClient;

        public TableStorage(MigrationOptions options, ILoggerFactory loggerFactory, IOrchestrationHelper orchestrationHelper, IAzureTableClientFactory azureTableClientFactory, IFhirProcessor exportProcessor, IFhirClient fhirClient, TelemetryClient telemetryClient)
        {
            _options = options;
            _logger = loggerFactory.CreateLogger<FhirMigrationToolE2E>();
            _orchestrationHelper = orchestrationHelper;
            _azureTableClientFactory = azureTableClientFactory;
            _exportProcessor = exportProcessor;
            _fhirClient = fhirClient;
            _telemetryClient = telemetryClient;
        }

        [Function("CheckAccessTrigger")]
        public static async Task<HttpResponseData> CheckAccessTrigger(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
            [DurableClient] DurableTaskClient client,
            FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger("CheckAccessTrigger");

            string body = await new StreamReader(req.Body).ReadToEndAsync();
            string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
                nameof(CheckAccessOrchestrator), body);

            logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);
            return client.CreateCheckStatusResponse(req, instanceId);
        }

        [Function(nameof(CheckAccessOrchestrator))]
        public async Task<string> CheckAccessOrchestrator(
            [OrchestrationTrigger] TaskOrchestrationContext context)
        {
            ILogger logger = context.CreateReplaySafeLogger(nameof(CheckAccessOrchestrator));
            logger.LogInformation("Start CheckAccess.");

            string tableCheckResult = await context.CallActivityAsync<string>(nameof(CheckTableAccessActivity1), null);
            string azureApiForFhirCheckResult = await context.CallActivityAsync<string>(nameof(CheckAzureApiForFhirServerAccessActivity), null);
            string fhirCheckResult = await context.CallActivityAsync<string>(nameof(CheckFhirServerAccessActivity), null);

            JObject result = new JObject
            {
                ["TableCheck"] = JObject.Parse(tableCheckResult),
                ["AzureApiForFhir"] = JObject.Parse(azureApiForFhirCheckResult),
                ["FhirCheck"] = JObject.Parse(fhirCheckResult)
            };

            return result.ToString();
        }

        [Function(nameof(CheckTableAccessActivity1))]
        public async Task<string> CheckTableAccessActivity1([ActivityTrigger] object input, FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger(nameof(CheckTableAccessActivity1));
            logger.LogInformation("Performing table access check.");

            string storageAccountName = _options.StagingStorageAccountName;
            string tableName = _options.ExportTableName;
            JObject checkResult = new JObject();

            try
            {
                string tableServiceUri = _options.StagingStorageUri;
                TableServiceClient tableServiceClient = new TableServiceClient(new Uri(tableServiceUri), new DefaultAzureCredential());

                // List all tables and check if the target table exists
                await foreach (TableItem table in tableServiceClient.QueryAsync())
                {
                    if (table.Name == tableName)
                    {
                        checkResult["Status"] = "Success";
                        checkResult["Message"] = $"Table '{tableName}' exists in storage account '{storageAccountName}'.";
                        return checkResult.ToString();
                    }
                }

                checkResult["Status"] = "Failed";
                checkResult["Message"] = $"Table '{tableName}' does not exist in storage account '{storageAccountName}'.";
            }
            catch (Exception ex)
            {
                logger.LogError($"Failed to access the storage account: {ex.Message}");
                checkResult["Status"] = "Failed";
                checkResult["Message"] = $"Failed to access the storage account: {ex.Message}";
            }

            return checkResult.ToString();
        }

        [Function(nameof(CheckAzureApiForFhirServerAccessActivity))]
        public async Task<string> CheckAzureApiForFhirServerAccessActivity([ActivityTrigger] object input, FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger(nameof(CheckAzureApiForFhirServerAccessActivity));
            logger.LogInformation("Performing FHIR server access check.");

            string fhirServerUrl = $"{_options.SourceUri}/metadata";
            JObject checkResult = new JObject();

            try
            {
                ResponseModel response = new ResponseModel();
                HttpMethod method = HttpMethod.Get;
                response =await _exportProcessor.CheckProcessStatus(fhirServerUrl, _options.SourceUri, _options.SourceHttpClient);
  
                logger.LogInformation($"status code is  {response.Status}");
                if (response.Status == ResponseStatus.Completed)
                {
                    checkResult["Status"] = "Success";
                    checkResult["Message"] = $"Successfully accessed FHIR server at '{fhirServerUrl}'.";
                }
                else
                {
                    checkResult["Status"] = "Failed";
                    checkResult["Message"] = $"Failed to access FHIR server at '{fhirServerUrl}'. Status code: {response.Status}";
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"Failed to access the FHIR server: {ex.Message}");
                checkResult["Status"] = "Failed";
                checkResult["Message"] = $"Failed to access the FHIR server: {ex.Message}";
            }

            return checkResult.ToString();
        }

        [Function(nameof(CheckFhirServerAccessActivity))]
        public async Task<string> CheckFhirServerAccessActivity([ActivityTrigger] object input, FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger(nameof(CheckFhirServerAccessActivity));
            logger.LogInformation("Performing FHIR server access check.");

            string fhirServerUrl = $"{_options.DestinationUri}/metadata";
            JObject checkResult = new JObject();

            try
            {
                ResponseModel response = new ResponseModel();
                HttpMethod method = HttpMethod.Get;
                response = await _exportProcessor.CheckProcessStatus(fhirServerUrl, _options.SourceUri, _options.SourceHttpClient);

                logger.LogInformation($"status code is  {response.Status}");
                if (response.Status == ResponseStatus.Completed)
                {
                    checkResult["Status"] = "Success";
                    checkResult["Message"] = $"Successfully accessed FHIR server at '{fhirServerUrl}'.";
                }
                else
                {
                    checkResult["Status"] = "Failed";
                    checkResult["Message"] = $"Failed to access FHIR server at '{fhirServerUrl}'. Status code: {response.Status}";
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"Failed to access the FHIR server: {ex.Message}");
                checkResult["Status"] = "Failed";
                checkResult["Message"] = $"Failed to access the FHIR server: {ex.Message}";
            }

            return checkResult.ToString();
        }
    }
}
