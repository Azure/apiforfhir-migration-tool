// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Reflection;
using FhirMigrationTool.Configuration;
using FhirMigrationTool.FhirOperation;
using FhirMigrationTool.Models;
using FhirMigrationTool.OrchestrationHelper;
using FhirMigrationTool.Processors;
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
#pragma warning disable CS0649 // Field 'FhirMigrationToolE2ETests._loggerExport' is never assigned to, and will always have its default value null
        private static readonly ILogger? _loggerExport;
        private static ILogger? _loggerImport;
#pragma warning restore CS0649 // Field 'FhirMigrationToolE2ETests._loggerExport' is never assigned to, and will always have its default value null
        private static TelemetryClient? telemetryClient;
        private static IFhirClient? exportFhirClient;
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
            exportFhirClient = new FhirClient(httpClientFactory);
            importFhirClient = new FhirClient(httpClientFactory);

            using ILoggerFactory loggerFactory = LoggerFactory.Create(loggingBuilder => loggingBuilder
                .SetMinimumLevel(LogLevel.Trace)
                .AddConsole());

            _logger = loggerFactory.CreateLogger<FhirMigrationToolE2ETests>();

            root.Bind(fhirLogger);
            root.Bind(_logger);
            root.Bind(_loggerExport);
            root.Bind(_loggerImport);
            root.Bind(telemetryClient);
            root.Bind(exportFhirClient);
            root.Bind(importFhirClient);
            root.Bind(orchestrationHelper);
        }

        [TestMethod]
        public async Task ExportProcessTest()
        {
            IFhirProcessor exportProcessor = new FhirProcessor(exportFhirClient, telemetryClient, _loggerExport as ILogger<FhirProcessor>);

            try
            {
                var method = HttpMethod.Get;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                ResponseModel exportResponse = await exportProcessor.CallProcess(method, string.Empty, _config.SourceUri, "/$export", _config.SourceHttpClient);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                Assert.IsTrue(!string.IsNullOrEmpty(exportResponse.Content));
            }
            catch (Exception ex)
            {
                _loggerExport.LogError($"Error occurred during test: {ex.Message}");
            }
        }

        [TestMethod]
        public async Task ImportProcessTest()
        {
            IFhirProcessor importProcessor = new FhirProcessor(exportFhirClient, telemetryClient, _loggerImport as ILogger<FhirProcessor>);
            try
            {
                string json = await File.ReadAllTextAsync("../../../import_body.json");
                var method = HttpMethod.Post;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                ResponseModel response = await importProcessor.CallProcess(method, json, _config.DestinationUri, "/$import", _config.DestinationHttpClient);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                Assert.IsTrue(!string.IsNullOrEmpty(response.Content));
            }
            catch (Exception ex)
            {
                _loggerImport.LogError($"Error occurred during test: {ex.Message}");
            }
        }
    }
}
