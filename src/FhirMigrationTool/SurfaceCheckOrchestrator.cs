// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using FhirMigrationTool.Configuration;
using FhirMigrationTool.FhirOperation;
using FhirMigrationTool.SurfaceCheck;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace FhirMigrationTool
{
    public class SurfaceCheckOrchestrator
    {
        private readonly ISurfaceCheck _surfaceCheck;
        private readonly MigrationOptions _options;
        private readonly IFhirClient _fhirClient;
        private readonly ILogger _logger;

        public SurfaceCheckOrchestrator(IFhirClient fhirClient, ISurfaceCheck surfaceCheck, MigrationOptions options, ILogger<SurfaceCheckOrchestrator> logger)
        {
            _surfaceCheck = surfaceCheck;
            _options = options;
            _fhirClient = fhirClient;
            _logger = logger;
        }

        [Function(nameof(SurfaceCheckOrchestration))]
        public static async Task<string> SurfaceCheckOrchestration(
            [OrchestrationTrigger] TaskOrchestrationContext context)
        {
            ILogger logger = context.CreateReplaySafeLogger(nameof(SurfaceCheckOrchestration));
            logger.LogInformation("Starting Surface Check activities.");
            var output = string.Empty;

            output = await context.CallActivityAsync<string>(nameof(Count));

            return output;
        }

        [Function(nameof(Count))]
        public async Task<string> Count([ActivityTrigger] string query)
        {
            try
            {
                var surfacecheckstatus = await _surfaceCheck.Execute(query);

                return surfacecheckstatus;
            }
            catch
            {
                throw;
            }
        }
    }
}
