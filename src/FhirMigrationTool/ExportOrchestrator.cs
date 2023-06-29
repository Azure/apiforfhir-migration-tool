// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Net;
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

            try
            {
                var statusUrl = await context.CallActivityAsync<string>(nameof(ProcessExport));
                while (true)
                {
                    var response = await context.CallActivityAsync<ResponseModel>(nameof(ProcessExportStatusCheck), statusUrl);

                    if (response.Status == ResponseStatus.Accepted)
                    {
                        logger?.LogInformation($"Export Status check returned: InProgress.");
                        logger?.LogInformation($"Waiting for next status check for {_options.ScheduleInterval} minutes.");
                        DateTime waitTime = context.CurrentUtcDateTime.Add(TimeSpan.FromMinutes(
                    Convert.ToDouble(_options.ScheduleInterval)));
                        await context.CreateTimer(waitTime, CancellationToken.None);
                    }
                    else if (response.Status == ResponseStatus.Completed)
                    {
                        logger?.LogInformation($"Export Status check returned: Success.");
                        var import_body = _helper.CreateImportRequest(response.Content, _options.ImportMode);
                        return import_body;
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
                            throw new HttpFailureException($"StatusCode: {statusRespose.StatusCode}, Response: {statusRespose.Content.ReadAsStringAsync()}. Export status check failed, exiting process");
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
        }

        [Function(nameof(ProcessExport))]
        public async Task<string> ProcessExport([ActivityTrigger] string name, FunctionContext executionContext)
        {
            try
            {
                string exportStatusUrl = await _exportProcessor.Execute();

                return exportStatusUrl;
            }
            catch
            {
                throw;
            }
        }

        [Function(nameof(ProcessExportStatusCheck))]
        public async Task<ResponseModel> ProcessExportStatusCheck([ActivityTrigger] string exportStatusUrl, FunctionContext executionContext)
        {
            var exportStatusResponse = new HttpResponseMessage();
            var response = new ResponseModel();
            ILogger logger = executionContext.GetLogger(nameof(ProcessExportStatusCheck));
            try
            {
                if (!string.IsNullOrEmpty(exportStatusUrl))
                {
                    exportStatusResponse = await _exportProcessor.CheckExportStatus(exportStatusUrl);
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
