// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using ApiForFhirMigrationTool;
using ApiForFhirMigrationTool.Configuration;
using ApiForFhirMigrationTool.FhirOperation;
using ApiForFhirMigrationTool.Models;
using ApiForFhirMigrationTool.OrchestrationHelper;
using ApiForFhirMigrationTool.Processors;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace FhirMigrationTool.Tests.Unit
{
    [TestClass]
    public class FhirMigrationUnitTests
    {
        private readonly MigrationOptions _config;
        private readonly Mock<IFhirClient> _mockClient;
        private readonly Mock<IFhirProcessor> _exportProcessor;
        private readonly Mock<IFhirProcessor> _importProcessor;
        private readonly Mock<FunctionContext> _mockFunctionContext;
        private readonly Mock<IAzureTableClientFactory> _azureTableClientFactory;
        private readonly Mock<IMetadataStore> _azureTableMetadataStore;
        private readonly Mock<TableClient> _azureTableClient;
        private static TableEntity _azureTableEntity = new TableEntity();
        private readonly Mock<IOrchestrationHelper> _orchestrationHelper;
        private readonly ExportOrchestrator _testExportOrchestrator;
        private readonly ExportStatusOrchestrator _testExportStatusOrchestrator;
        private readonly ImportOrchestrator _testImportOrchestrator;
        private readonly ImportStatusOrchestrator _testImportStatusOrchestrator;

        public FhirMigrationUnitTests()
        {
            _mockClient = new Mock<IFhirClient>();
            _exportProcessor = new Mock<IFhirProcessor>();
            _importProcessor = new Mock<IFhirProcessor>();
            _mockFunctionContext = new Mock<FunctionContext>();
            _azureTableClientFactory = new Mock<IAzureTableClientFactory>();
            _azureTableMetadataStore = new Mock<IMetadataStore>();
            _azureTableClient = new Mock<TableClient>();
            _orchestrationHelper = new Mock<IOrchestrationHelper>();

            _config = new MigrationOptions
            {
                SourceUri = new Uri(TestHelpers.TestSourceUri),
                DestinationUri = new Uri(TestHelpers.TestDestinationUri),
            };

            _testExportOrchestrator = new ExportOrchestrator(
                                _exportProcessor.Object,
                                _config,
                                _azureTableClientFactory.Object,
                                _azureTableMetadataStore.Object,
                                _mockClient.Object,
                                _orchestrationHelper.Object);

            _testExportStatusOrchestrator = new ExportStatusOrchestrator(
                                            _exportProcessor.Object,
                                            _config,
                                            _azureTableClientFactory.Object,
                                            _azureTableMetadataStore.Object,
                                            _mockClient.Object,
                                            _orchestrationHelper.Object);

            _testImportOrchestrator = new ImportOrchestrator(
                                            _importProcessor.Object,
                                            _config,
                                            _azureTableClientFactory.Object,
                                            _azureTableMetadataStore.Object,
                                            _orchestrationHelper.Object);

            _testImportStatusOrchestrator = new ImportStatusOrchestrator(
                                            _importProcessor.Object,
                                            _config,
                                            _azureTableClientFactory.Object,
                                            _azureTableMetadataStore.Object);
        }

        [ClassInitialize]
        public void Initialize(TestContext context)
        {
            _azureTableClientFactory.Setup(x => x.Create(It.IsAny<string>())).Returns(_azureTableClient.Object);
            _azureTableMetadataStore.Setup(x => x.GetEntity(It.IsAny<TableClient>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(_azureTableEntity!);
            _azureTableMetadataStore.Setup(x => x.AddEntity(It.IsAny<TableClient>(), It.IsAny<TableEntity>(), It.IsAny<CancellationToken>())).Returns(true);
            _azureTableMetadataStore.Setup(x => x.UpdateEntity(It.IsAny<TableClient>(), It.IsAny<TableEntity>(), It.IsAny<CancellationToken>())).Returns(true);
        }

        [TestMethod]
        public async Task ExportProcessorTestCasePass()
        {
            _mockClient.SetupSuccessfulExportOperationResponse();

            IFhirProcessor exportProcessor = new FhirProcessor(
                fhirClient: _mockClient.Object,
                telemetryClient: null,
                logger: NullLogger.Instance as ILogger<FhirProcessor>);

            ResponseModel exportResponse = await exportProcessor.CallProcess(HttpMethod.Get, string.Empty, _config.SourceUri, "/$export?_type=Patient", _config.SourceHttpClient);
            var statusUrl = exportResponse.Content;

            Assert.IsTrue(exportResponse.Status == ResponseStatus.Completed);
            Assert.AreEqual(statusUrl, TestHelpers.ExportStatusUrl);
        }

        [TestMethod]
        public async Task ExportStatusTestCaseCompleted()
        {
            _mockClient.SetupSuccessfulExportStatusResponse();

            IFhirProcessor exportProcessor = new FhirProcessor(
                _mockClient.Object,
                telemetryClient: null,
                logger: NullLogger.Instance as ILogger<FhirProcessor>);

            ResponseModel exportStatusResponse = await exportProcessor.CheckProcessStatus(TestHelpers.ExportStatusUrl, _config.SourceUri, _config.SourceHttpClient);
            Assert.IsTrue(exportStatusResponse.Status == ResponseStatus.Completed);
        }

        [TestMethod]
        public async Task ImportProcessorTestCasePass()
        {
            _mockClient.SetupSuccessfulImportOperationResponse();

            IFhirProcessor importProcessor = new FhirProcessor(
                fhirClient: _mockClient.Object,
                telemetryClient: null,
                logger: NullLogger.Instance as ILogger<FhirProcessor>);

            ResponseModel importResponse = await importProcessor.CallProcess(HttpMethod.Post, TestHelpers.TestImportBody, _config.DestinationUri, "/$import", _config.DestinationHttpClient);
            var statusUrl = importResponse.Content;

            Assert.IsTrue(importResponse.Status == ResponseStatus.Completed);
            Assert.AreEqual(statusUrl, TestHelpers.ImportStatusUrl);
        }

        [TestMethod]
        public async Task ImportStatusTestCaseCompleted()
        {
            _mockClient.SetupSuccessfulImportStatusResponse();

            IFhirProcessor importProcessor = new FhirProcessor(
                    fhirClient: _mockClient.Object,
                    telemetryClient: null,
                    logger: NullLogger.Instance as ILogger<FhirProcessor>);

            ResponseModel importStatusResponse = await importProcessor.CheckProcessStatus(TestHelpers.ImportStatusUrl, _config.DestinationUri, _config.DestinationHttpClient);

            Assert.IsTrue(importStatusResponse.Status == ResponseStatus.Completed);
        }

        [TestMethod]
        public async Task MigrationTestPass()
        {
            _exportProcessor
                .SetupSuccessfulExportOperationResponse()
                .SetupSuccessfulExportStatusResponse();

            _importProcessor
                .SetupSuccessfulImportOperationResponse()
                .SetupSuccessfulImportStatusResponse();

            var exportResponse = await _testExportOrchestrator.ProcessExport(string.Empty, _mockFunctionContext.Object);
            var exportStatusResponse = await _testExportStatusOrchestrator.ProcessExportStatusCheck(exportResponse.Content, _mockFunctionContext.Object);
            var importResponse = await _testImportOrchestrator.ProcessImport(exportStatusResponse.Content, _mockFunctionContext.Object);
            var importStatusResponse = await _testImportStatusOrchestrator.ProcessImportStatusCheck(importResponse.Content, _mockFunctionContext.Object);

            Assert.IsTrue(exportResponse?.Status == ResponseStatus.Accepted);
            Assert.IsTrue(exportStatusResponse?.Status == ResponseStatus.Completed);
            Assert.IsTrue(importResponse?.Status == ResponseStatus.Accepted);
            Assert.IsTrue(importStatusResponse?.Status == ResponseStatus.Completed);
        }

        [TestMethod]
        public async Task MigrationTestExportFail()
        {
            _exportProcessor.SetupFailedExportOperationResponse();

            ResponseModel exportResponse = await _testExportOrchestrator.ProcessExport(string.Empty, _mockFunctionContext.Object);

            Assert.IsTrue(exportResponse.Status == ResponseStatus.Failed);
        }

        [TestMethod]
        public async Task MigrationTestExportStatusFail()
        {
            _exportProcessor
                .SetupSuccessfulExportOperationResponse()
                .SetupFailedExportStatusResponse();

            ResponseModel exportResponse = await _testExportOrchestrator.ProcessExport(string.Empty, _mockFunctionContext.Object);
            ResponseModel exportStatusResponse = await _testExportStatusOrchestrator.ProcessExportStatusCheck(exportResponse.Content, _mockFunctionContext.Object);

            Assert.IsTrue(exportResponse.Status == ResponseStatus.Accepted);
            Assert.IsTrue(exportStatusResponse.Status == ResponseStatus.Failed);
    }

        [TestMethod]
        public async Task MigrationTestImportFail()
        {
            _exportProcessor
                .SetupSuccessfulExportOperationResponse()
                .SetupSuccessfulExportStatusResponse();

            _importProcessor
                .SetupFailedImportOperationResponse();

            var exportResponse = await _testExportOrchestrator.ProcessExport(string.Empty, _mockFunctionContext.Object);
            var exportStatusResponse = await _testExportStatusOrchestrator.ProcessExportStatusCheck(exportResponse.Content, _mockFunctionContext.Object);
            var importResponse = await _testImportOrchestrator.ProcessImport(exportStatusResponse.Content, _mockFunctionContext.Object);

            Assert.IsTrue(exportResponse.Status == ResponseStatus.Accepted);
            Assert.IsTrue(exportStatusResponse.Status == ResponseStatus.Completed);
            Assert.IsTrue(importResponse.Status == ResponseStatus.Failed);
        }

        [TestMethod]
        public async Task MigrationTestImportStatusFail()
        {
            _exportProcessor
                .SetupSuccessfulExportOperationResponse()
                .SetupSuccessfulExportStatusResponse();

            _importProcessor
                .SetupSuccessfulImportOperationResponse()
                .SetupFailedImportStatusResponse();

            var exportResponse = await _testExportOrchestrator.ProcessExport(string.Empty, _mockFunctionContext.Object);
            var exportStatusResponse = await _testExportStatusOrchestrator.ProcessExportStatusCheck(exportResponse.Content, _mockFunctionContext.Object);
            var importResponse = await _testImportOrchestrator.ProcessImport(exportStatusResponse.Content, _mockFunctionContext.Object);
            var importStatusResponse = await _testImportStatusOrchestrator.ProcessImportStatusCheck(importResponse.Content, _mockFunctionContext.Object);

            Assert.IsTrue(exportResponse.Status == ResponseStatus.Accepted);
            Assert.IsTrue(exportStatusResponse.Status == ResponseStatus.Completed);
            Assert.IsTrue(importResponse.Status == ResponseStatus.Accepted);
            Assert.IsTrue(importStatusResponse.Status == ResponseStatus.Failed);
        }
    }
}
