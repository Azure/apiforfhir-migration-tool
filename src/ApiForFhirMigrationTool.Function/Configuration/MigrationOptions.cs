// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using Azure.Core;
using Azure.Identity;
using Newtonsoft.Json;

namespace ApiForFhirMigrationTool.Function.Configuration
{
    public class MigrationOptions
    {
        [JsonProperty("sourceFhirUri")]
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public Uri SourceUri { get; set; }

        [JsonProperty("destinationFhirUri")]
        public Uri DestinationUri { get; set; }

#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        [JsonProperty("stagingStorageAccountName")]
        public string StagingStorageAccountName { get; set; } = string.Empty;

        [JsonProperty("stagingStorageUri")]
        public string StagingStorageUri { get; set; } = string.Empty;

        [JsonProperty("stagingContainerName")]
        public string StagingContainerName { get; set; } = string.Empty;

        [JsonProperty("scheduleInterval")]
        public double ScheduleInterval { get; set; }

        [JsonProperty("startDate")]
        public DateTime StartDate { get; set; }

        [JsonProperty("endDate")]
        public string EndDate { get; set; } = string.Empty;

        [JsonProperty("AppInsightConnectionString")]
        public string AppInsightConnectionstring { get; set; } = string.Empty;

        [JsonProperty("importMode")]
        public string ImportMode { get; set; } = "IncrementalLoad";

        public List<string>? SurfaceCheckResources { get; set; } = new List<string> { "Patient" };

        public List<string>? QuerySurface { get; set; } = new List<string> { "?_summary=Count" };

        public List<string>? QueryDeep { get; set; } = new List<string> { "?_count=" };

        [JsonProperty("DeepCheckCount")]
        public int DeepCheckCount { get; set; }

        [JsonProperty("UserAgent")]
        public string UserAgent { get; set; } = "FhirMigrationTool";

        [JsonProperty("SourceHttpClient")]
        public string SourceHttpClient { get; set; } = "SourceFhirEndpoint";

        [JsonProperty("DestinationHttpClient")]
        public string DestinationHttpClient { get; set; } = "DestinationFhirEndpoint";

        [JsonProperty("TokenCredential")]
        public TokenCredential TokenCredential { get; set; } = new DefaultAzureCredential();

        [JsonProperty("retryCount")]
        public int RetryCount { get; set; }

        [JsonProperty("waitForRetry")]
        public double WaitForRetry { get; set; }

        [JsonProperty("debug")]
        public bool Debug { get; set; }

        [JsonProperty("exportTableName")]
        public string ExportTableName { get; set; } = string.Empty;

        [JsonProperty("chunkTableName")]
        public string ChunkTableName { get; set; } = string.Empty;

        [JsonProperty("ExportChunkTime")]
        public int ExportChunkTime { get; set; } = 30;

        [JsonProperty("ExportChunkDuration")]
        public string ExportChunkDuration { get; set; } = "Days";

        [JsonProperty("partitionKey")]
        public string PartitionKey { get; set; } = "mypartitionkey";

        [JsonProperty("rowKey")]
        public string RowKey { get; set; } = "myrowkey";

        public bool ValidateConfig()
        {
            if (SourceUri != null
                && DestinationUri != null
                && !string.IsNullOrEmpty(ImportMode)
                && ScheduleInterval > 0)
            {
                return true;
            }
            else
            {
                throw new ArgumentException($"Process exiting: Please make sure that the required configuration values are available.");
            }
        }
    }
}
