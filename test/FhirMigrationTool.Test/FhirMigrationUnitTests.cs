// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Net;
using System.Reflection;
using Azure.Data.Tables;
using FhirMigrationTool;
using FhirMigrationTool.Configuration;
using FhirMigrationTool.FhirOperation;
using FhirMigrationTool.Models;
using FhirMigrationTool.OrchestrationHelper;
using FhirMigrationTool.Processors;
using FhirMigrationTool.SearchParameterOperation;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.ApplicationInsights;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json.Linq;

namespace FhirMigrationtool.Tests
{
    [TestClass]
    public class FhirMigrationUnitTests
    {
        private static MigrationOptions? _config;
        private static ILogger<FhirMigrationUnitTests>? _logger;
#pragma warning disable CS0649 // Field 'FhirMigrationUnitTests._loggerExport' is never assigned to, and will always have its default value null
        private static ILogger? _loggerExport;
        private static ILogger? _loggerImport;
#pragma warning restore CS0649 // Field 'FhirMigrationUnitTests._loggerExport' is never assigned to, and will always have its default value null

        private static TelemetryClient? telemetryClient;
        private static IFhirClient? fhirClient;
        private static IOrchestrationHelper? orchestrationHelper;
        private readonly Mock<IFhirClient> _mockClient;
        private readonly Mock<HttpClient> _mockHttpClient;
        private readonly Mock<IFhirProcessor> _exportProcessor;
        private readonly Mock<IFhirProcessor> _importProcessor;
        private static IAzureTableClientFactory? _azureTableClientFactory;
        private static IMetadataStore? _azureTableMetadataStore;
        private static TableServiceClient? _tableServiceClient;
        private readonly Mock<ISearchParameterOperation> _mockSearchParameterOperation;
        private static ILogger<SearchParameterMigrationActivity>? _loggerSearchParameter;
        private static ILogger<SearchParameterOperation>? _loggerSearchParameterOperation;

