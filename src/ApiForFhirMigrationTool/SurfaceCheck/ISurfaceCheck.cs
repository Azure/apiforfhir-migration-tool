// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

namespace ApiForFhirMigrationTool.SurfaceCheck
{
    public interface ISurfaceCheck
    {
        Task<string> Execute(string query);
    }
}
