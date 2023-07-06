// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Net;
using System.Net.Http.Headers;
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
            var statusUrl = string.Empty;

            try
            {
                while (retryCount <= _options.RetryCount)
                {
                    ResponseModel importResponse = await context.CallActivityAsync<ResponseModel>(nameof(ProcessImport), requestContent);
                    if (importResponse.Status == ResponseStatus.Completed)
                    {
                        logger?.LogInformation($"Import  returned: Success.");
                        statusUrl = importResponse.Content;
                        break;
                    }
                    else if (importResponse.Status == ResponseStatus.Retry)
                    {
                        logger?.LogInformation($"Import Status check returned: 429. Retrying in {_options.WaitForRetry} minutes");
                        retryCount++;
                        DateTime waitTime = context.CurrentUtcDateTime.Add(TimeSpan.FromMinutes(_options.WaitForRetry));
                        await context.CreateTimer(waitTime, CancellationToken.None);
                    }
                    else
                    {
                        logger?.LogInformation($"Import Status check returned: Unsuccessful.");
                        throw new HttpFailureException($"Response: {importResponse.Content} ");
                    }
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
        public async Task<ResponseModel> ProcessImport([ActivityTrigger] string requestContent, FunctionContext executionContext)
        {
            HttpResponseMessage importResponse;
            var response = new ResponseModel();
            var importStatusUrl = string.Empty;
            try
            {
                importResponse = await _importProcessor.CallImport(requestContent);

                switch (importResponse.StatusCode)
                {
                    case HttpStatusCode.Accepted:
                        response.Status = ResponseStatus.Accepted;
                        HttpHeaders headers = importResponse.Content.Headers;
                        IEnumerable<string> values;
                        if (headers.GetValues("Content-Location") != null)
                        {
                            values = headers.GetValues("Content-Location");
                            importStatusUrl = values.First();
                        }

                        response.Content = importStatusUrl;
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
            catch
            {
                throw;
            }

            return response;
        }

        [Function(nameof(ProcessImportStatusCheck))]
        public async Task<ResponseModel> ProcessImportStatusCheck([ActivityTrigger] string importStatusUrl, FunctionContext executionContext)
        {
            var response = new ResponseModel();
            try
            {
                if (!string.IsNullOrEmpty(importStatusUrl))
                {
                    HttpResponseMessage importStatusResponse = await _importProcessor.CheckImportStatus(importStatusUrl);
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
