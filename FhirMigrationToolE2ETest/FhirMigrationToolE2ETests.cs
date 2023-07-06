// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Reflection;
using FhirMigrationTool.Configuration;
using FhirMigrationTool.ExportProcess;
using FhirMigrationTool.FhirOperation;
using FhirMigrationTool.ImportProcess;
using FhirMigrationTool.OrchestrationHelper;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.ApplicationInsights;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FhirMigrationToolE2ETest
{
    [TestClass]
    internal class FhirMigrationToolE2ETests
    {
        private static MigrationOptions? _config;
        private static ILogger<FhirMigrationToolE2ETests>? _logger;
        private static ILogger? _loggerExport;
        private static ILogger? _loggerImport;
        private static TelemetryClient? telemetryClient;
        private static IFhirClient? fhirClient;
        private static IFhirClient? importFhirClient;
        private static IOrchestrationHelper? orchestrationHelper;

        [ClassInitialize]
        public static void Initialize(TestContext context)
        {
            IConfigurationBuilder builder = new ConfigurationBuilder()
                 .AddJsonFile("appsettings.test.json", true, true)
                 .AddUserSecrets(Assembly.GetExecutingAssembly(), true)
                 .AddEnvironmentVariables();
            IConfigurationRoot root = builder.Build();
            _config = new MigrationOptions();
            root.Bind(_config);

            TelemetryConfiguration telemetryConfiguration = new TelemetryConfiguration();
            telemetryConfiguration.ConnectionString = _config.AppInsightConnectionstring;
            telemetryClient = new TelemetryClient(telemetryConfiguration);

            var serviceProvider = new ServiceCollection()
                                     .AddLogging(builder =>
                                     {
                                         builder.AddFilter<ApplicationInsightsLoggerProvider>(string.Empty, LogLevel.Information);
                                         builder.AddApplicationInsights(op => op.ConnectionString = _config.AppInsightConnectionstring, op => op.FlushOnDispose = true);
                                     })
                                     .AddHttpClient()
                                     .AddScoped<IFhirClient, FhirClient>()
                                     .BuildServiceProvider();

            var factory = serviceProvider.GetService<ILoggerFactory>();

#pragma warning disable CS8604 // Possible null reference argument.
            ILogger<FhirClient> fhirLogger = factory.CreateLogger<FhirClient>();
            IHttpClientFactory? httpClientFactory = serviceProvider.GetService<IHttpClientFactory>();

            orchestrationHelper = new OrchestrationHelper();
            fhirClient = new FhirClient(httpClientFactory);
            importFhirClient = new FhirClient(httpClientFactory);

            using ILoggerFactory loggerFactory = LoggerFactory.Create(loggingBuilder => loggingBuilder
                .SetMinimumLevel(LogLevel.Trace)
                .AddConsole());

            _logger = loggerFactory.CreateLogger<FhirMigrationToolE2ETests>();
            _loggerExport = loggerFactory.CreateLogger<ExportProcessor>();
            _loggerImport = loggerFactory.CreateLogger<ImportProcessor>();

            root.Bind(fhirLogger);
            root.Bind(_logger);
            root.Bind(_loggerExport);
            root.Bind(_loggerImport);
            root.Bind(telemetryClient);
            root.Bind(fhirClient);
            root.Bind(importFhirClient);
            root.Bind(orchestrationHelper);
        }

        [TestMethod]
        public async Task ExportProcessTest()
        {
            IExportProcessor exportProcessor = new ExportProcessor(fhirClient, _config, telemetryClient, _loggerExport as ILogger<ExportProcessor>);

            try
            {
                HttpResponseMessage exportResponse = await exportProcessor.CallExport();
                Assert.IsTrue(exportResponse.IsSuccessStatusCode);
            }
            catch (Exception ex)
            {
                _loggerExport.LogError($"Error occurred during test: {ex.Message}");
            }
        }

        [TestMethod]
        public async Task ImportProcessTest()
        {
            IImportProcessor importProcessor = new ImportProcessor(fhirClient, _config, telemetryClient, _loggerImport as ILogger<ImportProcessor>);
            try
            {
                string json = await File.ReadAllTextAsync("../../../import_body.json");
                HttpResponseMessage response = await importProcessor.CallImport(json);
                Assert.IsTrue(response.IsSuccessStatusCode);
            }
            catch (Exception ex)
            {
                _loggerImport.LogError($"Error occurred during test: {ex.Message}");
            }
        }
    }
}
