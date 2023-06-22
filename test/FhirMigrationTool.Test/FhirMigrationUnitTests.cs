// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Net;
using System.Reflection;
using FhirMigrationTool;
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
using Moq;

namespace FhirMigrationtool.Tests
{
    [TestClass]
    public class FhirMigrationUnitTests
    {
        private static MigrationOptions? _config;
        private static ILogger<FhirMigrationtoolTests>? _logger;
        private static ILogger? _loggerExport;
        private static ILogger? _loggerImport;
        private static TelemetryClient? telemetryClient;
        private static IFhirClient? fhirClient;
        private static IOrchestrationHelper? orchestrationHelper;
        private readonly Mock<IFhirClient> _mockClient;
        private readonly Mock<IExportProcessor> _exportProcessor;
        private readonly Mock<IImportProcessor> _importProcessor;

        public FhirMigrationUnitTests()
        {
            _mockClient = new Mock<IFhirClient>();
            _exportProcessor = new Mock<IExportProcessor>();
            _importProcessor = new Mock<IImportProcessor>();
        }

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
            if (factory is not null)
            {
                var fhirLogger = factory.CreateLogger<FhirClient>();
                var httpClientFactory = serviceProvider.GetService<IHttpClientFactory>();
                var tokenCache = serviceProvider.GetService<IBearerTokenHelper>();
                orchestrationHelper = new OrchestrationHelper();

                if (httpClientFactory is not null && tokenCache is not null)
                {
                    fhirClient = new FhirClient(httpClientFactory, tokenCache, fhirLogger, _config);

                    using ILoggerFactory loggerFactory = LoggerFactory.Create(loggingBuilder => loggingBuilder
                        .SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace)
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
            }
        }

        [TestMethod]
        public async Task ExportProcessorTestCasePass()
        {
            var exportStatusUrl = "fhirserver/export/exportId";
            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.Accepted,
                ReasonPhrase = "Export request accepted.",
                Content =
                {
                    Headers =
                    {
                        { "Content-Location", exportStatusUrl },
                    },
                },
            };

            _mockClient.Setup(c => c.Send(It.IsAny<HttpRequestMessage>(), It.IsAny<Uri>(), It.IsAny<string>())).Returns(Task.FromResult(response));

            try
            {
                string result = string.Empty;

#pragma warning disable CS8604 // Possible null reference argument.
                IExportProcessor exportProcessor = new ExportProcessor(
                    fhirClient: _mockClient.Object,
                    options: _config,
                    telemetryClient: telemetryClient,
                    logger: _loggerExport as ILogger<ExportProcessor>,
                    orchestrationHelper: orchestrationHelper);
                result = await exportProcessor.Execute();
                Assert.IsTrue(!string.IsNullOrEmpty(result));
            }
            catch (Exception ex)
            {
                _loggerExport.LogError($"Error occurred during test: {ex.Message}");
            }
#pragma warning restore CS8604 // Possible null reference argument.
        }

        [TestMethod]
        public async Task ExportStatusTestCaseCompleted()
        {
            var exportStatusUrl = "https://azureapiforfhir.azurehealthcareapis.com/_operations/export/container";
            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(await File.ReadAllTextAsync("../../../mock_export_status_response.json")),
            };

            _mockClient.Setup(c => c.Send(It.IsAny<HttpRequestMessage>(), It.IsAny<Uri>(), It.IsAny<string>())).Returns(Task.FromResult(response));

            try
            {
                string result = string.Empty;

#pragma warning disable CS8604 // Possible null reference argument.
                IExportProcessor exportProcessor = new ExportProcessor(
                    _mockClient.Object,
                    options: _config,
                    telemetryClient: telemetryClient,
                    logger: _loggerExport as ILogger<ExportProcessor>,
                    orchestrationHelper: orchestrationHelper);
                result = await exportProcessor.CheckExportStatus(exportStatusUrl);
                Assert.IsTrue(!string.IsNullOrEmpty(result));
            }
            catch (Exception ex)
            {
                _loggerExport.LogError($"Error occurred during test: {ex.Message}");
            }
#pragma warning restore CS8604 // Possible null reference argument.
        }

        [TestMethod]
        public async Task ImportProcessorTestCasePass()
        {
            var importStatusUrl = "fhirserver/import/importId";
            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.Accepted,
                ReasonPhrase = "Import request accepted.",
                Content =
                {
                    Headers =
                    {
                        { "Content-Location", importStatusUrl },
                    },
                },
            };
            string importRequest = await File.ReadAllTextAsync("../../../import_body.json");

            _mockClient.Setup(c => c.Send(It.IsAny<HttpRequestMessage>(), It.IsAny<Uri>(), It.IsAny<string>())).Returns(Task.FromResult(response));

            try
            {
                string result = string.Empty;

#pragma warning disable CS8604 // Possible null reference argument.
                IImportProcessor importProcessor = new ImportProcessor(
                    fhirClient: _mockClient.Object,
                    options: _config,
                    telemetryClient: telemetryClient,
                    logger: _loggerImport as ILogger<ImportProcessor>);
                result = await importProcessor.Execute(importRequest);
                Assert.IsTrue(!string.IsNullOrEmpty(result));
            }
            catch (Exception ex)
            {
                _loggerImport.LogError($"Error occurred during test: {ex.Message}");
            }
