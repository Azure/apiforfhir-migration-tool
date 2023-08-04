// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using Azure.Data.Tables;
using EnsureThat;

namespace ApiForFhirMigrationTool.Models
{
    public class AzureTableClientFactory : IAzureTableClientFactory
    {
        private readonly TableServiceClient _tableServiceClient;

        public AzureTableClientFactory(TableServiceClient tableServiceClient)
        {
            _tableServiceClient = tableServiceClient;
        }

        public TableClient Create(string tableName)
        {
            EnsureArg.IsNotNullOrWhiteSpace(tableName, nameof(tableName));

            TableClient tableClient = _tableServiceClient.GetTableClient(tableName);

            return tableClient;
        }
    }
}
