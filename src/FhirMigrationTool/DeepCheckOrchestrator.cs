// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using FhirMigrationTool.Configuration;
using FhirMigrationTool.DeepCheck;
using FhirMigrationTool.FhirOperation;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace FhirMigrationTool
{
    public class DeepCheckOrchestrator
    {
        private readonly IDeepCheck _deepCheck;
        private readonly MigrationOptions _options;
        private readonly IFhirClient _fhirClient;
        private readonly ILogger _logger;

        public DeepCheckOrchestrator(IFhirClient fhirClient, IDeepCheck deepCheck, MigrationOptions options, ILogger<DeepCheckOrchestrator> logger)
        {
            _deepCheck = deepCheck;
            _options = options;
            _fhirClient = fhirClient;
            _logger = logger;
        }

        [Function(nameof(DeepCheckOrchestration))]
        public static async Task<string> DeepCheckOrchestration(
            [OrchestrationTrigger] TaskOrchestrationContext context)
        {
            ILogger logger = context.CreateReplaySafeLogger(nameof(DeepCheckOrchestration));
            logger.LogInformation("Starting Deep Check activities.");
            var output = string.Empty;

            output = await context.CallActivityAsync<string>(nameof(DeepResourceCheck));
            return output;
        }

        [Function(nameof(DeepResourceCheck))]
        public async Task<string> DeepResourceCheck([ActivityTrigger] FunctionContext executionContext)
        {
            try
            {
                var deepcheckstatus = await _deepCheck.Execute();
                return deepcheckstatus;
            }
            catch
            {
                throw;
            }
        }
    }
}
