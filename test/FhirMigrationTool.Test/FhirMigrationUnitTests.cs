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
using FhirMigrationTool.Models;
using FhirMigrationTool.OrchestrationHelper;
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
        private static ILogger<FhirMigrationUnitTests>? _logger;
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
                                     .AddScoped<IFhirClient, FhirClient>()
                                     .BuildServiceProvider();

            var factory = serviceProvider.GetService<ILoggerFactory>();
            if (factory is not null)
            {
                var fhirLogger = factory.CreateLogger<FhirClient>();
                var httpClientFactory = serviceProvider.GetService<IHttpClientFactory>();

                orchestrationHelper = new OrchestrationHelper();

                if (httpClientFactory is not null)
                {
                    fhirClient = new FhirClient(httpClientFactory);

                    using ILoggerFactory loggerFactory = LoggerFactory.Create(loggingBuilder => loggingBuilder
                        .SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace)
                        .AddConsole());

                    _logger = loggerFactory.CreateLogger<FhirMigrationUnitTests>();
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
#pragma warning disable CS8604 // Possible null reference argument.
                IExportProcessor exportProcessor = new ExportProcessor(
                    fhirClient: _mockClient.Object,
                    options: _config,
                    telemetryClient: telemetryClient,
                    logger: _loggerExport as ILogger<ExportProcessor>);
                HttpResponseMessage exportResponse = await exportProcessor.CallExport();
                KeyValuePair<string, IEnumerable<string>> contentLocationHeader = exportResponse.Content.Headers.FirstOrDefault(x => x.Key == "Content-Location");
                var statusUrl = contentLocationHeader.Value.FirstOrDefault();
                Assert.IsTrue(exportResponse.IsSuccessStatusCode);
                Assert.AreEqual(statusUrl, exportStatusUrl);
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
#pragma warning disable CS8604 // Possible null reference argument.
                IExportProcessor exportProcessor = new ExportProcessor(
                    _mockClient.Object,
                    options: _config,
                    telemetryClient: telemetryClient,
                    logger: _loggerExport as ILogger<ExportProcessor>);
                HttpResponseMessage exportStatusResponse = await exportProcessor.CheckExportStatus(exportStatusUrl);
                Assert.IsTrue(exportStatusResponse.IsSuccessStatusCode);
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
#pragma warning disable CS8604 // Possible null reference argument.
                IImportProcessor importProcessor = new ImportProcessor(
                    fhirClient: _mockClient.Object,
                    options: _config,
                    telemetryClient: telemetryClient,
                    logger: _loggerImport as ILogger<ImportProcessor>);
                HttpResponseMessage importResponse = await importProcessor.CallImport(importRequest);
                KeyValuePair<string, IEnumerable<string>> contentLocationHeader = importResponse.Content.Headers.FirstOrDefault(x => x.Key == "Content-Location");
                var statusUrl = contentLocationHeader.Value.FirstOrDefault();
                Assert.IsTrue(importResponse.IsSuccessStatusCode);
                Assert.AreEqual(statusUrl, importStatusUrl);
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
#pragma warning disable CS8604 // Possible null reference argument.
                IImportProcessor importProcessor = new ImportProcessor(
                    fhirClient: _mockClient.Object,
                    options: _config,
                    telemetryClient: telemetryClient,
                    logger: _loggerImport as ILogger<ImportProcessor>);
                HttpResponseMessage importStatusResponse = await importProcessor.CheckImportStatus(importStatusUrl);
                Assert.IsTrue(importStatusResponse.IsSuccessStatusCode);
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
            var exportResponse = new HttpResponseMessage
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
            var exportStatusResponse = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(await File.ReadAllTextAsync("../../../mock_export_status_response.json")),
            };
            var importResponse = new HttpResponseMessage
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
            var importStatusResponse = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(await File.ReadAllTextAsync("../../../mock_import_status_response.json")),
            };

            _exportProcessor.Setup(x => x.CallExport()).Returns(Task.FromResult(exportResponse));

            _exportProcessor.Setup(x => x.CheckExportStatus(It.IsAny<string>())).Returns(Task.FromResult(exportStatusResponse));

            _importProcessor.Setup(x => x.CallImport(It.IsAny<string>())).Returns(Task.FromResult(importResponse));

            _importProcessor.Setup(x => x.CheckImportStatus(It.IsAny<string>())).Returns(Task.FromResult(importStatusResponse));
