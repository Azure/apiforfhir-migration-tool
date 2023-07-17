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
    public class ExportOrchestrator
    {
        private readonly IFhirProcessor _exportProcessor;
        private readonly MigrationOptions _options;

        public ExportOrchestrator(IFhirProcessor exportProcessor, MigrationOptions options)
        {
            _exportProcessor = exportProcessor;
            _options = options;
        }

        [Function(nameof(ExportOrchestration))]
        public async Task<string> ExportOrchestration(
            [OrchestrationTrigger] TaskOrchestrationContext context)
        {
            ILogger logger = context.CreateReplaySafeLogger(nameof(ExportOrchestration));
            logger.LogInformation("Starting export activities.");
            var statusRespose = new HttpResponseMessage();
            var statusUrl = string.Empty;
            var import_body = string.Empty;

            try
            {
                ResponseModel exportResponse = await context.CallActivityAsync<ResponseModel>(nameof(ProcessExport));
                if (exportResponse.Status == ResponseStatus.Completed)
                {
                    logger?.LogInformation($"Export  returned: Success.");
                    statusUrl = exportResponse.Content;
                }
                else
                {
                    logger?.LogInformation($"Export Status check returned: Unsuccessful.");
                    throw new HttpFailureException($"Status: {exportResponse.Status} Response: {exportResponse.Content} ");
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
                        import_body = response.Content;
                        break;
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
            try
            {
                HttpMethod method = HttpMethod.Get;
                string query = GetQueryStringForExport();
                ResponseModel exportResponse = await _exportProcessor.CallProcess(method, string.Empty, _options.SourceUri, query, _options.SourceHttpClient);
                return exportResponse;
            }
            catch
            {
                throw;
            }
        }

        [Function(nameof(ProcessExportStatusCheck))]
        public async Task<ResponseModel> ProcessExportStatusCheck([ActivityTrigger] string exportStatusUrl, FunctionContext executionContext)
        {
            try
            {
                if (!string.IsNullOrEmpty(exportStatusUrl))
                {
                    ResponseModel exportStatusResponse = await _exportProcessor.CheckProcessStatus(exportStatusUrl, _options.SourceUri, _options.SourceHttpClient);
                    return exportStatusResponse;
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
        }

        private string GetQueryStringForExport()
        {
            string query = $"?_since={_options.StartDate.ToString("yyyy-MM-dd")}&_till={DateTime.Now.ToString("yyyy-MM-dd")}";
            return $"/$export{query}";
        }
    }
}
