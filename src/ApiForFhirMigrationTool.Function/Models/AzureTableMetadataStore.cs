// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using Azure.Data.Tables;

namespace ApiForFhirMigrationTool.Function.Models
{
    public class AzureTableMetadataStore : IMetadataStore
    {
        public AzureTableMetadataStore()
        {
        }

        public bool AddEntity(TableClient metadataTableClient, ITableEntity tableEntity, CancellationToken cancellationToken = default)
        {
                metadataTableClient.AddEntity(tableEntity, cancellationToken);
                return true;
        }

        public bool UpdateEntity(TableClient metadataTableClient, ITableEntity tableEntity, CancellationToken cancellationToken = default)
        {
                metadataTableClient.UpdateEntity(
                    tableEntity,
                    tableEntity.ETag,
                    cancellationToken: cancellationToken);
                return true;
        }

        public TableEntity GetEntity(TableClient metadataTableClient, string partitionKey, string rowKey, CancellationToken cancellationToken = default)
        {
            TableEntity getEntity = metadataTableClient.GetEntity<TableEntity>(
                    partitionKey,
                    rowKey,
                    cancellationToken: cancellationToken);

            return getEntity;
        }
    }
}
