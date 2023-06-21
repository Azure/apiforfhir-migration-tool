// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

namespace FhirMigrationTool.OrchestrationHelper
{
    public interface IOrchestrationHelper
    {
        string CreateImportRequest(HttpResponseMessage exportStatusResponse, string importMode);
    }
}
