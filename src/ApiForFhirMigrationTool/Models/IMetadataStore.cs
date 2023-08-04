// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using Azure.Data.Tables;

namespace ApiForFhirMigrationTool.Models
{
    public interface IMetadataStore
    {
        /// <summary>
        /// Attempts to add entity to azure table
        /// </summary>
        /// <param name="metadataTableClient">the table client.</param>
        /// <param name="tableEntity">the table entity to add.</param>
        /// <param name="cancellationToken">cancellation token.</param>
        /// <returns>return true if add entity successfully, return false if the entity already exists.</returns>
        public bool AddEntity(TableClient metadataTableClient, ITableEntity tableEntity, CancellationToken cancellationToken = default);

        /// <summary>
        /// Attempts to update entity to azure table
        /// </summary>
        /// <param name="metadataTableClient">the table client.</param>
        /// <param name="tableEntity">the table entity to update.</param>
        /// <param name="cancellationToken">cancellation token.</param>
        /// <returns>return true if update entity successfully, return false if etag is not satisfied.</returns>
        public bool UpdateEntity(TableClient metadataTableClient, ITableEntity tableEntity, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get current trigger entity from azure table
        /// </summary>
        /// <param name="metadataTableClient">the table client.</param>
        /// <param name="partitionKey">Partition Key.</param>
        /// <param name="rowKey">Row Key.</param>
        /// <param name="cancellationToken">cancellation token.</param>
        /// <returns>Current trigger entity, return null if does not exist.</returns>
        public TableEntity GetEntity(TableClient metadataTableClient, string partitionKey, string rowKey, CancellationToken cancellationToken = default);
    }
}
