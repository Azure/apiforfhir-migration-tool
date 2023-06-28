// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using Newtonsoft.Json;

namespace FhirMigrationTool.Configuration
{
    public class MigrationOptions
    {
        [JsonProperty("sourceFhirUri")]
        public string SourceFhirUri { get; set; } = string.Empty;

        [JsonProperty("destinationFhirUri")]
        public string DestinationFhirUri { get; set; } = string.Empty;

        [JsonProperty("stagingStorageAccountName")]
        public string StagingStorageAccountName { get; set; } = string.Empty;

        [JsonProperty("stagingContainerName")]
        public string StagingContainerName { get; set; } = string.Empty;

        [JsonProperty("scheduleInterval")]
        public string ScheduleInterval { get; set; } = string.Empty;

        [JsonProperty("startDate")]
        public string StartDate { get; set; } = string.Empty;

        [JsonProperty("endDate")]
        public string EndDate { get; set; } = string.Empty;

        [JsonProperty("AppInsightConnectionString")]
        public string AppInsightConnectionstring { get; set; } = string.Empty;

        [JsonProperty("importMode")]
        public string ImportMode { get; set; } = "IncrementalLoad";

        public List<string>? SurfaceCheckResources { get; set; }

        [JsonProperty("DeepCheckCount")]
        public int DeepCheckCount { get; set; }

        [JsonProperty("UserAgent")]
        public string UserAgent { get; set; } = "FhirMigrationTool";

        [JsonProperty("SourceHttpClient")]
        public string SourceHttpClient { get; set; } = "SourceFhirEndpoint";

        [JsonProperty("DestinationHttpClient")]
        public string DestinationHttpClient { get; set; } = "DestinationFhirEndpoint";
    }
}
