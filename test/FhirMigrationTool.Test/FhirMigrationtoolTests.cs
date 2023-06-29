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
using FhirMigrationTool.Security;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.ApplicationInsights;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace FhirMigrationtool.Tests
{
    [TestClass]
    public class FhirMigrationtoolTests
    {
        private static MigrationOptions? _config;
        private static ILogger<FhirMigrationtoolTests>? _logger;
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
                                     .AddScoped<IBearerTokenHelper, BearerTokenHelper>()
                                     .AddScoped<IFhirClient, FhirClient>()
                                     .BuildServiceProvider();

            var factory = serviceProvider.GetService<ILoggerFactory>();

#pragma warning disable CS8604 // Possible null reference argument.
            ILogger<FhirClient> fhirLogger = factory.CreateLogger<FhirClient>();
            IHttpClientFactory? httpClientFactory = serviceProvider.GetService<IHttpClientFactory>();
            IBearerTokenHelper? tokenCache = serviceProvider.GetService<IBearerTokenHelper>();
            IBearerTokenHelper? importTokenCache = serviceProvider.GetService<IBearerTokenHelper>();
            orchestrationHelper = new OrchestrationHelper();

            fhirClient = new FhirClient(httpClientFactory, tokenCache, fhirLogger, _config);
            importFhirClient = new FhirClient(httpClientFactory, importTokenCache, fhirLogger, _config);

            using ILoggerFactory loggerFactory = LoggerFactory.Create(loggingBuilder => loggingBuilder
                .SetMinimumLevel(LogLevel.Trace)
                .AddConsole());

            _logger = loggerFactory.CreateLogger<FhirMigrationtoolTests>();
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
                string result = string.Empty;
                result = await exportProcessor.Execute();
                _logger.LogError($"Export result: {result}");
                Assert.IsTrue(result.Contains("_operations/export/"));
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
                string result = string.Empty;
                string json = await File.ReadAllTextAsync("../../../import_body.json");
                result = await importProcessor.Execute(json);
                _logger.LogError($"Import result: {result}");
                Assert.IsTrue(result.Contains("_operations/import/"));
            }
            catch (Exception ex)
            {
                _loggerImport.LogError($"Error occurred during test: {ex.Message}");
            }
        }

        /*
        [TestMethod]
        public async Task MigrationTest()
        {
            IExportProcessor exportProcessor = new ExportProcessor(fhirClient, _config, telemetryClient, _loggerExport as ILogger<ExportProcessor>, orchestrationHelper);

            ExportOrchestrator exportOrchestrator = new ExportOrchestrator(exportProcessor);

            IImportProcessor importProcessor = new ImportProcessor(importFhirClient, _config, telemetryClient, _loggerImport as ILogger<ImportProcessor>);

            ImportOrchestrator importOrchestrator = new ImportOrchestrator(importProcessor);

            try
            {
                string result = string.Empty;
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
                var exportResult = await exportOrchestrator.ProcessExport(name: null, executionContext: null);

                _logger.LogInformation($"Export result: {exportResult}");
                if (!string.IsNullOrEmpty(exportResult))
                {
                    result = await importOrchestrator.ProcessImport(exportResult, null);
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
                    if (!string.IsNullOrEmpty(result))
                    {
                        _logger.LogInformation($"Import result: {result}");
                    }
                    else
                    {
                        _loggerExport.LogError($"Error occurred during ProcessImport.");
                    }
                }
                else
                {
                    _loggerExport.LogError($"Error occurred during ProcessExport.");
                }

                Assert.IsTrue(!string.IsNullOrEmpty(result));
            }
            catch (Exception ex)
            {
                _loggerExport.LogError($"Error occurred during test: {ex.Message}");
            }
#pragma warning restore CS8604 // Possible null reference argument.
        }*/
    }
}
