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

    /// <summary>
    /// Types of process
    /// </summary>
    public enum ProcessType
    {
        /// <summary>
        /// Import
        /// </summary>
        Import,

        /// <summary>
        /// Export
        /// </summary>
        Export,
    }
}
