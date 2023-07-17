// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using FhirMigrationTool.Configuration;
using FhirMigrationTool.ExceptionHelper;
using FhirMigrationTool.Models;
using FhirMigrationTool.Processors;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace FhirMigrationTool
{
    public class ImportOrchestrator
    {
        private readonly IFhirProcessor _importProcessor;
        private readonly MigrationOptions _options;

        public ImportOrchestrator(IFhirProcessor importProcessor, MigrationOptions options)
        {
            _importProcessor = importProcessor;
            _options = options;
        }

        [Function(nameof(ImportOrchestration))]
        public async Task<string> ImportOrchestration(
            [OrchestrationTrigger] TaskOrchestrationContext context, string requestContent)
        {
            ILogger logger = context.CreateReplaySafeLogger(nameof(ImportOrchestration));
            logger.LogInformation("Starting import activities.");
            var statusRespose = new HttpResponseMessage();
            var statusUrl = string.Empty;

            try
            {
                ResponseModel importResponse = await context.CallActivityAsync<ResponseModel>(nameof(ProcessImport), requestContent);
                if (importResponse.Status == ResponseStatus.Completed)
                {
                    logger?.LogInformation($"Import  returned: Success.");
                    statusUrl = importResponse.Content;
                }
                else
                {
                    logger?.LogInformation($"Import Status check returned: Unsuccessful.");
                    throw new HttpFailureException($"Response: {importResponse.Content} ");
                }

                while (true)
                {
                    ResponseModel response = await context.CallActivityAsync<ResponseModel>(nameof(ProcessImportStatusCheck), statusUrl);

                    if (response.Status == ResponseStatus.Accepted)
                    {
                        logger?.LogInformation($"Import Status check returned: InProgress.");
                        logger?.LogInformation($"Waiting for next status check for {_options.ScheduleInterval} minutes.");
                        DateTime waitTime = context.CurrentUtcDateTime.Add(TimeSpan.FromMinutes(
                    Convert.ToDouble(_options.ScheduleInterval)));
                        await context.CreateTimer(waitTime, CancellationToken.None);
                    }
                    else if (response.Status == ResponseStatus.Completed)
                    {
                        logger?.LogInformation($"Import Status check returned: Success.");
                        return "Completed";
                    }
                    else
                    {
                        logger?.LogInformation($"Import Status check returned: Unsuccessful.");
                        throw new HttpFailureException($"StatusCode: {statusRespose.StatusCode}, Response: {statusRespose.Content.ReadAsStringAsync()} ");
                    }
                }
            }
            catch
            {
                throw;
            }
        }

        [Function(nameof(ProcessImport))]
        public async Task<ResponseModel> ProcessImport([ActivityTrigger] string requestContent, FunctionContext executionContext)
        {
            try
            {
                HttpMethod method = HttpMethod.Post;
                ResponseModel importResponse = await _importProcessor.CallProcess(method, requestContent, _options.DestinationUri, "/$import",  _options.DestinationHttpClient);
                return importResponse;
            }
            catch
            {
                throw;
            }
        }

        [Function(nameof(ProcessImportStatusCheck))]
        public async Task<ResponseModel> ProcessImportStatusCheck([ActivityTrigger] string importStatusUrl, FunctionContext executionContext)
        {
            try
            {
                if (!string.IsNullOrEmpty(importStatusUrl))
                {
                    ResponseModel importStatusResponse = await _importProcessor.CheckProcessStatus(importStatusUrl, _options.DestinationUri, _options.DestinationHttpClient);
                    return importStatusResponse;
                }
                else
                {
                    throw new ArgumentException($"Url to check import status was empty.");
                }
            }
            catch
            {
                throw;
            }
        }
    }
}
