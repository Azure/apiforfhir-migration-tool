// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Net;
using System.Net.Http.Headers;
using FhirMigrationTool.Configuration;
using FhirMigrationTool.ExceptionHelper;
using FhirMigrationTool.ExportProcess;
using FhirMigrationTool.Models;
using FhirMigrationTool.OrchestrationHelper;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace FhirMigrationTool
{
    public class ExportOrchestrator
    {
        private readonly IExportProcessor _exportProcessor;
        private readonly MigrationOptions _options;
        private readonly IOrchestrationHelper _helper;

        public ExportOrchestrator(IExportProcessor exportProcessor, MigrationOptions options, IOrchestrationHelper helper)
        {
            _exportProcessor = exportProcessor;
            _options = options;
            _helper = helper;
        }

        [Function(nameof(ExportOrchestration))]
        public async Task<string> ExportOrchestration(
            [OrchestrationTrigger] TaskOrchestrationContext context)
        {
            ILogger logger = context.CreateReplaySafeLogger(nameof(ExportOrchestration));
            logger.LogInformation("Starting export activities.");
            var statusRespose = new HttpResponseMessage();
            var retryCount = 0;
            var statusUrl = string.Empty;
            var import_body = string.Empty;

            try
            {
                while (retryCount <= _options.RetryCount)
                {
                    ResponseModel exportResponse = await context.CallActivityAsync<ResponseModel>(nameof(ProcessExport));
                    if (exportResponse.Status == ResponseStatus.Completed)
                    {
                        logger?.LogInformation($"Export  returned: Success.");
                        statusUrl = exportResponse.Content;
                        break;
                    }
                    else if (exportResponse.Status == ResponseStatus.Retry)
                    {
                        logger?.LogInformation($"Export Status check returned: 429. Retrying in {_options.WaitForRetry} minutes");
                        retryCount++;
                        DateTime waitTime = context.CurrentUtcDateTime.Add(TimeSpan.FromMinutes(_options.WaitForRetry));
                        await context.CreateTimer(waitTime, CancellationToken.None);
                    }
                    else
                    {
                        logger?.LogInformation($"Export Status check returned: Unsuccessful.");
                        throw new HttpFailureException($"Response: {exportResponse.Content} ");
                    }
                }

                while (true)
                {
                    ResponseModel response = await context.CallActivityAsync<ResponseModel>(nameof(ProcessExportStatusCheck), statusUrl);

                    if (response.Status == ResponseStatus.Accepted)
                    {
                        logger?.LogInformation($"Export Status check returned: InProgress.");
                        logger?.LogInformation($"Waiting for next status check for {_options.ScheduleInterval} minutes.");
                        DateTime waitTime = context.CurrentUtcDateTime.Add(TimeSpan.FromMinutes(_options.ScheduleInterval));
                        await context.CreateTimer(waitTime, CancellationToken.None);
                    }
                    else if (response.Status == ResponseStatus.Completed)
                    {
                        logger?.LogInformation($"Export Status check returned: Success.");
                        import_body = _helper.CreateImportRequest(response.Content, _options.ImportMode);
                        break;
                    }
                    else if (response.Status == ResponseStatus.Retry)
                    {
                        logger?.LogInformation($"Export Status check returned: 429.");
                        if (retryCount < _options.RetryCount)
                        {
                            retryCount++;
                            continue;
                        }
                        else
                        {
                            throw new HttpFailureException($"Response: {response.Content}. Export status check exceeded retry limit, exiting process");
                        }
                    }
                    else
                    {
                        logger?.LogInformation($"Export Status check returned: Unsuccessful.");
                        throw new HttpFailureException($"StatusCode: {statusRespose.StatusCode}, Response: {statusRespose.Content.ReadAsStringAsync()} ");
                    }
                }
            }
            catch
            {
                throw;
            }

            return import_body;
        }

        [Function(nameof(ProcessExport))]
        public async Task<ResponseModel> ProcessExport([ActivityTrigger] string name, FunctionContext executionContext)
        {
            HttpResponseMessage exportResponse;
            var response = new ResponseModel();
            var exportStatusUrl = string.Empty;
            try
            {
                exportResponse = await _exportProcessor.CallExport();

                switch (exportResponse.StatusCode)
                {
                    case HttpStatusCode.Accepted:
                        response.Status = ResponseStatus.Accepted;
                        HttpHeaders headers = exportResponse.Content.Headers;
                        IEnumerable<string> values;
                        if (headers.GetValues("Content-Location") != null)
                        {
                            values = headers.GetValues("Content-Location");
                            exportStatusUrl = values.First();
                        }

                        response.Content = exportStatusUrl;
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

        [Function(nameof(ProcessExportStatusCheck))]
        public async Task<ResponseModel> ProcessExportStatusCheck([ActivityTrigger] string exportStatusUrl, FunctionContext executionContext)
        {
            ResponseModel response = new ResponseModel();
            try
            {
                if (!string.IsNullOrEmpty(exportStatusUrl))
                {
                    HttpResponseMessage exportStatusResponse = await _exportProcessor.CheckExportStatus(exportStatusUrl);
                    switch (exportStatusResponse.StatusCode)
                    {
                        case HttpStatusCode.Accepted:
                            response.Status = ResponseStatus.Accepted;
                            break;
                        case HttpStatusCode.OK:
                            response.Content = exportStatusResponse.Content.ReadAsStringAsync().Result;
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
                    throw new ArgumentException($"Url to check export status was empty.");
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
