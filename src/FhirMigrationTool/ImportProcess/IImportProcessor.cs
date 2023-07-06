﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

namespace FhirMigrationTool.ImportProcess
{
    public interface IImportProcessor
    {
        Task<HttpResponseMessage> CallImport(string requestContent);

        Task<HttpResponseMessage> CheckImportStatus(string statusUrl);
    }
}
