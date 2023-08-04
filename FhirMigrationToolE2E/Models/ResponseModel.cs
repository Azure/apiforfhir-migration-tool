// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

namespace FhirMigrationToolE2E.Models
{
    public class ResponseModel
    {
        public ResponseStatus Status { get; set; }

        public string Content { get; set; } = string.Empty;
    }
}
