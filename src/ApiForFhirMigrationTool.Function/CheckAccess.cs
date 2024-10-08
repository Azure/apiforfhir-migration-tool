﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using ApiForFhirMigrationTool.Function.Configuration;
using ApiForFhirMigrationTool.Function.Security;
using Azure.Core;
using Azure.Data.Tables;
using Azure.Data.Tables.Models;
using Azure.Identity;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace ApiForFhirMigrationTool.Function
{
    public class CheckAccess
    {
        private readonly MigrationOptions _options;
        public CheckAccess(MigrationOptions options)
        {
            _options = options;
        }

        [Function("CheckAccessConnection")]
        public static async Task<HttpResponseData> CheckAccessTrigger(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
            [DurableClient] DurableTaskClient client,
            FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger("CheckAccessConnection");

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

            logger.LogInformation("Start Storage Access.");
            string tableCheckResult = await context.CallActivityAsync<string>(nameof(CheckTableAccessActivity), new object());
            logger.LogInformation("Storage access check completed.");
            logger.LogInformation("Start Azure API for FHIR Access.");
            string azureApiForFhirCheckResult = await context.CallActivityAsync<string>(nameof(CheckAzureApiForFhirServerAccessActivity), new object());
            logger.LogInformation("Azure API for FHIR access check completed.");
            logger.LogInformation("Start FHIR server Access.");
            string fhirCheckResult = await context.CallActivityAsync<string>(nameof(CheckFhirServerAccessActivity), new object());
            logger.LogInformation("FHIR server access completed.");

            JObject result = new JObject
            {
                ["TableCheck"] = JObject.Parse(tableCheckResult),
                ["AzureApiForFhir"] = JObject.Parse(azureApiForFhirCheckResult),
                ["FhirCheck"] = JObject.Parse(fhirCheckResult)
            };
            logger.LogInformation("All Check completed.");
            return result.ToString();
        }

        [Function(nameof(CheckTableAccessActivity))]
        public async Task<string> CheckTableAccessActivity([ActivityTrigger] object input, FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger(nameof(CheckTableAccessActivity));
            logger.LogInformation("Performing storage account access check.");

            string storageAccountName = _options.StagingStorageAccountName;
            JObject checkResult = new JObject();

            try
            {
                string tableServiceUri = _options.StagingStorageUri;
                TableServiceClient tableServiceClient = new TableServiceClient(new Uri(tableServiceUri), new DefaultAzureCredential());

                await foreach (TableItem table in tableServiceClient.QueryAsync())
                {
                    checkResult["Status"] = "Success";
                    checkResult["Message"] = $"Successfully accessed storage account '{storageAccountName}'.";
                    logger.LogInformation($"Status for accessing storage account: {checkResult["Status"]}");
                    return checkResult.ToString();
                }

                checkResult["Status"] = "Success";
                checkResult["Message"] = $"Successfully accessed storage account '{storageAccountName}'";
            }
            catch (Exception ex)
            {
                logger.LogError($"Failed to access the storage account: {ex.Message}");
                checkResult["Status"] = "Failed";
                checkResult["Message"] = $"Failed to access the storage account: {ex.Message}";
            }
            logger.LogInformation($"Status for accessing storage account: {checkResult["Status"]}");
            return checkResult.ToString();
        }

        [Function(nameof(CheckAzureApiForFhirServerAccessActivity))]
        public async Task<string> CheckAzureApiForFhirServerAccessActivity([ActivityTrigger] object input, FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger(nameof(CheckAzureApiForFhirServerAccessActivity));
            logger.LogInformation("Performing Azure API for FHIR server access check.");

            string azureApiForFhirServerUrl = $"{_options.SourceUri}";
            JObject checkResult = new JObject();

            try
            {
                Uri baseAddress = _options.SourceUri;
                TokenCredential tokenCredential = new DefaultAzureCredential();
                string[] scopes = new string[] { $"{baseAddress}/.default" };

                BearerTokenHandler bearerTokenHandler = new BearerTokenHandler(tokenCredential, baseAddress, scopes)
                {
                    InnerHandler = new HttpClientHandler()
                };

                HttpClient httpClient = new HttpClient(bearerTokenHandler)
                {
                    BaseAddress = baseAddress
                };

                HttpResponseMessage response = await httpClient.GetAsync(baseAddress);

                logger.LogInformation($"Status code is : {response.StatusCode}");
                if (response.IsSuccessStatusCode)
                {
                    checkResult["Status"] = "Success";
                    checkResult["Message"] = $"Successfully accessed Azure API for FHIR server at '{azureApiForFhirServerUrl}'.";
                }
                else
                {
                    checkResult["Status"] = "Failed";
                    checkResult["Message"] = $"Failed to access Azure API for FHIR server at '{azureApiForFhirServerUrl}'. Status code: {response.StatusCode}";
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"Failed to access the Azure API for FHIR server: {ex.Message}");
                checkResult["Status"] = "Failed";
                checkResult["Message"] = $"Failed to access the Azure API for FHIR server: {ex.Message}";
            }

            logger.LogInformation($"Status for accessing Azure API for FHIR server: {checkResult["Status"]}");
            return checkResult.ToString();
        }

        [Function(nameof(CheckFhirServerAccessActivity))]
        public async Task<string> CheckFhirServerAccessActivity([ActivityTrigger] object input, FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger(nameof(CheckFhirServerAccessActivity));
            logger.LogInformation("Performing FHIR server access check.");

            string fhirServerUrl = $"{_options.DestinationUri}";
            JObject checkResult = new JObject();

            try
            {
                Uri baseAddress = _options.DestinationUri;
                TokenCredential tokenCredential = new DefaultAzureCredential();
                string[] scopes = new string[] { $"{baseAddress}/.default" };

                BearerTokenHandler bearerTokenHandler = new BearerTokenHandler(tokenCredential, baseAddress, scopes)
                {
                    InnerHandler = new HttpClientHandler()
                };

                HttpClient httpClient = new HttpClient(bearerTokenHandler)
                {
                    BaseAddress = baseAddress
                };

                HttpResponseMessage response = await httpClient.GetAsync(baseAddress);

                logger.LogInformation($"Status code is : {response.StatusCode}");
                if (response.IsSuccessStatusCode)
                {
                    checkResult["Status"] = "Success";
                    checkResult["Message"] = $"Successfully accessed FHIR server at '{fhirServerUrl}'.";
                }
                else
                {
                    checkResult["Status"] = "Failed";
                    checkResult["Message"] = $"Failed to access FHIR server at '{fhirServerUrl}'. Status code: {response.StatusCode}";
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"Failed to access the FHIR server: {ex.Message}");
                checkResult["Status"] = "Failed";
                checkResult["Message"] = $"Failed to access the FHIR server: {ex.Message}";
            }
            logger.LogInformation($"Status for accessing FHIR service server: {checkResult["Status"]}");
            return checkResult.ToString();
        }
    }
}

