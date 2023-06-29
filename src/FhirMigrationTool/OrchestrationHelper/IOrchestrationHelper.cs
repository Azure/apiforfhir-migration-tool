// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using FhirMigrationTool.Configuration;

namespace FhirMigrationTool.OrchestrationHelper
{
    public interface IOrchestrationHelper
    {
        string CreateImportRequest(string content, string importMode);

        bool ValidateConfig(MigrationOptions options);
    }
}
