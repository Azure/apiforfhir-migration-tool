﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using Azure.Data.Tables;

namespace ApiForFhirMigrationTool.Function.Models
{
    public interface IAzureTableClientFactory
    {
        /// <summary>
        /// Create metadata table client
        /// </summary>
        /// <param name="tableName">table name</param>
        /// <returns>TableClient</returns>
        public TableClient Create(string tableName);
    }
}