#pragma warning restore CS8604 // Possible null reference argument.
        }

        [TestMethod]
        public async Task ImportStatusTestCaseCompleted()
        {
            var importStatusUrl = "https://fhirservice.fhir.azurehealthcareapis.com/_operations/import/importId";
            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(await File.ReadAllTextAsync("../../../mock_import_status_response.json")),
            };

            _mockClient.Setup(c => c.Send(It.IsAny<HttpRequestMessage>(), It.IsAny<Uri>(), It.IsAny<string>())).Returns(Task.FromResult(response));

            try
            {
                string result = string.Empty;

#pragma warning disable CS8604 // Possible null reference argument.
                IImportProcessor importProcessor = new ImportProcessor(
                    fhirClient: _mockClient.Object,
                    options: _config,
                    telemetryClient: telemetryClient,
                    logger: _loggerImport as ILogger<ImportProcessor>);
                result = await importProcessor.CheckImportStatus(importStatusUrl);
                Assert.IsTrue(!string.IsNullOrEmpty(result));
            }
            catch (Exception ex)
            {
                _loggerImport.LogError($"Error occurred during test: {ex.Message}");
            }
#pragma warning restore CS8604 // Possible null reference argument.
        }

        [TestMethod]
        public async Task MigrationTestPass()
        {
            var exportStatusUrl = "https://azureapiforfhir.azurehealthcareapis.com/_operations/export/container";
            var importStatusUrl = "https://fhirservice.fhir.azurehealthcareapis.com/_operations/import/importId";

            _exportProcessor.Setup(x => x.Execute()).Returns(Task.FromResult(exportStatusUrl));

            _exportProcessor.Setup(x => x.CheckExportStatus(It.IsAny<string>())).Returns(File.ReadAllTextAsync("../../../mock_import_request.json"));

            _importProcessor.Setup(x => x.Execute(It.IsAny<string>())).Returns(Task.FromResult(importStatusUrl));

            _importProcessor.Setup(x => x.CheckImportStatus(It.IsAny<string>())).Returns(Task.FromResult("Completed"));

            ExportOrchestrator exportOrchestrator = new ExportOrchestrator(_exportProcessor.Object);
            ImportOrchestrator importOrchestrator = new ImportOrchestrator(_importProcessor.Object);

            try
            {
                string result = string.Empty;

#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
                var exportResult = await exportOrchestrator.ProcessExport(null, null);

#pragma warning disable CS8604 // Possible null reference argument.
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

                Assert.AreEqual<string>("Completed", result);
            }
            catch (Exception ex)
            {
                _loggerExport.LogError($"Error occurred during test: {ex.Message}");
#pragma warning restore CS8604 // Possible null reference argument.
            }
        }

        [TestMethod]
        public async Task MigrationTestExportFail()
        {
            var importStatusUrl = "https://fhirservice.fhir.azurehealthcareapis.com/_operations/import/importId";

            _exportProcessor.Setup(x => x.Execute()).Returns(Task.FromResult(string.Empty));

            _exportProcessor.Setup(x => x.CheckExportStatus(It.IsAny<string>())).Returns(File.ReadAllTextAsync("../../../mock_import_request.json"));

            _importProcessor.Setup(x => x.Execute(It.IsAny<string>())).Returns(Task.FromResult(importStatusUrl));

            _importProcessor.Setup(x => x.CheckImportStatus(It.IsAny<string>())).Returns(Task.FromResult("Completed"));

            ExportOrchestrator exportOrchestrator = new ExportOrchestrator(_exportProcessor.Object);
            ImportOrchestrator importOrchestrator = new ImportOrchestrator(_importProcessor.Object);

            try
            {
                string result = string.Empty;
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
                var exportResult = await exportOrchestrator.ProcessExport(null, null);

#pragma warning disable CS8604 // Possible null reference argument.
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
#pragma warning restore CS8604 // Possible null reference argument.
                Assert.AreEqual<string>("Export status Url was not received in export response.", ex.Message);
            }
        }

        [TestMethod]
        public async Task MigrationTestExportStatusFail()
        {
            var exportStatusUrl = "https://azureapiforfhir.azurehealthcareapis.com/_operations/export/container";
            var importStatusUrl = "https://fhirservice.fhir.azurehealthcareapis.com/_operations/import/importId";

            _exportProcessor.Setup(x => x.Execute()).Returns(Task.FromResult(exportStatusUrl));

            _exportProcessor.Setup(x => x.CheckExportStatus(It.IsAny<string>())).Returns(Task.FromResult(string.Empty));

            _importProcessor.Setup(x => x.Execute(It.IsAny<string>())).Returns(Task.FromResult(importStatusUrl));

            _importProcessor.Setup(x => x.CheckImportStatus(It.IsAny<string>())).Returns(Task.FromResult("Completed"));

            ExportOrchestrator exportOrchestrator = new ExportOrchestrator(_exportProcessor.Object);
            ImportOrchestrator importOrchestrator = new ImportOrchestrator(_importProcessor.Object);

            try
            {
                string result = string.Empty;

#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
                var exportResult = await exportOrchestrator.ProcessExport(null, null);

#pragma warning disable CS8604 // Possible null reference argument.
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
                        _loggerExport.LogError($"Export status check failed.");
                    }
                }
                else
                {
                    _loggerExport.LogError($"Error occurred during ProcessExport.");
                }

                Assert.IsTrue(string.IsNullOrEmpty(result));
            }
            catch (Exception ex)
            {
                _loggerExport.LogError($"Error occurred during test: {ex.Message}");
#pragma warning restore CS8604 // Possible null reference argument.
            }
        }

        [TestMethod]
        public async Task MigrationTestImportFail()
        {
            var exportStatusUrl = "https://azureapiforfhir.azurehealthcareapis.com/_operations/export/container";

            _exportProcessor.Setup(x => x.Execute()).Returns(Task.FromResult(exportStatusUrl));

            _exportProcessor.Setup(x => x.CheckExportStatus(It.IsAny<string>())).Returns(File.ReadAllTextAsync("../../../mock_import_request.json"));

            _importProcessor.Setup(x => x.Execute(It.IsAny<string>())).Returns(Task.FromResult(string.Empty));

            _importProcessor.Setup(x => x.CheckImportStatus(It.IsAny<string>())).Returns(Task.FromResult("Completed"));

            ExportOrchestrator exportOrchestrator = new ExportOrchestrator(_exportProcessor.Object);
            ImportOrchestrator importOrchestrator = new ImportOrchestrator(_importProcessor.Object);

            try
            {
                string result = string.Empty;

#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
                var exportResult = await exportOrchestrator.ProcessExport(null, null);

#pragma warning disable CS8604 // Possible null reference argument.
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
#pragma warning restore CS8604 // Possible null reference argument.
                Assert.AreEqual<string>("Import status Url was not received in export response.", ex.Message);
            }
        }

        [TestMethod]
        public async Task MigrationTestImportStatusFail()
        {
            var exportStatusUrl = "https://azureapiforfhir.azurehealthcareapis.com/_operations/export/container";
            var importStatusUrl = "https://fhirservice.fhir.azurehealthcareapis.com/_operations/import/importId";

            _exportProcessor.Setup(x => x.Execute()).Returns(Task.FromResult(exportStatusUrl));

            _exportProcessor.Setup(x => x.CheckExportStatus(It.IsAny<string>())).Returns(File.ReadAllTextAsync("../../../mock_import_request.json"));

            _importProcessor.Setup(x => x.Execute(It.IsAny<string>())).Returns(Task.FromResult(importStatusUrl));

            _importProcessor.Setup(x => x.CheckImportStatus(It.IsAny<string>())).Returns(Task.FromResult(string.Empty));

            ExportOrchestrator exportOrchestrator = new ExportOrchestrator(_exportProcessor.Object);
            ImportOrchestrator importOrchestrator = new ImportOrchestrator(_importProcessor.Object);

            try
            {
                string result = string.Empty;

#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
                var exportResult = await exportOrchestrator.ProcessExport(null, null);

#pragma warning disable CS8604 // Possible null reference argument.
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
                        _loggerExport.LogError($"Import status check failed.");
                    }
                }
                else
                {
                    _loggerExport.LogError($"Error occurred during ProcessExport.");
                }

                Assert.IsTrue(string.IsNullOrEmpty(result));
            }
            catch (Exception ex)
            {
                _loggerExport.LogError($"Error occurred during test: {ex.Message}");
                Assert.AreEqual<string>("Import status Url was not received in export response.", ex.Message);
            }
#pragma warning restore CS8604 // Possible null reference argument.
        }
    }
}