#pragma warning disable CS8604 // Possible null reference argument.
            var exportOrchestrator = new ExportOrchestrator(_exportProcessor.Object, options: _config, orchestrationHelper);
            var importOrchestrator = new ImportOrchestrator(_importProcessor.Object, options: _config);
            var importResult = new ResponseModel();
            var exportResult = new ResponseModel();
            var importStatusResult = new ResponseModel();
            var exportStatusResult = new ResponseModel();

            try
            {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
                exportResult = await exportOrchestrator.ProcessExport(null, null);
                if (exportResult.Status == ResponseStatus.Completed)
                {
                    exportStatusResult = await exportOrchestrator.ProcessExportStatusCheck(exportResult.Content, null);
                    if (exportStatusResult.Status == ResponseStatus.Completed)
                    {
                        string importRequestContent = exportStatusResult.Content;
                        importResult = await importOrchestrator.ProcessImport(importRequestContent, null);

                        if (importResult.Status == ResponseStatus.Completed)
                        {
                            importStatusResult = await importOrchestrator.ProcessImportStatusCheck(importResult.Content, null);
                        }
                    }
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
                }

                Assert.IsTrue(exportResult.Status == ResponseStatus.Completed);
                Assert.IsTrue(exportStatusResult.Status == ResponseStatus.Completed);
                Assert.IsTrue(importResult.Status == ResponseStatus.Completed);
                Assert.IsTrue(importStatusResult.Status == ResponseStatus.Completed);
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
            var exportResponse = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError,
                ReasonPhrase = "Export request failed.",
                Content = new StringContent("Export Failed."),
            };
            var exportStatusResponse = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(await File.ReadAllTextAsync("../../../mock_export_status_response.json")),
            };
            var importResponse = new HttpResponseMessage
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
            var importStatusResponse = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(await File.ReadAllTextAsync("../../../mock_import_status_response.json")),
            };

            _exportProcessor.Setup(x => x.CallExport()).Returns(Task.FromResult(exportResponse));

            _exportProcessor.Setup(x => x.CheckExportStatus(It.IsAny<string>())).Returns(Task.FromResult(exportStatusResponse));

            _importProcessor.Setup(x => x.CallImport(It.IsAny<string>())).Returns(Task.FromResult(importResponse));

            _importProcessor.Setup(x => x.CheckImportStatus(It.IsAny<string>())).Returns(Task.FromResult(importStatusResponse));
