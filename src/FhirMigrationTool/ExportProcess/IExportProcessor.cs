// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

namespace FhirMigrationTool.ExportProcess
{
    public interface IExportProcessor
    {
        Task<HttpResponseMessage> CallExport();

        Task<HttpResponseMessage> CheckExportStatus(string statusUrl);
    }
}
