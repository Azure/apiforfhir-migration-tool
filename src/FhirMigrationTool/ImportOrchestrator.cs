// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using FhirMigrationTool.ImportProcess;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace FhirMigrationTool
{
    public class ImportOrchestrator
    {
        private readonly IImportProcessor _importProcessor;

        public ImportOrchestrator(IImportProcessor importProcessor)
        {
            _importProcessor = importProcessor;
        }

        [Function(nameof(ImportOrchestration))]
        public static async Task<string> ImportOrchestration(
            [OrchestrationTrigger] TaskOrchestrationContext context, string requestContent)
        {
            ILogger logger = context.CreateReplaySafeLogger(nameof(ImportOrchestration));
            logger.LogInformation("Starting import activities.");
            var output = string.Empty;

            output = await context.CallActivityAsync<string>(nameof(ProcessImport), requestContent);

            return output;
        }

        [Function(nameof(ProcessImport))]
        public async Task<string> ProcessImport([ActivityTrigger] string requestContent, FunctionContext executionContext)
        {
            string importStatus;
            try
            {
                var importStatusUrl = await _importProcessor.Execute(requestContent);
                importStatus = !string.IsNullOrEmpty(importStatusUrl)
                    ? await _importProcessor.CheckImportStatus(importStatusUrl)
                    : throw new Exception($"Import status Url was not received in export response.");

                return importStatus;
            }
            catch
            {
                throw;
            }
        }
    }
}