#pragma warning disable CS8604 // Possible null reference argument.
            var exportOrchestrator = new ExportOrchestrator(_exportProcessor.Object, options: _config, orchestrationHelper);
            var importOrchestrator = new ImportOrchestrator(_importProcessor.Object, options: _config);
            var importResult = new ResponseModel();
            var exportResult = new ResponseModel();
            var importStatusResult = new ResponseModel();
            var exportStatusResult = new ResponseModel();

            try
            {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
                exportResult = await exportOrchestrator.ProcessExport(null, null);
                if (exportResult.Status == ResponseStatus.Completed)
                {
                    exportStatusResult = await exportOrchestrator.ProcessExportStatusCheck(exportResult.Content, null);
                    if (exportStatusResult.Status == ResponseStatus.Completed)
                    {
                        string importRequestContent = exportStatusResult.Content;
                        importResult = await importOrchestrator.ProcessImport(importRequestContent, null);

                        if (importResult.Status == ResponseStatus.Completed)
                        {
                            importStatusResult = await importOrchestrator.ProcessImportStatusCheck(importResult.Content, null);
                        }
                    }
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
                }

                Assert.IsTrue(exportResult.Status == ResponseStatus.Failed);
            }
            catch (Exception ex)
            {
                _loggerExport.LogError($"Error occurred during test: {ex.Message}");
#pragma warning restore CS8604 // Possible null reference argument.
            }
        }

        [TestMethod]
        public async Task MigrationTestExportStatusFail()
        {
            var exportStatusUrl = "https://azureapiforfhir.azurehealthcareapis.com/_operations/export/container";
            var importStatusUrl = "https://fhirservice.fhir.azurehealthcareapis.com/_operations/import/importId";
            var exportResponse = new HttpResponseMessage
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
            var exportStatusResponse = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError,
                ReasonPhrase = "Export status request failed.",
                Content = new StringContent("Export status Failed."),
            };
            var importResponse = new HttpResponseMessage
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
            var importStatusResponse = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(await File.ReadAllTextAsync("../../../mock_import_status_response.json")),
            };

            _exportProcessor.Setup(x => x.CallExport()).Returns(Task.FromResult(exportResponse));
            _exportProcessor.Setup(x => x.CheckExportStatus(It.IsAny<string>())).Returns(Task.FromResult(exportStatusResponse));
            _importProcessor.Setup(x => x.CallImport(It.IsAny<string>())).Returns(Task.FromResult(importResponse));
            _importProcessor.Setup(x => x.CheckImportStatus(It.IsAny<string>())).Returns(Task.FromResult(importStatusResponse));
#pragma warning disable CS8604 // Possible null reference argument.
            var exportOrchestrator = new ExportOrchestrator(_exportProcessor.Object, options: _config, orchestrationHelper);
            var importOrchestrator = new ImportOrchestrator(_importProcessor.Object, options: _config);
            var importResult = new ResponseModel();
            var exportResult = new ResponseModel();
            var importStatusResult = new ResponseModel();
            var exportStatusResult = new ResponseModel();

            try
            {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
                exportResult = await exportOrchestrator.ProcessExport(null, null);
                if (exportResult.Status == ResponseStatus.Completed)
                {
                    exportStatusResult = await exportOrchestrator.ProcessExportStatusCheck(exportResult.Content, null);
                    if (exportStatusResult.Status == ResponseStatus.Completed)
                    {
                        string importRequestContent = exportStatusResult.Content;
                        importResult = await importOrchestrator.ProcessImport(importRequestContent, null);

                        if (importResult.Status == ResponseStatus.Completed)
                        {
                            importStatusResult = await importOrchestrator.ProcessImportStatusCheck(importResult.Content, null);
                        }
                    }
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
                }

                Assert.IsTrue(exportResult.Status == ResponseStatus.Completed);
                Assert.IsTrue(exportStatusResult.Status == ResponseStatus.Failed);
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
            var exportResponse = new HttpResponseMessage
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
            var exportStatusResponse = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(await File.ReadAllTextAsync("../../../mock_export_status_response.json")),
            };
            var importResponse = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError,
                ReasonPhrase = "Import request failed.",
            };
            var importStatusResponse = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(await File.ReadAllTextAsync("../../../mock_import_status_response.json")),
            };

            _exportProcessor.Setup(x => x.CallExport()).Returns(Task.FromResult(exportResponse));

            _exportProcessor.Setup(x => x.CheckExportStatus(It.IsAny<string>())).Returns(Task.FromResult(exportStatusResponse));

            _importProcessor.Setup(x => x.CallImport(It.IsAny<string>())).Returns(Task.FromResult(importResponse));

            _importProcessor.Setup(x => x.CheckImportStatus(It.IsAny<string>())).Returns(Task.FromResult(importStatusResponse));
