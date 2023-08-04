// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using FhirMigrationTool.Configuration;
using FhirMigrationTool.FhirOperation;
using FhirMigrationTool.Models;
using FhirMigrationTool.Processors;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace FhirMigrationTool.Test
{
    [TestClass]
    public class FhirMigrationUnitTests
    {
        private readonly MigrationOptions _config;
        private readonly Mock<IFhirClient> _mockClient;
        private readonly Mock<IFhirProcessor> _exportProcessor;
        private readonly Mock<IFhirProcessor> _importProcessor;
        private readonly Mock<FunctionContext> _mockFunctionContext;

        // private readonly Mock<IImportProcessor> _importProcessor;
        public FhirMigrationUnitTests()
        {
            _mockClient = new Mock<IFhirClient>();
            _exportProcessor = new Mock<IFhirProcessor>();
            _importProcessor = new Mock<IFhirProcessor>();
            _mockFunctionContext = new Mock<FunctionContext>();

            _config = new MigrationOptions
            {
                SourceUri = new Uri(TestHelpers.TestSourceUri),
                DestinationUri = new Uri(TestHelpers.TestDestinationUri),
            };
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

            var exportOrchestrator = new ExportOrchestrator(_exportProcessor.Object, options: _config);
            var importOrchestrator = new ImportOrchestrator(_importProcessor.Object, options: _config);

            var exportResponse = await exportOrchestrator.ProcessExport(string.Empty, _mockFunctionContext.Object);
            var exportStatusResponse = await exportOrchestrator.ProcessExportStatusCheck(exportResponse.Content, _mockFunctionContext.Object);
            var importResponse = await importOrchestrator.ProcessImport(exportStatusResponse.Content, _mockFunctionContext.Object);
            var importStatusResponse = await importOrchestrator.ProcessImportStatusCheck(importResponse.Content, _mockFunctionContext.Object);

            Assert.IsTrue(exportResponse?.Status == ResponseStatus.Accepted);
            Assert.IsTrue(exportStatusResponse?.Status == ResponseStatus.Completed);
            Assert.IsTrue(importResponse?.Status == ResponseStatus.Accepted);
            Assert.IsTrue(importStatusResponse?.Status == ResponseStatus.Completed);
        }

        [TestMethod]
        public async Task MigrationTestExportFail()
        {
            _exportProcessor.SetupFailedExportOperationResponse();

            var exportOrchestrator = new ExportOrchestrator(_exportProcessor.Object, options: _config);

            ResponseModel exportResponse = await exportOrchestrator.ProcessExport(string.Empty, _mockFunctionContext.Object);

            Assert.IsTrue(exportResponse.Status == ResponseStatus.Failed);
        }

        [TestMethod]
        public async Task MigrationTestExportStatusFail()
        {
            _exportProcessor
                .SetupSuccessfulExportOperationResponse()
                .SetupFailedExportStatusResponse();

            var exportOrchestrator = new ExportOrchestrator(_exportProcessor.Object, options: _config);

            ResponseModel exportResponse = await exportOrchestrator.ProcessExport(string.Empty, _mockFunctionContext.Object);
            ResponseModel exportStatusResponse = await exportOrchestrator.ProcessExportStatusCheck(exportResponse.Content, _mockFunctionContext.Object);

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

            var exportOrchestrator = new ExportOrchestrator(_exportProcessor.Object, options: _config);
            var importOrchestrator = new ImportOrchestrator(_importProcessor.Object, options: _config);

            var exportResponse = await exportOrchestrator.ProcessExport(string.Empty, _mockFunctionContext.Object);
            var exportStatusResponse = await exportOrchestrator.ProcessExportStatusCheck(exportResponse.Content, _mockFunctionContext.Object);
            var importResponse = await importOrchestrator.ProcessImport(exportStatusResponse.Content, _mockFunctionContext.Object);

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

            var exportOrchestrator = new ExportOrchestrator(_exportProcessor.Object, options: _config);
            var importOrchestrator = new ImportOrchestrator(_importProcessor.Object, options: _config);

            var exportResponse = await exportOrchestrator.ProcessExport(string.Empty, _mockFunctionContext.Object);
            var exportStatusResponse = await exportOrchestrator.ProcessExportStatusCheck(exportResponse.Content, _mockFunctionContext.Object);
            var importResponse = await importOrchestrator.ProcessImport(exportStatusResponse.Content, _mockFunctionContext.Object);
            var importStatusResponse = await importOrchestrator.ProcessImportStatusCheck(importResponse.Content, _mockFunctionContext.Object);

            Assert.IsTrue(exportResponse.Status == ResponseStatus.Accepted);
            Assert.IsTrue(exportStatusResponse.Status == ResponseStatus.Completed);
            Assert.IsTrue(importResponse.Status == ResponseStatus.Accepted);
            Assert.IsTrue(importStatusResponse.Status == ResponseStatus.Failed);
        }
    }
}
