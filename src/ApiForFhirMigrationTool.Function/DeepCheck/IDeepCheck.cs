// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

namespace ApiForFhirMigrationTool.Function.DeepCheck
{
    public interface IDeepCheck
    {
        Task<string> Execute(string query);
    }
}