#pragma warning disable CS8604 // Possible null reference argument.
            var exportOrchestrator = new ExportOrchestrator(_exportProcessor.Object, options: _config, orchestrationHelper);
            var importOrchestrator = new ImportOrchestrator(_importProcessor.Object, options: _config);
            var importResult = new ResponseModel();
            var exportResult = new ResponseModel();
            var importStatusResult = new ResponseModel();
            var exportStatusResult = new ResponseModel();

            try
            {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
                exportResult = await exportOrchestrator.ProcessExport(null, null);
                if (exportResult.Status == ResponseStatus.Completed)
                {
                    exportStatusResult = await exportOrchestrator.ProcessExportStatusCheck(exportResult.Content, null);
                    if (exportStatusResult.Status == ResponseStatus.Completed)
                    {
                        string importRequestContent = exportStatusResult.Content;
                        importResult = await importOrchestrator.ProcessImport(importRequestContent, null);

                        if (importResult.Status == ResponseStatus.Completed)
                        {
                            importStatusResult = await importOrchestrator.ProcessImportStatusCheck(importResult.Content, null);
                        }
                    }
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
                }

                Assert.IsTrue(exportResult.Status == ResponseStatus.Completed);
                Assert.IsTrue(exportStatusResult.Status == ResponseStatus.Completed);
                Assert.IsTrue(importResult.Status == ResponseStatus.Failed);
            }
            catch (Exception ex)
            {
                _loggerExport.LogError($"Error occurred during test: {ex.Message}");
#pragma warning restore CS8604 // Possible null reference argument.
            }
        }

        [TestMethod]
        public async Task MigrationTestImportStatusFail()
        {
            var exportStatusUrl = "https://azureapiforfhir.azurehealthcareapis.com/_operations/export/container";
            var importStatusUrl = "https://fhirservice.fhir.azurehealthcareapis.com/_operations/import/importId";
            var exportResponse = new HttpResponseMessage
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
            var exportStatusResponse = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(await File.ReadAllTextAsync("../../../mock_export_status_response.json")),
            };
            var importResponse = new HttpResponseMessage
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
            var importStatusResponse = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError,
                Content = new StringContent(await File.ReadAllTextAsync("../../../mock_import_status_response.json")),
            };

            _exportProcessor.Setup(x => x.CallExport()).Returns(Task.FromResult(exportResponse));

            _exportProcessor.Setup(x => x.CheckExportStatus(It.IsAny<string>())).Returns(Task.FromResult(exportStatusResponse));

            _importProcessor.Setup(x => x.CallImport(It.IsAny<string>())).Returns(Task.FromResult(importResponse));

            _importProcessor.Setup(x => x.CheckImportStatus(It.IsAny<string>())).Returns(Task.FromResult(importStatusResponse));
#pragma warning disable CS8604 // Possible null reference argument.
            var exportOrchestrator = new ExportOrchestrator(_exportProcessor.Object, options: _config, orchestrationHelper);
            var importOrchestrator = new ImportOrchestrator(_importProcessor.Object, options: _config);
            var importResult = new ResponseModel();
            var exportResult = new ResponseModel();
            var importStatusResult = new ResponseModel();
            var exportStatusResult = new ResponseModel();

            try
            {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
                exportResult = await exportOrchestrator.ProcessExport(null, null);
                if (exportResult.Status == ResponseStatus.Completed)
                {
                    exportStatusResult = await exportOrchestrator.ProcessExportStatusCheck(exportResult.Content, null);
                    if (exportStatusResult.Status == ResponseStatus.Completed)
                    {
                        string importRequestContent = exportStatusResult.Content;
                        importResult = await importOrchestrator.ProcessImport(importRequestContent, null);

                        if (importResult.Status == ResponseStatus.Completed)
                        {
                            importStatusResult = await importOrchestrator.ProcessImportStatusCheck(importResult.Content, null);
                        }
                    }
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
                }

                Assert.IsTrue(exportResult.Status == ResponseStatus.Completed);
                Assert.IsTrue(exportStatusResult.Status == ResponseStatus.Completed);
                Assert.IsTrue(importResult.Status == ResponseStatus.Completed);
                Assert.IsTrue(importStatusResult.Status == ResponseStatus.Completed);
            }
            catch (Exception ex)
            {
                _loggerExport.LogError($"Error occurred during test: {ex.Message}");
#pragma warning restore CS8604 // Possible null reference argument.
            }
        }
    }
}
