// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Net;
using FhirMigrationTool.Configuration;
using FhirMigrationTool.ExceptionHelper;
using FhirMigrationTool.ImportProcess;
using FhirMigrationTool.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace FhirMigrationTool
{
    public class ImportOrchestrator
    {
        private readonly IImportProcessor _importProcessor;
        private readonly MigrationOptions _options;

        public ImportOrchestrator(IImportProcessor importProcessor, MigrationOptions options)
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
            var retryCount = 0;

            try
            {
                var statusUrl = await context.CallActivityAsync<string>(nameof(ProcessImport), requestContent);
                while (true)
                {
                    var response = await context.CallActivityAsync<ResponseModel>(nameof(ProcessImportStatusCheck), statusUrl);

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
                    else if (response.Status == ResponseStatus.Retry)
                    {
                        logger?.LogInformation($"Import Status check returned: 429.");
                        if (retryCount < _options.RetryCount)
                        {
                            retryCount++;
                            continue;
                        }
                        else
                        {
                            throw new HttpFailureException($"StatusCode: {statusRespose.StatusCode}, Response: {statusRespose.Content.ReadAsStringAsync()}. Import status check failed, exiting process");
                        }
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
        public async Task<string> ProcessImport([ActivityTrigger] string requestContent, FunctionContext executionContext)
        {
            try
            {
                string importStatusUrl = await _importProcessor.Execute(requestContent);

                return importStatusUrl;
            }
            catch
            {
                throw;
            }
        }

        [Function(nameof(ProcessImportStatusCheck))]
        public async Task<ResponseModel> ProcessImportStatusCheck([ActivityTrigger] string importStatusUrl, FunctionContext executionContext)
        {
            var importStatusResponse = new HttpResponseMessage();
            var response = new ResponseModel();
            ILogger logger = executionContext.GetLogger(nameof(ProcessImportStatusCheck));
            try
            {
                if (!string.IsNullOrEmpty(importStatusUrl))
                {
                    importStatusResponse = await _importProcessor.CheckImportStatus(importStatusUrl);
                    switch (importStatusResponse.StatusCode)
                    {
                        case HttpStatusCode.Accepted:
                            response.Status = ResponseStatus.Accepted;
                            break;
                        case HttpStatusCode.OK:
                            response.Content = importStatusResponse.Content.ReadAsStringAsync().Result;
                            response.Status = ResponseStatus.Completed;
                            break;
                        case HttpStatusCode.TooManyRequests:
                            response.Status = ResponseStatus.Retry;
                            break;
                        default:
                            response.Status = ResponseStatus.Failed;
                            break;
                    }
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

            return response;
        }
    }
}
