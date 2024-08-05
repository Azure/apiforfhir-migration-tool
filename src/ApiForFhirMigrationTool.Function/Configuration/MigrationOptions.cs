﻿// -------------------------------------------------------------------------------------------------
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

        [JsonProperty("blobStorageUri")]
        public string BlobStorageUri { get; set; } = string.Empty;

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
        public List<string>? HistoryDeleteQuerySurface { get; set; } = new List<string> { "_history?_summary=count" };

        public List<string>? QueryDeep { get; set; } = new List<string> { "?_count=" };
        public List<string>? HistoryDeleteQueryDeep { get; set; } = new List<string> { "_history?_count=" };

        [JsonProperty("DeepCheckCount")]
        public int DeepCheckCount { get; set; }

        [JsonProperty("UserAgent")]
        public string UserAgent { get; set; } = "fhir-migration-tool";

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

        [JsonProperty("exportWithHistory")]
        public bool ExportWithHistory { get; set; } = false;

        [JsonProperty("exportWithDelete")]
        public bool ExportWithDelete { get; set; } = false;

        [JsonProperty("isParallel")]
        public bool IsParallel { get; set; } = true;

        [JsonProperty("isExportDeidentified")]
        public bool IsExportDeidentified { get; set; } = false;

        [JsonProperty("configFile")]
        public string ConfigFile { get; set; } = string.Empty;

        [JsonProperty("chunkLimit")]
        public int ChunkLimit = 100000000;

        [JsonProperty("resourceExportChunkTime")]
        public int ResourceExportChunkTime { get; set; } = 30;

        [JsonProperty("resourceTypes")]
        public List<string> ResourceTypes { get; set; } = new List<string>{ };

        [JsonProperty("payloadcount")]
        public int PayloadCount { get; set; } = 5;

        [JsonProperty("fileCount")]
        public int FileCount { get; set; } = 10000;

        [JsonProperty("stopDm")]
        public bool StopDm { get; set; } = false;

        [JsonProperty("startTime")]
        public int StartTime { get; set; } = 8;

        [JsonProperty("endTime")]
        public int EndTime { get; set; } = 17;

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
