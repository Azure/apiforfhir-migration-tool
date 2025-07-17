// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using Newtonsoft.Json.Linq;

namespace ApiForFhirMigrationTool.Function.OrchestrationHelper
{
    public interface IOrchestrationHelper
    {
        (int,ulong) CreateImportRequest(string content, string importMode, string statusUrl);

        string GetProcessId(string statusUrl);

        ulong CalculateSumOfResources(JArray output);
    }
}
