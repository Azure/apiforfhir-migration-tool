// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------
using System.Net;
using ApiForFhirMigrationTool.FhirOperation;
using ApiForFhirMigrationTool.Models;
using ApiForFhirMigrationTool.Processors;
using Moq;

namespace FhirMigrationTool.Tests.Unit
{
    internal static class TestHelpers
    {
        internal const string TestSourceUri = "https://azureapiforfhir.azurehealthcareapis.com";
        internal const string TestDestinationUri = "https://fhirservice.fhir.azurehealthcareapis.com";
        internal const string ExportStatusUrl = $"{TestSourceUri}/_operations/export/importId";
        internal const string ImportStatusUrl = $"{TestDestinationUri}/_operations/import/importId";

        internal static string MockExportStatusResponse => File.ReadAllText("../../../TestFiles/mock_export_status_response.json");

        internal static string MockImportStatusResponse => File.ReadAllText("../../../TestFiles/mock_import_status_response.json");

        internal static string TestImportBody => File.ReadAllText("../../../TestFiles/import_body.json");

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
    }
}