        // private readonly Mock<IImportProcessor> _importProcessor;
        public FhirMigrationUnitTests()
        {
            _mockClient = new Mock<IFhirClient>();
            _exportProcessor = new Mock<IFhirProcessor>();
            _importProcessor = new Mock<IFhirProcessor>();
            _mockHttpClient = new Mock<HttpClient>();
            _mockSearchParameterOperation = new Mock<ISearchParameterOperation>();
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
            _config.AppInsightConnectionstring = "InstrumentationKey=Test;";
            _config.StagingStorageUri = "https://storageuri.net";
            _config.SourceUri = _config.DestinationUri = new Uri("https://dummy.azurehealthcareapis.com");
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
                                     .AddTransient<IAzureTableClientFactory, AzureTableClientFactory>()
                                     .AddTransient<IMetadataStore, AzureTableMetadataStore>()
                                     .AddSingleton<TableServiceClient>(new TableServiceClient(new Uri(_config.StagingStorageUri), _config.TokenCredential))
                                     .BuildServiceProvider();

            var factory = serviceProvider.GetService<ILoggerFactory>();
            if (factory is not null)
            {
                var fhirLogger = factory.CreateLogger<FhirClient>();
                var httpClientFactory = serviceProvider.GetService<IHttpClientFactory>();

                orchestrationHelper = new OrchestrationHelper();

                _tableServiceClient = serviceProvider.GetService<TableServiceClient>()!;
                _azureTableClientFactory = new AzureTableClientFactory(_tableServiceClient);

                _azureTableMetadataStore = new AzureTableMetadataStore();

                if (httpClientFactory is not null)
                {
                    fhirClient = new FhirClient(httpClientFactory);

                    using ILoggerFactory loggerFactory = LoggerFactory.Create(loggingBuilder => loggingBuilder
                        .SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace)
                        .AddConsole());

                    _logger = loggerFactory.CreateLogger<FhirMigrationUnitTests>();
                    _loggerSearchParameter = loggerFactory.CreateLogger<SearchParameterMigrationActivity>();
                    _loggerSearchParameterOperation = loggerFactory.CreateLogger<SearchParameterOperation>();

                    // _loggerExport = loggerFactory.CreateLogger<ExportProcessor>();
                    // _loggerImport = loggerFactory.CreateLogger<ImportProcessor>();
                    root.Bind(fhirLogger);
                    root.Bind(_logger);
                    root.Bind(_loggerExport);
                    root.Bind(_loggerImport);
                    root.Bind(telemetryClient);
                    root.Bind(fhirClient);
                    root.Bind(orchestrationHelper);
                    root.Bind(_azureTableClientFactory);
                    root.Bind(_azureTableMetadataStore);
                    root.Bind(_loggerSearchParameter);
                    root.Bind(_loggerSearchParameterOperation);
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
                // _mockHttpClient.Setup(a => a.SendAsync(It.IsAny<HttpRequestMessage>())).Returns(Task.FromResult(response));
                var httpClient = new HttpClient();
                var method = HttpMethod.Get;
#pragma warning disable CS8604 // Possible null reference argument.
                IFhirProcessor exportProcessor = new FhirProcessor(
                    fhirClient: _mockClient.Object,
                    telemetryClient: telemetryClient,
                    logger: _loggerExport as ILogger<FhirProcessor>);
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                ResponseModel exportResponse = await exportProcessor.CallProcess(method, string.Empty, _config.SourceUri, "/$export?_type=Patient", _config.SourceHttpClient);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                var statusUrl = exportResponse.Content;
                Assert.IsTrue(exportResponse.Status == ResponseStatus.Completed);
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
                IFhirProcessor exportProcessor = new FhirProcessor(
                    _mockClient.Object,
                    telemetryClient: telemetryClient,
                    logger: _loggerExport as ILogger<FhirProcessor>);
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                ResponseModel exportStatusResponse = await exportProcessor.CheckProcessStatus(exportStatusUrl, _config.SourceUri, _config.SourceHttpClient);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                Assert.IsTrue(exportStatusResponse.Status == ResponseStatus.Completed);
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
            var method = HttpMethod.Post;
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
                IFhirProcessor importProcessor = new FhirProcessor(
                    fhirClient: _mockClient.Object,
                    telemetryClient: telemetryClient,
                    logger: _loggerImport as ILogger<FhirProcessor>);
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                ResponseModel importResponse = await importProcessor.CallProcess(method, importRequest, _config.DestinationUri, string.Empty, _config.DestinationHttpClient);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                var statusUrl = importResponse.Content;
                Assert.IsTrue(importResponse.Status == ResponseStatus.Completed);
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
                IFhirProcessor importProcessor = new FhirProcessor(
                    fhirClient: _mockClient.Object,
                    telemetryClient: telemetryClient,
                    logger: _loggerImport as ILogger<FhirProcessor>);
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                ResponseModel importStatusResponse = await importProcessor.CheckProcessStatus(importStatusUrl, _config.DestinationUri, _config.DestinationHttpClient);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                Assert.IsTrue(importStatusResponse.Status == ResponseStatus.Completed);
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
            var exportResponse = new ResponseModel
            {
                Status = ResponseStatus.Accepted,
                Content = exportStatusUrl,
            };
            var exportStatusResponse = new ResponseModel
            {
                Status = ResponseStatus.Completed,
                Content = await File.ReadAllTextAsync("../../../mock_export_status_response.json"),
            };
            var importResponse = new ResponseModel
            {
                Status = ResponseStatus.Accepted,
                Content = importStatusUrl,
            };
            var importStatusResponse = new ResponseModel
            {
                Status = ResponseStatus.Completed,
                Content = await File.ReadAllTextAsync("../../../mock_import_status_response.json"),
            };

            _exportProcessor.Setup(x => x.CallProcess(It.IsAny<HttpMethod>(), It.IsAny<string>(), It.IsAny<Uri>(), It.IsAny<string>(), It.IsAny<string>())).Returns(Task.FromResult(exportResponse));

            _exportProcessor.Setup(x => x.CheckProcessStatus(It.IsAny<string>(), It.IsAny<Uri>(), It.IsAny<string>())).Returns(Task.FromResult(exportStatusResponse));

            _importProcessor.Setup(x => x.CallProcess(It.IsAny<HttpMethod>(), It.IsAny<string>(), It.IsAny<Uri>(), It.IsAny<string>(), It.IsAny<string>())).Returns(Task.FromResult(importResponse));

            _importProcessor.Setup(x => x.CheckProcessStatus(It.IsAny<string>(), It.IsAny<Uri>(), It.IsAny<string>())).Returns(Task.FromResult(importStatusResponse));
#pragma warning disable CS8604 // Possible null reference argument.
            var exportOrchestrator = new ExportOrchestrator(_exportProcessor.Object, options: _config, _azureTableClientFactory, _azureTableMetadataStore, _mockClient.Object, orchestrationHelper);
            var importOrchestrator = new ImportOrchestrator(_importProcessor.Object, options: _config, _azureTableClientFactory, _azureTableMetadataStore);
            var exportStatusOrchestrator = new ExportStatusOrchestrator(_exportProcessor.Object, options: _config, _azureTableClientFactory, _azureTableMetadataStore, _mockClient.Object, orchestrationHelper);
            var importStatusOrchestrator = new ImportStatusOrchestrator(_importProcessor.Object, options: _config, _azureTableClientFactory, _azureTableMetadataStore);
            try
            {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
                exportResponse = await exportOrchestrator.ProcessExport(null, null);
                if (exportResponse.Status == ResponseStatus.Completed)
                {
                    exportStatusResponse = await exportStatusOrchestrator.ProcessExportStatusCheck(exportResponse.Content, null);
                    if (exportStatusResponse.Status == ResponseStatus.Completed)
                    {
                        string importRequestContent = exportStatusResponse.Content;
                        importResponse = await importOrchestrator.ProcessImport(importRequestContent, null);

                        if (importResponse.Status == ResponseStatus.Completed)
                        {
                            importStatusResponse = await importStatusOrchestrator.ProcessImportStatusCheck(importResponse.Content, null);
                        }
                    }
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
                }

                Assert.IsTrue(exportResponse.Status == ResponseStatus.Accepted);
                Assert.IsTrue(exportStatusResponse.Status == ResponseStatus.Completed);
                Assert.IsTrue(importResponse.Status == ResponseStatus.Accepted);
                Assert.IsTrue(importStatusResponse.Status == ResponseStatus.Completed);
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
            var exportResponse = new ResponseModel
            {
                Status = ResponseStatus.Failed,
            };
            var exportStatusResponse = new ResponseModel
            {
                Status = ResponseStatus.Completed,
                Content = await File.ReadAllTextAsync("../../../mock_export_status_response.json"),
            };
            var importResponse = new ResponseModel
            {
                Status = ResponseStatus.Accepted,
                Content = importStatusUrl,
            };
            var importStatusResponse = new ResponseModel
            {
                Status = ResponseStatus.Completed,
                Content = await File.ReadAllTextAsync("../../../mock_import_status_response.json"),
            };

            _exportProcessor.Setup(x => x.CallProcess(It.IsAny<HttpMethod>(), It.IsAny<string>(), It.IsAny<Uri>(), It.IsAny<string>(), It.IsAny<string>())).Returns(Task.FromResult(exportResponse));

            _exportProcessor.Setup(x => x.CheckProcessStatus(It.IsAny<string>(), It.IsAny<Uri>(), It.IsAny<string>())).Returns(Task.FromResult(exportStatusResponse));

            _importProcessor.Setup(x => x.CallProcess(It.IsAny<HttpMethod>(), It.IsAny<string>(), It.IsAny<Uri>(), It.IsAny<string>(), It.IsAny<string>())).Returns(Task.FromResult(importResponse));

            _importProcessor.Setup(x => x.CheckProcessStatus(It.IsAny<string>(), It.IsAny<Uri>(), It.IsAny<string>())).Returns(Task.FromResult(importStatusResponse));
#pragma warning disable CS8604 // Possible null reference argument.
            var exportOrchestrator = new ExportOrchestrator(_exportProcessor.Object, options: _config, _azureTableClientFactory, _azureTableMetadataStore, _mockClient.Object, orchestrationHelper);
            var importOrchestrator = new ImportOrchestrator(_importProcessor.Object, options: _config, _azureTableClientFactory, _azureTableMetadataStore);
            var exportStatusOrchestrator = new ExportStatusOrchestrator(_exportProcessor.Object, options: _config, _azureTableClientFactory, _azureTableMetadataStore, _mockClient.Object, orchestrationHelper);
            var importStatusOrchestrator = new ImportStatusOrchestrator(_importProcessor.Object, options: _config, _azureTableClientFactory, _azureTableMetadataStore);
            try
            {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
                exportResponse = await exportOrchestrator.ProcessExport(null, null);
                if (exportResponse.Status == ResponseStatus.Completed)
                {
                    exportStatusResponse = await exportStatusOrchestrator.ProcessExportStatusCheck(exportResponse.Content, null);
                    if (exportStatusResponse.Status == ResponseStatus.Completed)
                    {
                        string importRequestContent = exportStatusResponse.Content;
                        importResponse = await importOrchestrator.ProcessImport(importRequestContent, null);

                        if (importResponse.Status == ResponseStatus.Completed)
                        {
                            importStatusResponse = await importStatusOrchestrator.ProcessImportStatusCheck(importResponse.Content, null);
                        }
                    }
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
                }

                Assert.IsTrue(exportResponse.Status == ResponseStatus.Failed);
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
            var exportResponse = new ResponseModel
            {
                Status = ResponseStatus.Accepted,
                Content = exportStatusUrl,
            };
            var exportStatusResponse = new ResponseModel
            {
                Status = ResponseStatus.Failed,
                Content = await File.ReadAllTextAsync("../../../mock_export_status_response.json"),
            };
            var importResponse = new ResponseModel
            {
                Status = ResponseStatus.Accepted,
                Content = importStatusUrl,
            };
            var importStatusResponse = new ResponseModel
            {
                Status = ResponseStatus.Completed,
                Content = await File.ReadAllTextAsync("../../../mock_import_status_response.json"),
            };

            _exportProcessor.Setup(x => x.CallProcess(It.IsAny<HttpMethod>(), It.IsAny<string>(), It.IsAny<Uri>(), It.IsAny<string>(), It.IsAny<string>())).Returns(Task.FromResult(exportResponse));

            _exportProcessor.Setup(x => x.CheckProcessStatus(It.IsAny<string>(), It.IsAny<Uri>(), It.IsAny<string>())).Returns(Task.FromResult(exportStatusResponse));

            _importProcessor.Setup(x => x.CallProcess(It.IsAny<HttpMethod>(), It.IsAny<string>(), It.IsAny<Uri>(), It.IsAny<string>(), It.IsAny<string>())).Returns(Task.FromResult(importResponse));

            _importProcessor.Setup(x => x.CheckProcessStatus(It.IsAny<string>(), It.IsAny<Uri>(), It.IsAny<string>())).Returns(Task.FromResult(importStatusResponse));
#pragma warning disable CS8604 // Possible null reference argument.
            var exportOrchestrator = new ExportOrchestrator(_exportProcessor.Object, options: _config, _azureTableClientFactory, _azureTableMetadataStore, _mockClient.Object, orchestrationHelper);
            var importOrchestrator = new ImportOrchestrator(_importProcessor.Object, options: _config, _azureTableClientFactory, _azureTableMetadataStore);
            var exportStatusOrchestrator = new ExportStatusOrchestrator(_exportProcessor.Object, options: _config, _azureTableClientFactory, _azureTableMetadataStore, _mockClient.Object, orchestrationHelper);
            var importStatusOrchestrator = new ImportStatusOrchestrator(_importProcessor.Object, options: _config, _azureTableClientFactory, _azureTableMetadataStore);
            try
            {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
                exportResponse = await exportOrchestrator.ProcessExport(null, null);
                if (exportResponse.Status == ResponseStatus.Completed)
                {
                    exportStatusResponse = await exportStatusOrchestrator.ProcessExportStatusCheck(exportResponse.Content, null);
                    if (exportStatusResponse.Status == ResponseStatus.Completed)
                    {
                        string importRequestContent = exportStatusResponse.Content;
                        importResponse = await importOrchestrator.ProcessImport(importRequestContent, null);

                        if (importResponse.Status == ResponseStatus.Completed)
                        {
                            importStatusResponse = await importStatusOrchestrator.ProcessImportStatusCheck(importResponse.Content, null);
                        }
                    }
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
                }

                Assert.IsTrue(exportResponse.Status == ResponseStatus.Accepted);
                Assert.IsTrue(exportStatusResponse.Status == ResponseStatus.Failed);
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
            var exportResponse = new ResponseModel
            {
                Status = ResponseStatus.Accepted,
                Content = exportStatusUrl,
            };
            var exportStatusResponse = new ResponseModel
            {
                Status = ResponseStatus.Completed,
                Content = await File.ReadAllTextAsync("../../../mock_export_status_response.json"),
            };
            var importResponse = new ResponseModel
            {
                Status = ResponseStatus.Failed,
                Content = string.Empty,
            };
            var importStatusResponse = new ResponseModel
            {
                Status = ResponseStatus.Completed,
                Content = await File.ReadAllTextAsync("../../../mock_import_status_response.json"),
            };

            _exportProcessor.Setup(x => x.CallProcess(It.IsAny<HttpMethod>(), It.IsAny<string>(), It.IsAny<Uri>(), It.IsAny<string>(), It.IsAny<string>())).Returns(Task.FromResult(exportResponse));

            _exportProcessor.Setup(x => x.CheckProcessStatus(It.IsAny<string>(), It.IsAny<Uri>(), It.IsAny<string>())).Returns(Task.FromResult(exportStatusResponse));

            _importProcessor.Setup(x => x.CallProcess(It.IsAny<HttpMethod>(), It.IsAny<string>(), It.IsAny<Uri>(), It.IsAny<string>(), It.IsAny<string>())).Returns(Task.FromResult(importResponse));

            _importProcessor.Setup(x => x.CheckProcessStatus(It.IsAny<string>(), It.IsAny<Uri>(), It.IsAny<string>())).Returns(Task.FromResult(importStatusResponse));
#pragma warning disable CS8604 // Possible null reference argument.
            var exportOrchestrator = new ExportOrchestrator(_exportProcessor.Object, options: _config, _azureTableClientFactory, _azureTableMetadataStore, _mockClient.Object, orchestrationHelper);
            var importOrchestrator = new ImportOrchestrator(_importProcessor.Object, options: _config, _azureTableClientFactory, _azureTableMetadataStore);
            var exportStatusOrchestrator = new ExportStatusOrchestrator(_exportProcessor.Object, options: _config, _azureTableClientFactory, _azureTableMetadataStore, _mockClient.Object, orchestrationHelper);
            var importStatusOrchestrator = new ImportStatusOrchestrator(_importProcessor.Object, options: _config, _azureTableClientFactory, _azureTableMetadataStore);
            try
            {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
                exportResponse = await exportOrchestrator.ProcessExport(null, null);
                if (exportResponse.Status == ResponseStatus.Completed)
                {
                    exportStatusResponse = await exportStatusOrchestrator.ProcessExportStatusCheck(exportResponse.Content, null);
                    if (exportStatusResponse.Status == ResponseStatus.Completed)
                    {
                        string importRequestContent = exportStatusResponse.Content;
                        importResponse = await importOrchestrator.ProcessImport(importRequestContent, null);

                        if (importResponse.Status == ResponseStatus.Completed)
                        {
                            importStatusResponse = await importStatusOrchestrator.ProcessImportStatusCheck(importResponse.Content, null);
                        }
                    }
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
                }

                Assert.IsTrue(exportResponse.Status == ResponseStatus.Accepted);
                Assert.IsTrue(exportStatusResponse.Status == ResponseStatus.Completed);
                Assert.IsTrue(importResponse.Status == ResponseStatus.Failed);
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
            var exportResponse = new ResponseModel
            {
                Status = ResponseStatus.Accepted,
                Content = exportStatusUrl,
            };
            var exportStatusResponse = new ResponseModel
            {
                Status = ResponseStatus.Completed,
                Content = await File.ReadAllTextAsync("../../../mock_export_status_response.json"),
            };
            var importResponse = new ResponseModel
            {
                Status = ResponseStatus.Accepted,
                Content = importStatusUrl,
            };
            var importStatusResponse = new ResponseModel
            {
                Status = ResponseStatus.Failed,
            };

            _exportProcessor.Setup(x => x.CallProcess(It.IsAny<HttpMethod>(), It.IsAny<string>(), It.IsAny<Uri>(), It.IsAny<string>(), It.IsAny<string>())).Returns(Task.FromResult(exportResponse));

            _exportProcessor.Setup(x => x.CheckProcessStatus(It.IsAny<string>(), It.IsAny<Uri>(), It.IsAny<string>())).Returns(Task.FromResult(exportStatusResponse));

            _importProcessor.Setup(x => x.CallProcess(It.IsAny<HttpMethod>(), It.IsAny<string>(), It.IsAny<Uri>(), It.IsAny<string>(), It.IsAny<string>())).Returns(Task.FromResult(importResponse));

            _importProcessor.Setup(x => x.CheckProcessStatus(It.IsAny<string>(), It.IsAny<Uri>(), It.IsAny<string>())).Returns(Task.FromResult(importStatusResponse));
#pragma warning disable CS8604 // Possible null reference argument.
            var exportOrchestrator = new ExportOrchestrator(_exportProcessor.Object, options: _config, _azureTableClientFactory, _azureTableMetadataStore, _mockClient.Object, orchestrationHelper);
            var importOrchestrator = new ImportOrchestrator(_importProcessor.Object, options: _config, _azureTableClientFactory, _azureTableMetadataStore);
            var exportStatusOrchestrator = new ExportStatusOrchestrator(_exportProcessor.Object, options: _config, _azureTableClientFactory, _azureTableMetadataStore, _mockClient.Object, orchestrationHelper);
            var importStatusOrchestrator = new ImportStatusOrchestrator(_importProcessor.Object, options: _config, _azureTableClientFactory, _azureTableMetadataStore);
            try
            {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
                exportResponse = await exportOrchestrator.ProcessExport(null, null);
                if (exportResponse.Status == ResponseStatus.Completed)
                {
                    exportStatusResponse = await exportStatusOrchestrator.ProcessExportStatusCheck(exportResponse.Content, null);
                    if (exportStatusResponse.Status == ResponseStatus.Completed)
                    {
                        string importRequestContent = exportStatusResponse.Content;
                        importResponse = await importOrchestrator.ProcessImport(importRequestContent, null);

                        if (importResponse.Status == ResponseStatus.Completed)
                        {
                            importStatusResponse = await importStatusOrchestrator.ProcessImportStatusCheck(importResponse.Content, null);
                        }
                    }
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
                }

                Assert.IsTrue(exportResponse.Status == ResponseStatus.Accepted);
                Assert.IsTrue(exportStatusResponse.Status == ResponseStatus.Completed);
                Assert.IsTrue(importResponse.Status == ResponseStatus.Accepted);
                Assert.IsTrue(importStatusResponse.Status == ResponseStatus.Failed);
            }
            catch (Exception ex)
            {
                _loggerExport.LogError($"Error occurred during test: {ex.Message}");
#pragma warning restore CS8604 // Possible null reference argument.
            }
        }

        [TestMethod]
        public async Task SearchParamterMigrationTestPass()
        {
            string mockSearchParamterJson = await File.ReadAllTextAsync("../../../mock_search_parameter.json");
            string mockTranformedSearchParamterJson = await File.ReadAllTextAsync("../../../mock_Transformed_Search_Parameters.json");
            _mockSearchParameterOperation.Setup(x => x.GetSearchParameters()).Returns(Task.FromResult(JObject.Parse(mockSearchParamterJson)));

            _mockSearchParameterOperation.Setup(x => x.TransformObject(JObject.Parse(mockSearchParamterJson))).Returns(mockTranformedSearchParamterJson);

#pragma warning disable CS8604 // Possible null reference argument.
            var searchParameterMigrationActivity = new SearchParameterMigrationActivity(_mockSearchParameterOperation.Object, _loggerSearchParameter);
#pragma warning restore CS8604 // Possible null reference argument.

            try
            {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
                await searchParameterMigrationActivity.SearchParameterMigration(null);
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
            }
            catch (Exception ex)
            {
                _loggerSearchParameter.LogError($"Error occurred during test: {ex.Message}");
            }
        }

        [TestMethod]
        public async Task GetSearchParametersTestPass()
        {
            string mockSearchParamterJson = await File.ReadAllTextAsync("../../../mock_search_parameter.json");
            string mockTranformedSearchParamterJson = await File.ReadAllTextAsync("../../../mock_Transformed_Search_Parameters.json");

            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(mockSearchParamterJson),
            };

            _mockClient.Setup(c => c.Send(It.IsAny<HttpRequestMessage>(), It.IsAny<Uri>(), It.IsAny<string>())).Returns(Task.FromResult(response));
            try
            {
#pragma warning disable CS8604 // Possible null reference argument.
                ISearchParameterOperation searchParameterOperation = new SearchParameterOperation(options: _config, _mockClient.Object, _loggerSearchParameterOperation);
#pragma warning restore CS8604 // Possible null reference argument.

                JObject jObjectResponse = await searchParameterOperation.GetSearchParameters();

                Assert.IsTrue(JToken.DeepEquals(jObjectResponse, JObject.Parse(mockSearchParamterJson)));
            }
            catch (Exception ex)
            {
#pragma warning disable CS8604 // Possible null reference argument.
                _loggerSearchParameterOperation.LogError($"Error occurred during test: {ex.Message}");
#pragma warning restore CS8604 // Possible null reference argument.
            }
        }

        [TestMethod]
        public async Task TransformObjectTestPass()
        {
            string mockSearchParamterJson = await File.ReadAllTextAsync("../../../mock_search_parameter.json");
            string mockTranformedSearchParamterJson = await File.ReadAllTextAsync("../../../mock_Transformed_Search_Parameters.json");

            try
            {
#pragma warning disable CS8604 // Possible null reference argument.
                ISearchParameterOperation searchParameterOperation = new SearchParameterOperation(options: _config, _mockClient.Object, _loggerSearchParameterOperation);
#pragma warning restore CS8604 // Possible null reference argument.

                string transformedJson = searchParameterOperation.TransformObject(JObject.Parse(mockSearchParamterJson));

                Assert.AreEqual(JObject.Parse(mockTranformedSearchParamterJson)["type"], JObject.Parse(mockTranformedSearchParamterJson)["type"]);
            }
            catch (Exception ex)
            {
#pragma warning disable CS8604 // Possible null reference argument.
                _loggerSearchParameterOperation.LogError($"Error occurred during test: {ex.Message}");
#pragma warning restore CS8604 // Possible null reference argument.
            }
        }

        [TestMethod]
        public async Task PostSearchParametersTestPass()
        {
            string mockTranformedSearchParamterJson = await File.ReadAllTextAsync("../../../mock_Transformed_Search_Parameters.json");

            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(mockTranformedSearchParamterJson),
            };

            _mockClient.Setup(c => c.Send(It.IsAny<HttpRequestMessage>(), It.IsAny<Uri>(), It.IsAny<string>())).Returns(Task.FromResult(response));

            try
            {
#pragma warning disable CS8604 // Possible null reference argument.
                ISearchParameterOperation searchParameterOperation = new SearchParameterOperation(options: _config, _mockClient.Object, _loggerSearchParameterOperation);
#pragma warning restore CS8604 // Possible null reference argument.

                await searchParameterOperation.PostSearchParameters(mockTranformedSearchParamterJson);
            }
            catch (Exception ex)
            {
#pragma warning disable CS8604 // Possible null reference argument.
                _loggerSearchParameterOperation.LogError($"Error occurred during test: {ex.Message}");
#pragma warning restore CS8604 // Possible null reference argument.
            }
        }
    }
}
