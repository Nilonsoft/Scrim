using System;
using System.Text.Json;
using System.Threading.Tasks;
using Scrim.Updates;
using Xunit;

namespace Scrim.Tests {
    public class UpdateServiceTests {
        [Fact]
        public void UpdateResponse_DeserializesProperlyFromJson() {
            string json = @"{
                ""projectId"": ""d2e8813f-2a03-4b5d-b3e3-3a2d066e48b0"",
                ""softwareSlug"": ""scrim"",
                ""softwareName"": ""Scrim"",
                ""latestVersion"": ""1.0.4"",
                ""isUpdateAvailable"": true,
                ""currentVersion"": ""1.0.0"",
                ""releaseDateUtc"": ""2026-09-19T17:23:44.4652104Z"",
                ""downloadUrl"": ""/downloads/ScrimSetup-v1.0.4.msi"",
                ""fileSizeBytes"": 65732209,
                ""osPlatform"": ""Windows"",
                ""isMandatory"": false,
                ""changelogSummary"": [
                    ""Feature A"",
                    ""Improvement B""
                ]
            }";

            var response = JsonSerializer.Deserialize<UpdateResponse>(json);

            Assert.NotNull(response);
            Assert.Equal("scrim", response.SoftwareSlug);
            Assert.Equal("1.0.4", response.LatestVersion);
            Assert.True(response.IsUpdateAvailable);
            Assert.Equal(2, response.ChangelogSummary.Count);
            Assert.Equal(65732209, response.FileSizeBytes);
        }

        [Fact]
        public void UpdateCheckResult_FactoryMethods_ReturnExpectedStatuses() {
            var info = new UpdateResponse {
                SoftwareName = "Scrim",
                LatestVersion = "1.0.4",
                IsUpdateAvailable = true
            };

            var avail = UpdateCheckResult.Available(info);
            Assert.Equal(UpdateStatus.UpdateAvailable, avail.Status);
            Assert.Equal(info, avail.UpdateInfo);

            var notAvail = UpdateCheckResult.NotAvailable(info);
            Assert.Equal(UpdateStatus.UpToDate, notAvail.Status);

            var failed = UpdateCheckResult.Failed("Connection timeout");
            Assert.Equal(UpdateStatus.Error, failed.Status);
            Assert.Equal("Connection timeout", failed.ErrorMessage);
        }

        [Fact]
        public async Task UpdateService_CheckForUpdates_AgainstLiveEndpoint_CompletesSuccessfully() {
            var service = new UpdateService();
            // Test with 1.0.0 (an older version) to verify live API returns an update
            var result = await service.CheckForUpdatesAsync("1.0.0");

            Assert.NotEqual(UpdateStatus.Error, result.Status);
            if (result.Status == UpdateStatus.UpdateAvailable) {
                Assert.NotNull(result.UpdateInfo);
                Assert.StartsWith("https://www.nilonsoft.com", result.UpdateInfo.DownloadUrl);
            }
        }
    }
}
