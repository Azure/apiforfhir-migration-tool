// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using ApiForFhirMigrationTool.Function.Configuration;
using ApiForFhirMigrationTool.Function.FhirOperation;
using ApiForFhirMigrationTool.Function.Models;
using ApiForFhirMigrationTool.Function.OrchestrationHelper;
using ApiForFhirMigrationTool.Function.Processors;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ApiForFhirMigrationTool.Function.UnitTests
{
    public class FhirMigrationUnitTests
    {
        private readonly MigrationOptions _config;
        private readonly Mock<IFhirClient> _mockClient;
        private readonly Mock<FunctionContext> _mockFunctionContext;
        private readonly Mock<IMetadataStore> _azureTableMetadataStore;
        private readonly Mock<IOrchestrationHelper> _orchestrationHelper;

        public FhirMigrationUnitTests()
        {
            _mockClient = new Mock<IFhirClient>();
            _mockFunctionContext = new Mock<FunctionContext>();
            _azureTableMetadataStore = new Mock<IMetadataStore>();
            _orchestrationHelper = new Mock<IOrchestrationHelper>();

            _config = new MigrationOptions
            {
                SourceUri = new Uri(TestHelpers.TestSourceUri),
                DestinationUri = new Uri(TestHelpers.TestDestinationUri),
            };
        }

        [Fact]
        public async Task GivenAnExportRequest_WhenFhirReturnsSuccess_ShouldReturnResponseWithCompleted()
        {
            _mockClient.SetupSuccessfulExportOperationResponse();

            IFhirProcessor exportProcessor = new FhirProcessor(
                fhirClient: _mockClient.Object,
                telemetryClient: null,
                logger: NullLogger.Instance as ILogger<FhirProcessor>);

            ResponseModel exportResponse = await exportProcessor.CallProcess(HttpMethod.Get, string.Empty, _config.SourceUri, "/$export?_type=Patient", _config.SourceHttpClient);
            var statusUrl = exportResponse.Content;

            Assert.Equal(ResponseStatus.Accepted, exportResponse.Status);
            Assert.Equal(TestHelpers.ExportStatusUrl, statusUrl);
        }

        [Fact]
        public async Task GivenAnExportStatusRequest_WhenFhirOperationIsDone_ShouldReturnCompleted()
        {
            _mockClient.SetupSuccessfulExportStatusResponse();

            IFhirProcessor exportProcessor = new FhirProcessor(
                _mockClient.Object,
                telemetryClient: null,
                logger: NullLogger.Instance as ILogger<FhirProcessor>);

            ResponseModel exportStatusResponse = await exportProcessor.CheckProcessStatus(TestHelpers.ExportStatusUrl, _config.SourceUri, _config.SourceHttpClient);

            Assert.Equal(ResponseStatus.Completed, exportStatusResponse.Status);
        }

        [Fact]
        public async Task GivenAnImportRequest_WhenFhirReturnsSuccess_ShouldReturnResponseWithCompleted()
        {
            _mockClient.SetupSuccessfulImportOperationResponse();

            IFhirProcessor importProcessor = new FhirProcessor(
                fhirClient: _mockClient.Object,
                telemetryClient: null,
                logger: NullLogger.Instance as ILogger<FhirProcessor>);

            ResponseModel importResponse = await importProcessor.CallProcess(HttpMethod.Post, TestHelpers.TestImportBody, _config.DestinationUri, "/$import", _config.DestinationHttpClient);
            var statusUrl = importResponse.Content;

            Assert.Equal(ResponseStatus.Accepted, importResponse.Status);
            Assert.Equal(TestHelpers.ImportStatusUrl, statusUrl);
        }

        [Fact]
        public async Task GivenAnImportStatusRequest_WhenFhirOperationIsDone_ShouldReturnCompleted()
        {
            _mockClient.SetupSuccessfulImportStatusResponse();

            IFhirProcessor importProcessor = new FhirProcessor(
                    fhirClient: _mockClient.Object,
                    telemetryClient: null,
                    logger: NullLogger.Instance as ILogger<FhirProcessor>);

            ResponseModel importStatusResponse = await importProcessor.CheckProcessStatus(TestHelpers.ImportStatusUrl, _config.DestinationUri, _config.DestinationHttpClient);

            Assert.Equal(ResponseStatus.Completed, importStatusResponse.Status);
        }

        [Fact]
        public async Task GivenAMigrationWorkflow_WhenAllOperationsAreSuccess_OrchestratorReturnCorrectStatusCodes()
        {
            var exportProcessor = new Mock<IFhirProcessor>()
                                    .SetupSuccessfulExportOperationResponse()
                                    .SetupSuccessfulExportStatusResponse();

            var importProcessor = new Mock<IFhirProcessor>()
                                    .SetupSuccessfulImportOperationResponse()
                                    .SetupSuccessfulImportStatusResponse();

            var exportResponse = await TestHelpers.GetTestExportOrchestrator(_config, exportProcessor.Object)
                                        .ProcessExport(string.Empty, _mockFunctionContext.Object);
            var exportStatusResponse = await TestHelpers.GetTestExportStatusOrchestrator(_config, exportProcessor.Object)
                                        .ProcessExportStatusCheck(exportResponse.Content, _mockFunctionContext.Object);
            var importResponse = await TestHelpers.GetTestImportOrchestrator(_config, importProcessor.Object)
                                        .ProcessImport(exportStatusResponse.Content, _mockFunctionContext.Object);
            var importStatusResponse = await TestHelpers.GetTestImportStatusOrchestrator(_config, importProcessor.Object)
                                        .ProcessImportStatusCheck(importResponse.Content, _mockFunctionContext.Object);

            Assert.Equal(ResponseStatus.Accepted, exportResponse?.Status);
            Assert.Equal(ResponseStatus.Completed, exportStatusResponse?.Status);
            Assert.Equal(ResponseStatus.Accepted, importResponse?.Status);
            Assert.Equal(ResponseStatus.Completed, importStatusResponse?.Status);
        }

        [Fact]
        public async Task GivenAMigrationWorkflow_WhenExportOperationFails_OrchestratorReturnCorrectStatusCodes()
        {
            var exportProcessor = new Mock<IFhirProcessor>()
                                    .SetupFailedExportOperationResponse();

            var exportResponse = await TestHelpers.GetTestExportOrchestrator(_config, exportProcessor.Object)
                                        .ProcessExport(string.Empty, _mockFunctionContext.Object);

            Assert.Equal(ResponseStatus.Failed, exportResponse.Status);
        }

        [Fact]
        public async Task GivenAMigrationWorkflow_WhenExportStatusCheckFails_OrchestratorReturnCorrectStatusCodes()
        {
            var exportProcessor = new Mock<IFhirProcessor>()
                                    .SetupSuccessfulExportOperationResponse()
                                    .SetupFailedExportStatusResponse();

            var exportResponse = await TestHelpers.GetTestExportOrchestrator(_config, exportProcessor.Object)
                                        .ProcessExport(string.Empty, _mockFunctionContext.Object);
            var exportStatusResponse = await TestHelpers.GetTestExportStatusOrchestrator(_config, exportProcessor.Object)
                                        .ProcessExportStatusCheck(exportResponse.Content, _mockFunctionContext.Object);

            Assert.Equal(ResponseStatus.Accepted, exportResponse.Status);
            Assert.Equal(ResponseStatus.Failed, exportStatusResponse.Status);
    }

        [Fact]
        public async Task GivenAMigrationWorkflow_WhenImportOperationFails_OrchestratorReturnCorrectStatusCodes()
        {
            var exportProcessor = new Mock<IFhirProcessor>()
                                    .SetupSuccessfulExportOperationResponse()
                                    .SetupSuccessfulExportStatusResponse();

            var importProcessor = new Mock<IFhirProcessor>()
                                    .SetupFailedImportOperationResponse();

            var exportResponse = await TestHelpers.GetTestExportOrchestrator(_config, exportProcessor.Object)
                                        .ProcessExport(string.Empty, _mockFunctionContext.Object);
            var exportStatusResponse = await TestHelpers.GetTestExportStatusOrchestrator(_config, exportProcessor.Object)
                                        .ProcessExportStatusCheck(exportResponse.Content, _mockFunctionContext.Object);
            var importResponse = await TestHelpers.GetTestImportOrchestrator(_config, importProcessor.Object)
                                        .ProcessImport(exportStatusResponse.Content, _mockFunctionContext.Object);

            Assert.Equal(ResponseStatus.Accepted, exportResponse.Status);
            Assert.Equal(ResponseStatus.Completed, exportStatusResponse.Status);
            Assert.Equal(ResponseStatus.Failed, importResponse.Status);
        }

        [Fact]
        public async Task GivenAMigrationWorkflow_WhenImportStatusCheckFails_OrchestratorReturnCorrectStatusCodes()
        {
            var exportProcessor = new Mock<IFhirProcessor>()
                                    .SetupSuccessfulExportOperationResponse()
                                    .SetupSuccessfulExportStatusResponse();

            var importProcessor = new Mock<IFhirProcessor>()
                                    .SetupSuccessfulImportOperationResponse()
                                    .SetupFailedImportStatusResponse();

            var exportResponse = await TestHelpers.GetTestExportOrchestrator(_config, exportProcessor.Object)
                                            .ProcessExport(string.Empty, _mockFunctionContext.Object);
            var exportStatusResponse = await TestHelpers.GetTestExportStatusOrchestrator(_config, exportProcessor.Object)
                                            .ProcessExportStatusCheck(exportResponse.Content, _mockFunctionContext.Object);
            var importResponse = await TestHelpers.GetTestImportOrchestrator(_config, importProcessor.Object)
                                            .ProcessImport(exportStatusResponse.Content, _mockFunctionContext.Object);
            var importStatusResponse = await TestHelpers.GetTestImportStatusOrchestrator(_config, importProcessor.Object)
                                            .ProcessImportStatusCheck(importResponse.Content, _mockFunctionContext.Object);

            Assert.Equal(ResponseStatus.Accepted, exportResponse.Status);
            Assert.Equal(ResponseStatus.Completed, exportStatusResponse.Status);
            Assert.Equal(ResponseStatus.Accepted, importResponse.Status);
            Assert.Equal(ResponseStatus.Failed, importStatusResponse.Status);
        }
    }
}
