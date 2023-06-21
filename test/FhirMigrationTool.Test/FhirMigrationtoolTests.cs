using fhir_migration_tool;
using fhir_migration_tool.Configuration;
using fhir_migration_tool.ExportProcess;
using fhir_migration_tool.FhirOperation;
using fhir_migration_tool.ImportProcess;
using fhir_migration_tool.OrchestrationHelper;
using fhir_migration_tool.Security;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.ApplicationInsights;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Reflection;
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
                                         builder.AddFilter<ApplicationInsightsLoggerProvider>("", LogLevel.Information);
                                         builder.AddApplicationInsights(op => op.ConnectionString = _config.AppInsightConnectionstring, op => op.FlushOnDispose = true);
                                     })
                                     .AddHttpClient()
                                     .AddScoped<IBearerTokenHelper, BearerTokenHelper>()
                                     .AddScoped<IFhirClient, FhirClient>()
                                     .BuildServiceProvider();

            var factory = serviceProvider.GetService<ILoggerFactory>();

            
            var fhirLogger = factory.CreateLogger<FhirClient>();

            var httpClientFactory = serviceProvider.GetService<IHttpClientFactory>();
            var tokenCache = serviceProvider.GetService<IBearerTokenHelper>();
            orchestrationHelper = new OrchestrationHelper();

            fhirClient = new FhirClient(httpClientFactory, tokenCache, fhirLogger, _config);

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
            root.Bind(orchestrationHelper);
        }

        [TestMethod]
        public async Task ExportProcessTest()
        {
            IExportProcessor exportProcessor = new ExportProcessor(fhirClient, _config, telemetryClient, (ILogger<ExportProcessor>)_loggerExport, orchestrationHelper);

            try
            {
                string result = "";
                result = await exportProcessor.Execute();
                _logger.LogError($"Export result: {result}");
                Assert.IsNotNull(result);
            }
            catch (Exception ex)
            {
                _loggerExport.LogError($"Error occurred during test: {ex.Message}");
            }
        }

        [TestMethod]
        public async Task ImportProcessTest()
        {
            IImportProcessor importProcessor = new ImportProcessor(fhirClient, _config, telemetryClient, (ILogger<ImportProcessor>)_loggerImport);

            try
            {
                string json = await File.ReadAllTextAsync("../../../import_body.json");
                string result = "";

                result = await importProcessor.Execute(json);
                _logger.LogError($"Import result: {result}");
                Assert.IsNotNull(result);
            }
            catch (Exception ex)
            {
                _loggerImport.LogError($"Error occurred during test: {ex.Message}");
            }
        }

        [TestMethod]
        public async Task MigrationTest()
        {

            IExportProcessor exportProcessor = new ExportProcessor(fhirClient, _config, telemetryClient, (ILogger<ExportProcessor>)_loggerExport, orchestrationHelper);

            ExportOrchestrator exportOrchestrator = new ExportOrchestrator(exportProcessor);

            IImportProcessor importProcessor = new ImportProcessor(fhirClient, _config, telemetryClient, (ILogger<ImportProcessor>)_loggerImport);

            ImportOrchestrator importOrchestrator = new ImportOrchestrator(importProcessor);

            try
            {
                string result = "";
                var exportResult = await exportOrchestrator.ProcessExport(null, null);
                _logger.LogInformation($"Export result: {exportResult}");
                if (!string.IsNullOrEmpty(exportResult))
                {
                    result = await importOrchestrator.ProcessImport(exportResult, null);
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
        }

    }
}
