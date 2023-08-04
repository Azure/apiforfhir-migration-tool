﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using FhirMigrationToolE2E.Models;

namespace FhirMigrationToolE2E.Processors
{
    public interface IFhirProcessor
    {
        Task<ResponseModel> CallProcess(HttpMethod method, string requestContent, Uri baseUri, string queryString, string endpoint);

        Task<ResponseModel> CheckProcessStatus(string statusUrl, Uri baseUri, string endpoint);
    }
}
