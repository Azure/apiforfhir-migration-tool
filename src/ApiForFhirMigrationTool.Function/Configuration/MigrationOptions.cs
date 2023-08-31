// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.ComponentModel.DataAnnotations;
using Azure.Core;
using Azure.Identity;

namespace ApiForFhirMigrationTool.Function.Configuration
{
    public class MigrationOptions
    {
        [Required]
        [Url]
        required public Uri? SourceFhirUri { get; set; }

        [Required]
        [Url]
        required public Uri? TargetFhirUri { get; set; }

        [Url]
        public Uri? StagingStorageUri { get; set; }

        public string? StagingStorageConnectionString { get; set; }

        public string StagingContainerName { get; set; } = "migration";

        // #TODO - change this to a reasonable value. We should default to something.
        // The range should also be checked - this is a placeholder.
        [Range(1, 1000, ErrorMessage = "Please enter a value bigger than {1} and smaller than {2} for ScheduleInterval.")]
        public double ScheduleInterval { get; set; } = 10;

        // #TODO - why is the StartDate a DateTime and EndDate a string?
        public DateTime StartDate { get; set; }

        public string EndDate { get; set; } = string.Empty;

        public string? AppInsightConnectionString { get; set; }

        // #TODO - should this be an enum?
        [Required]
        public string ImportMode { get; set; } = "IncrementalLoad";

        public List<string>? SurfaceCheckResources { get; set; }

        public List<string>? QuerySurface { get; set; }

        public List<string>? QueryDeep { get; set; }

        public int DeepCheckCount { get; set; }

        public string UserAgent { get; set; } = "FhirMigrationTool";

        public string SourceHttpClient { get; set; } = "SourceFhirEndpoint";

        public string DestinationHttpClient { get; set; } = "DestinationFhirEndpoint";

        public TokenCredential TokenCredential { get; set; } = new DefaultAzureCredential();

        public int RetryCount { get; set; }

        public double WaitForRetry { get; set; }

        public bool Debug { get; set; }

        public bool EnableTimers { get; set; } = true;

        [Required]
        public string ExportTableName { get; set; } = "MigrationExportState";

        [Required]
        public string ChunkTableName { get; set; } = "MigrationChunkState";

        // #TODO - The range is a placeholder - should be checked.
        [Range(1, 1000, ErrorMessage = "Please enter a value bigger than {1} and smaller than {2} for ExportChunkTime.")]
        [Required]
        public int ExportChunkTime { get; set; } = 30;

        [Required]
        public string PartitionKey { get; set; } = "mypartitionkey";

        [Required]
        public string RowKey { get; set; } = "myrowkey";

        /// <summary>
        /// This class is for complex validations that cannot be done with annotations.
        /// This method is called in Program.cs.
        /// </summary>
        /// <returns>Bool for if complex validation passes</returns>
        public bool ComplexValidate()
        {
            if (StagingStorageConnectionString is null && StagingStorageUri is null)
            {
                return false;
            }

            return true;
        }
    }
}
