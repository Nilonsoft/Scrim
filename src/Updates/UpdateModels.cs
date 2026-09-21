using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Scrim.Updates {
    public class UpdateResponse {
        [JsonPropertyName("projectId")]
        public string ProjectId { get; set; } = "";

        [JsonPropertyName("softwareSlug")]
        public string SoftwareSlug { get; set; } = "";

        [JsonPropertyName("softwareName")]
        public string SoftwareName { get; set; } = "";

        [JsonPropertyName("latestVersion")]
        public string LatestVersion { get; set; } = "";

        [JsonPropertyName("isUpdateAvailable")]
        public bool IsUpdateAvailable { get; set; }

        [JsonPropertyName("currentVersion")]
        public string CurrentVersion { get; set; } = "";

        [JsonPropertyName("releaseDateUtc")]
        public DateTime? ReleaseDateUtc { get; set; }

        [JsonPropertyName("downloadUrl")]
        public string DownloadUrl { get; set; } = "";

        [JsonPropertyName("fileSizeBytes")]
        public long FileSizeBytes { get; set; }

        [JsonPropertyName("osPlatform")]
        public string OsPlatform { get; set; } = "Windows";

        [JsonPropertyName("isMandatory")]
        public bool IsMandatory { get; set; }

        [JsonPropertyName("changelogSummary")]
        public List<string> ChangelogSummary { get; set; } = new();
    }

    public enum UpdateStatus {
        UpToDate,
        UpdateAvailable,
        Error
    }

    public class UpdateCheckResult {
        public UpdateStatus Status { get; set; }
        public UpdateResponse? UpdateInfo { get; set; }
        public string? ErrorMessage { get; set; }

        public static UpdateCheckResult Available(UpdateResponse info) {
            return new UpdateCheckResult {
                Status = UpdateStatus.UpdateAvailable,
                UpdateInfo = info
            };
        }

        public static UpdateCheckResult NotAvailable(UpdateResponse? info = null) {
            return new UpdateCheckResult {
                Status = UpdateStatus.UpToDate,
                UpdateInfo = info
            };
        }

        public static UpdateCheckResult Failed(string error) {
            return new UpdateCheckResult {
                Status = UpdateStatus.Error,
                ErrorMessage = error
            };
        }
    }
}
