﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------
using System.Collections.Concurrent;
using System.Net;
using ApiForFhirMigrationTool.Function.Configuration;
using ApiForFhirMigrationTool.Function.FhirOperation;
using ApiForFhirMigrationTool.Function.Migration;
using ApiForFhirMigrationTool.Function.Models;
using ApiForFhirMigrationTool.Function.OrchestrationHelper;
using ApiForFhirMigrationTool.Function.Processors;
using ApiForFhirMigrationTool.Function.SearchParameterOperation;
using Azure.Data.Tables;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;

namespace ApiForFhirMigrationTool.Function.UnitTests
{
    internal static class TestHelpers
    {
        internal const string TestSourceUri = "https://azureapiforfhir.azurehealthcareapis.com";
        internal const string TestDestinationUri = "https://fhirservice.fhir.azurehealthcareapis.com";
        internal const string ExportStatusUrl = $"{TestSourceUri}/_operations/export/importId";
        internal const string ImportStatusUrl = $"{TestDestinationUri}/_operations/import/importId";
        internal static ConcurrentQueue<ITelemetry> s_telemetryItems = new();

        internal static string MockExportStatusResponse => File.ReadAllText("../../../TestFiles/mock_export_status_response.json");

        internal static string MockImportStatusResponse => File.ReadAllText("../../../TestFiles/mock_import_status_response.json");

        internal static string TestImportBody => File.ReadAllText("../../../TestFiles/import_body.json");

        internal static string MockSearchParamterJson => File.ReadAllText("../../../TestFiles/mock_search_parameter.json");

        internal static string MockTranformedSearchParamterJson => File.ReadAllText("../../../TestFiles/mock_Transformed_Search_Parameters.json");

        internal static Mock<IAzureTableClientFactory> GetMockAzureTableClientFactory()
        {
            var azureTableClient = new Mock<TableClient>();
            var azureTableClientFactory = new Mock<IAzureTableClientFactory>();

            azureTableClientFactory.Setup(x => x.Create(It.IsAny<string>())).Returns(azureTableClient.Object);

            return azureTableClientFactory;
        }

        internal static Mock<IMetadataStore> GetMockMetadataStore()
        {
            var azureTableEntity = new TableEntity();
            var azureTableClientFactory = GetMockAzureTableClientFactory();
            var azureTableMetadataStore = new Mock<IMetadataStore>();

            azureTableMetadataStore.Setup(x => x.GetEntity(It.IsAny<TableClient>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(azureTableEntity!);
            azureTableMetadataStore.Setup(x => x.AddEntity(It.IsAny<TableClient>(), It.IsAny<TableEntity>(), It.IsAny<CancellationToken>())).Returns(true);
            azureTableMetadataStore.Setup(x => x.UpdateEntity(It.IsAny<TableClient>(), It.IsAny<TableEntity>(), It.IsAny<CancellationToken>())).Returns(true);

            return azureTableMetadataStore;
        }

        internal static TelemetryClient GetMockTelemetryClient()
        {
            //var telemetryClient = new Mock<TelemetryClient>();

            //telemetryClient.Setup(tc => tc.TrackEvent(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<Dictionary<string, double>>()));

            return CreateMockTelemetryClient();
        }

        internal static TelemetryClient CreateMockTelemetryClient()
        {
            var telemetryConfiguration = new TelemetryConfiguration
            {
                ConnectionString = "InstrumentationKey=" + Guid.NewGuid().ToString(),
                TelemetryChannel = new StubTelemetryChannel(s_telemetryItems.Enqueue)
            };

            // TODO: Add telemetry initializers and processors if/as necessary.
            return new TelemetryClient(telemetryConfiguration);
        }

        internal static ExportOrchestrator GetTestExportOrchestrator(MigrationOptions config, IFhirProcessor exportProcessor)
        {
            var mockClient = new Mock<IFhirClient>();
            var orchestrationHelper = new Mock<IOrchestrationHelper>();
            var logger = new Mock<ILogger<ExportOrchestrator>>();

            return new ExportOrchestrator(
                               exportProcessor,
                               config,
                               GetMockAzureTableClientFactory().Object,
                               GetMockMetadataStore().Object,
                               mockClient.Object,
                               orchestrationHelper.Object,
                               GetMockTelemetryClient(),
                               logger.Object);
        }

        internal static ExportStatusOrchestrator GetTestExportStatusOrchestrator(MigrationOptions config, IFhirProcessor exportProcessor)
        {
            var mockClient = new Mock<IFhirClient>();
            var orchestrationHelper = new Mock<IOrchestrationHelper>();
            var logger = new Mock<ILogger<ExportStatusOrchestrator>>();

            return new ExportStatusOrchestrator(
                                        exportProcessor,
                                        config,
                                        GetMockAzureTableClientFactory().Object,
                                        GetMockMetadataStore().Object,
                                        mockClient.Object,
                                        orchestrationHelper.Object,
                                        GetMockTelemetryClient(),
                                         logger.Object);
        }

        internal static ImportOrchestrator GetTestImportOrchestrator(MigrationOptions config, IFhirProcessor importProcessor, IAzureBlobClientFactory azureBlobClientFactory)
        {
            var orchestrationHelper = new Mock<IOrchestrationHelper>();
            var logger = new Mock<ILogger<ImportOrchestrator>>();

            return new ImportOrchestrator(
                                        importProcessor,
                                        config,
                                        GetMockAzureTableClientFactory().Object,
                                        GetMockMetadataStore().Object,
                                        orchestrationHelper.Object,
                                        GetMockTelemetryClient(),
                                        azureBlobClientFactory,
                                        logger.Object);
        }

        internal static ImportStatusOrchestrator GetTestImportStatusOrchestrator(MigrationOptions config, IFhirProcessor importProcessor)
        {
            var mockClient = new Mock<IFhirClient>();
            var orchestrationHelper = new Mock<IOrchestrationHelper>();
            var logger = new Mock<ILogger<ImportStatusOrchestrator>>();
            return new ImportStatusOrchestrator(
                                        importProcessor,
                                        config,
                                        GetMockAzureTableClientFactory().Object,
                                        GetMockMetadataStore().Object,
                                        orchestrationHelper.Object,
                                        GetMockTelemetryClient(),
                                        mockClient.Object,
                                        logger.Object);
        }

        internal static Mock<IFhirClient> SetupSuccessfulExportOperationResponse(this Mock<IFhirClient> fhirClient)
        {
            fhirClient.Setup(c => c.Send(
                It.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("$export")),
                It.IsAny<Uri>(),
                It.IsAny<string>()))
            .Returns(Task.FromResult(
                 new HttpResponseMessage
                 {
                     StatusCode = HttpStatusCode.Accepted,
                     ReasonPhrase = "Export request accepted.",
                     Content =
                    {
                        Headers =
                        {
                            { "Content-Location", ExportStatusUrl },
                        },
                    },
                 }));

            return fhirClient;
        }

        internal static Mock<IFhirClient> SetupSuccessfulExportStatusResponse(this Mock<IFhirClient> fhirClient)
        {
            fhirClient.Setup(c => c.Send(
                It.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("_operations/export")),
                It.IsAny<Uri>(),
                It.IsAny<string>()))
            .Returns(Task.FromResult(
                new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(MockExportStatusResponse),
                }));

