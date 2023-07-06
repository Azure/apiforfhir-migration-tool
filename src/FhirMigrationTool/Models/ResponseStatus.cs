// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

namespace FhirMigrationTool.Models
{
    /// <summary>
    /// Types of statuses
    /// </summary>
    public enum ResponseStatus
    {
        /// <summary>
        /// Response Failed
        /// </summary>
        Failed,

        /// <summary>
        /// response Ok
        /// </summary>
        Completed,

        /// <summary>
        /// Response Accepted
        /// </summary>
        Accepted,

        /// <summary>
        /// Response 429
        /// </summary>
        Retry,
    }
}
