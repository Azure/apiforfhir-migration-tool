// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using Azure;
using Azure.Data.Tables;
using FhirMigrationTool.Configuration;
using FhirMigrationTool.FhirOperation;
using FhirMigrationTool.Models;
using FhirMigrationTool.OrchestrationHelper;
using FhirMigrationTool.Processors;
using Microsoft.ApplicationInsights;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace FhirMigrationTool
{
    public class MigrationOrchestrator
    {
        private readonly MigrationOptions _options;
        private readonly ILogger _logger;
        private readonly IOrchestrationHelper _orchestrationHelper;
        private readonly IAzureTableClientFactory _azureTableClientFactory;
        private readonly IFhirProcessor _exportProcessor;
        private readonly IFhirClient _fhirClient;
        private readonly TelemetryClient _telemetryClient;

        public MigrationOrchestrator(MigrationOptions options, ILoggerFactory loggerFactory, IOrchestrationHelper orchestrationHelper, IAzureTableClientFactory azureTableClientFactory, IFhirProcessor exportProcessor, IFhirClient fhirClient, TelemetryClient telemetryClient)
        {
            _options = options;
            _logger = loggerFactory.CreateLogger<MigrationOrchestrator>();
            _orchestrationHelper = orchestrationHelper;
            _azureTableClientFactory = azureTableClientFactory;
            _exportProcessor = exportProcessor;
            _fhirClient = fhirClient;
            _telemetryClient = telemetryClient;
        }

        [Function(nameof(MigrationOrchestration))]
        public async Task<List<string>> MigrationOrchestration(
            [OrchestrationTrigger] TaskOrchestrationContext context)
        {
            ILogger logger = context.CreateReplaySafeLogger(nameof(MigrationOrchestration));
            var outputs = new List<string>();

            try
            {
                _options.ValidateConfig();
                logger.LogInformation("Start MigrationOrchestration.");
                TableClient exportTableClient = _azureTableClientFactory.Create(_options.ExportTableName);

                // Run sub orchestration for export
                Pageable<TableEntity> jobList = exportTableClient.Query<TableEntity>(filter: ent => ent.GetString("IsExportRunning") == "Running" || ent.GetString("IsExportRunning") == "Started" || ent.GetString("IsImportRunning") == "Running" || ent.GetString("IsImportRunning") == "Started" || ent.GetString("IsImportRunning") == "Not Started");
                if (jobList.Count() <= 0)
                {
                    // Run Get and Post activity for search parameter
                    await context.CallActivityAsync("SearchParameterMigration");

                    var exportContent = await context.CallSubOrchestratorAsync<string>("ExportOrchestration");
                }

                Pageable<TableEntity> exportRunningjobList = exportTableClient.Query<TableEntity>(filter: ent => ent.GetString("IsExportRunning") == "Started" || ent.GetString("IsExportRunning") == "Running");
                if (exportRunningjobList.Count() > 0)
                {
                    var exportStatusContent = await context.CallSubOrchestratorAsync<string>("ExportStatusOrchestration");
                }

                // Run sub orchestration for Import
                Pageable<TableEntity> jobListimport = exportTableClient.Query<TableEntity>(filter: ent => ent.GetBoolean("IsExportComplete") == true && ent.GetString("ImportRequest") != string.Empty && ent.GetString("IsImportRunning") == "Not Started");
                if (jobListimport.Count() > 0)
                {
                    var import = await context.CallSubOrchestratorAsync<string>("ImportOrchestration");
                }

                Pageable<TableEntity> jobListimportRunning = exportTableClient.Query<TableEntity>(filter: ent => ent.GetString("IsImportRunning") == "Started" || ent.GetString("IsImportRunning") == "Running");
                if (jobListimportRunning.Count() > 0)
                {
                    var importStatus = await context.CallSubOrchestratorAsync<string>("ImportStatusOrchestration");
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"Error occurred during migration process: {ex.Message}");
            }

            return outputs;
        }

        [Function("TimerOrchestration")]
        public async Task Run(
         [TimerTrigger("0 */5 * * * *")] TimerInfo myTimer,
         [DurableClient] DurableTaskClient client,
         FunctionContext executionContext)
        {
            string instanceId_new = "FhirMigrationTool";
            StartOrchestrationOptions options = new StartOrchestrationOptions(instanceId_new);
            var instanceId = await client.ScheduleNewOrchestrationInstanceAsync(nameof(MigrationOrchestration), options);
            _logger.LogInformation("Started: Timed {instanceId}...", instanceId);
        }
    }
}