            return fhirClient;
        }

        internal static Mock<IFhirClient> SetupSuccessfulImportOperationResponse(this Mock<IFhirClient> fhirClient)
        {
            fhirClient.Setup(c => c.Send(
                It.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("$import")),
                It.IsAny<Uri>(),
                It.IsAny<string>()))
            .Returns(Task.FromResult(
                new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.Accepted,
                    ReasonPhrase = "Import request accepted.",
                    Content =
                    {
                        Headers =
                        {
                            { "Content-Location", ImportStatusUrl },
                        },
                    },
                }));

            return fhirClient;
        }

        internal static Mock<IFhirClient> SetupSuccessfulImportStatusResponse(this Mock<IFhirClient> fhirClient)
        {
            fhirClient.Setup(c => c.Send(
                It.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("_operations/import")),
                It.IsAny<Uri>(),
                It.IsAny<string>()))
            .Returns(Task.FromResult(
                new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(MockImportStatusResponse),
                }));

            return fhirClient;
        }

        internal static Mock<IFhirProcessor> SetupSuccessfulExportOperationResponse(this Mock<IFhirProcessor> exportProcessor)
        {
            var exportResponse = new ResponseModel
            {
                Status = ResponseStatus.Accepted,
                Content = ExportStatusUrl,
            };

            exportProcessor.Setup(x => x.CallProcess(
                It.IsAny<HttpMethod>(),
                It.IsAny<string>(),
                It.IsAny<Uri>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Returns(Task.FromResult(exportResponse));

            return exportProcessor;
        }

        internal static Mock<IFhirProcessor> SetupFailedExportOperationResponse(this Mock<IFhirProcessor> exportProcessor)
        {
            var exportResponse = new ResponseModel
            {
                Status = ResponseStatus.Failed,
            };

            exportProcessor.Setup(x => x.CallProcess(
                It.IsAny<HttpMethod>(),
                It.IsAny<string>(),
                It.IsAny<Uri>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Returns(Task.FromResult(exportResponse));

            return exportProcessor;
        }

        internal static Mock<IFhirProcessor> SetupSuccessfulExportStatusResponse(this Mock<IFhirProcessor> exportProcessor)
        {
            var exportResponse = new ResponseModel
            {
                Status = ResponseStatus.Completed,
                Content = MockExportStatusResponse,
            };

            exportProcessor.Setup(x => x.CheckProcessStatus(
                It.IsAny<string>(),
                It.IsAny<Uri>(),
                It.IsAny<string>()))
            .Returns(Task.FromResult(exportResponse));

            return exportProcessor;
        }

        internal static Mock<IFhirProcessor> SetupFailedExportStatusResponse(this Mock<IFhirProcessor> exportProcessor)
        {
            var exportResponse = new ResponseModel
            {
                Status = ResponseStatus.Failed,
                Content = MockExportStatusResponse,
            };

            exportProcessor.Setup(x => x.CheckProcessStatus(
                It.IsAny<string>(),
                It.IsAny<Uri>(),
                It.IsAny<string>()))
            .Returns(Task.FromResult(exportResponse));

            return exportProcessor;
        }

        internal static Mock<IFhirProcessor> SetupSuccessfulImportOperationResponse(this Mock<IFhirProcessor> importProcessor)
        {
            var importResponse = new ResponseModel
            {
                Status = ResponseStatus.Accepted,
                Content = ImportStatusUrl,
            };

            importProcessor.Setup(x => x.CallProcess(
                It.IsAny<HttpMethod>(),
                It.IsAny<string>(),
                It.IsAny<Uri>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Returns(Task.FromResult(importResponse));

            return importProcessor;
        }

        internal static Mock<IFhirProcessor> SetupFailedImportOperationResponse(this Mock<IFhirProcessor> importProcessor)
        {
            var importResponse = new ResponseModel
            {
                Status = ResponseStatus.Failed,
            };

            importProcessor.Setup(x => x.CallProcess(
                It.IsAny<HttpMethod>(),
                It.IsAny<string>(),
                It.IsAny<Uri>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Returns(Task.FromResult(importResponse));

            return importProcessor;
        }

        internal static Mock<IFhirProcessor> SetupSuccessfulImportStatusResponse(this Mock<IFhirProcessor> importProcessor)
        {
            var importResponse = new ResponseModel
            {
                Status = ResponseStatus.Completed,
                Content = MockImportStatusResponse,
            };

            importProcessor.Setup(x => x.CheckProcessStatus(
                It.IsAny<string>(),
                It.IsAny<Uri>(),
                It.IsAny<string>()))
            .Returns(Task.FromResult(importResponse));

            return importProcessor;
        }

        internal static Mock<IFhirProcessor> SetupFailedImportStatusResponse(this Mock<IFhirProcessor> importProcessor)
        {
            var importResponse = new ResponseModel
            {
                Status = ResponseStatus.Failed,
                Content = MockImportStatusResponse,
            };

            importProcessor.Setup(x => x.CheckProcessStatus(
                It.IsAny<string>(),
                It.IsAny<Uri>(),
                It.IsAny<string>()))
            .Returns(Task.FromResult(importResponse));

            return importProcessor;
        }

        internal static SearchParameterMigrationActivity GetTestSearchParameterActivity(MigrationOptions config, ISearchParameterOperation searchParameterOperation)
        {
            var loggerMock = new Mock<ILogger<SearchParameterMigrationActivity>>();

            return new SearchParameterMigrationActivity(searchParameterOperation, GetMockAzureTableClientFactory().Object, GetMockMetadataStore().Object, config, loggerMock.Object);
        }

        internal static Mock<ISearchParameterOperation> SetupSearchParameterOperationResponse(this Mock<ISearchParameterOperation> searchParamterOperationClient)
        {
            searchParamterOperationClient.Setup(x => x.GetSearchParameters()).Returns(Task.FromResult(JObject.Parse(MockSearchParamterJson)));

            searchParamterOperationClient.Setup(x => x.TransformObject(It.IsAny<JObject>())).Returns(MockTranformedSearchParamterJson);

            searchParamterOperationClient.Setup(x => x.PostSearchParameters(It.IsAny<string>()));

            return searchParamterOperationClient;
        }

        internal static Mock<IFhirClient> SetupSuccessfulGetSearchParameterOperationResponse(this Mock<IFhirClient> fhirClient)
        {
            fhirClient.Setup(c => c.Send(
                It.IsAny<HttpRequestMessage>(),
                It.IsAny<Uri>(),
                It.IsAny<string>()))
            .Returns(Task.FromResult(
                 new HttpResponseMessage
                 {
                     StatusCode = HttpStatusCode.OK,
                     Content = new StringContent(MockSearchParamterJson),
                 }));

            return fhirClient;
        }

        internal static Mock<IFhirClient> SetupSuccessfulPostSearchParameterOperationResponse(this Mock<IFhirClient> fhirClient)
        {
            fhirClient.Setup(c => c.Send(
                It.IsAny<HttpRequestMessage>(),
                It.IsAny<Uri>(),
                It.IsAny<string>()))
            .Returns(Task.FromResult(
                 new HttpResponseMessage
                 {
                     StatusCode = HttpStatusCode.OK,
                     Content = new StringContent(MockTranformedSearchParamterJson),
                 }));

            return fhirClient;
        }
    }

    /// <summary>
    /// A telemetry channel that simply calls a delegate.
    /// </summary>
    internal sealed class StubTelemetryChannel : ITelemetryChannel
    {
        private readonly Action<ITelemetry> _onSend;
        public StubTelemetryChannel(Action<ITelemetry> onSend) => _onSend = onSend ?? throw new ArgumentNullException(nameof(onSend));
        public bool? DeveloperMode { get; set; }
        public string EndpointAddress { get; set; } = "";
        public void Dispose() { }
        public void Flush() { }
        public void Send(ITelemetry item) => _onSend(item);
    }
}
