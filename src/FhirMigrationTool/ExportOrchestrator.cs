// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using FhirMigrationTool.ExportProcess;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace FhirMigrationTool
{
    public class ExportOrchestrator
    {
        private readonly IExportProcessor _exportProcessor;

        public ExportOrchestrator(IExportProcessor exportProcessor)
        {
            _exportProcessor = exportProcessor;
        }

        [Function(nameof(ExportOrchestration))]
        public static async Task<string> ExportOrchestration(
            [OrchestrationTrigger] TaskOrchestrationContext context)
        {
            ILogger logger = context.CreateReplaySafeLogger(nameof(ExportOrchestration));
            logger.LogInformation("Starting export activities.");
            var output = string.Empty;

            output = await context.CallActivityAsync<string>(nameof(ProcessExport));

            return output;
        }

        [Function(nameof(ProcessExport))]
        public async Task<string> ProcessExport([ActivityTrigger] string name, FunctionContext executionContext)
        {
            string? exportStatus;
            try
            {
                var exportStatusUrl = await _exportProcessor.Execute();

                if (!string.IsNullOrEmpty(exportStatusUrl))
                {
                    exportStatus = await _exportProcessor.CheckExportStatus(exportStatusUrl);
                }
                else
                {
                    throw new Exception($"Export status Url was not received in export response.");
                }

                return exportStatus;
            }
            catch
            {
                throw;
            }
        }
    }
}
