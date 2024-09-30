// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using ApiForFhirMigrationTool.Function.Configuration;
using ApiForFhirMigrationTool.Function.FhirOperation;
using ApiForFhirMigrationTool.Function.Models;
using ApiForFhirMigrationTool.Function.OrchestrationHelper;
using ApiForFhirMigrationTool.Function.Processors;
using Azure;
using Azure.Data.Tables;
using Microsoft.ApplicationInsights;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace ApiForFhirMigrationTool.Function
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
        private readonly IMetadataStore _azureTableMetadataStore;

        public MigrationOrchestrator(MigrationOptions options, ILoggerFactory loggerFactory, IOrchestrationHelper orchestrationHelper, IMetadataStore azureTableMetadataStore, IAzureTableClientFactory azureTableClientFactory, IFhirProcessor exportProcessor, IFhirClient fhirClient, TelemetryClient telemetryClient)
        {
            _options = options;
            _logger = loggerFactory.CreateLogger<MigrationOrchestrator>();
            _orchestrationHelper = orchestrationHelper;
            _azureTableClientFactory = azureTableClientFactory;
            _exportProcessor = exportProcessor;
            _fhirClient = fhirClient;
            _telemetryClient = telemetryClient;
            _azureTableMetadataStore = azureTableMetadataStore;
        }

        [Function(nameof(MigrationOrchestration))]
        public async Task<List<string>> MigrationOrchestration(
            [OrchestrationTrigger] TaskOrchestrationContext context)
        {
            ILogger logger = context.CreateReplaySafeLogger(nameof(MigrationOrchestration));
            var outputs = new List<string>();

            try
            {
                bool shouldRun = true;

                if (_options.StopDm)
                {
                    logger.LogInformation("Data Migration Tool will pause during business hours.");
                    var currentTime = DateTime.UtcNow;
                    var startHour = new TimeSpan(_options.StartTime - 1, 0, 0);
                    var endHour = new TimeSpan(_options.EndTime, 30, 0);
                    logger.LogInformation($" Current time : ({currentTime}), startHour :({startHour}), endHour :({endHour})");
                    if (currentTime.TimeOfDay > startHour && currentTime.TimeOfDay < endHour)
                    {
                        shouldRun = false;
                        logger.LogInformation("Execution skipped: Current time is outside allowed hours");
                    }
                }
                if (shouldRun)
                {

                    _options.ValidateConfig();
                    logger.LogInformation("Start MigrationOrchestration.");
                    logger.LogInformation("Creating table client");
                    TableClient chunktableClient = _azureTableClientFactory.Create(_options.ChunkTableName);
                    logger.LogInformation("Table client created successfully.");

                    if (_options.IsParallel == true)
                    {
                        Pageable<TableEntity> jobList = chunktableClient.Query<TableEntity>();
                        if (jobList.Count() <= 0)
                        {
                            var tableEntity = new TableEntity(_options.PartitionKey, _options.RowKey)
                        {
                            { "JobId", 0 },
                            {"SurfaceJobId",0 },
                            {"DeepJobId",0 },
                            {"ImportId",0 },
                            {"SearchParameterMigration", false }
                        };
                            logger.LogInformation("Starting update of the chunk table.");
                            _azureTableMetadataStore.AddEntity(chunktableClient, tableEntity);
                            logger.LogInformation("Completed update of the chunk table.");
                        }
                    }
                    else
                    {
                        Pageable<TableEntity> jobList = chunktableClient.Query<TableEntity>();
                        if (jobList.Count() <= 0)
                        {
                            var tableEntity = new TableEntity(_options.PartitionKey, _options.RowKey)
                        {
                            { "JobId", 0 },
                            {"SurfaceJobId",0 },
                            {"DeepJobId",0 },
                            { "globalSinceExportType", "" },
                            { "globalTillExportType", "" },
                            { "noOfResources", _options.ResourceTypes?.Count() },
                            { "resourceTypeIndex", 0 },
                            { "multiExport", "" },
                             {"ImportId",0 },
                             {"SearchParameterMigration", false }
                        };
                            logger.LogInformation("Starting update of the chunk table.");
                            _azureTableMetadataStore.AddEntity(chunktableClient, tableEntity);
                            logger.LogInformation("Completed update of the chunk table.");
                        }
                    }
                    var options = TaskOptions.FromRetryPolicy(new RetryPolicy(
                            maxNumberOfAttempts: 3,
                            firstRetryInterval: TimeSpan.FromSeconds(5)));

                    logger.LogInformation("Starting SearchParameter migration activities.");
                    //Run sub orchestration for search parameter
                    var searchParameter = await context.CallSubOrchestratorAsync<string>("SearchParameterOrchestration", options: options);
                    logger.LogInformation("SearchParameter migration activities ended");

                    // Run sub orchestration for export and export status
                    logger.LogInformation("Starting Export migration activities.");
                    var exportContent = await context.CallSubOrchestratorAsync<string>("ExportOrchestration", options: options);
                    logger.LogInformation("Export migration activities ended.");

                    logger.LogInformation("Starting Export Status activities");
                    var exportStatusContent = await context.CallSubOrchestratorAsync<string>("ExportStatusOrchestration", options: options);
                    logger.LogInformation("Export Status activities ended.");

                    // Run sub orchestration for Import and Import status
                    logger.LogInformation("Starting Import  migration  activities.");
                    var import = await context.CallSubOrchestratorAsync<string>("ImportOrchestration", options: options);
                    logger.LogInformation("Import migration activities ended.");

                    logger.LogInformation("Starting Import Status activities.");
                    var importStatus = await context.CallSubOrchestratorAsync<string>("ImportStatusOrchestration", options: options);
                    logger.LogInformation("Import Status activities ended.");
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
            try
            {
                var instanceId = await client.ScheduleNewOrchestrationInstanceAsync(nameof(MigrationOrchestration), options);
                _logger.LogInformation("Started: Timed {instanceId}...", instanceId);
            }
            catch (Exception ex)
            {
                _logger.LogInformation($"Error in starting instance due to {ex.Message}");
            }
        }
    }
}
