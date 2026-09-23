using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Moq;
using Scrim.Audio;
using Scrim.Configuration;
using Scrim.Metadata;
using Scrim.Server;
using Xunit;

namespace Scrim.Tests {
    public class MultiStationTests {
        [Fact]
        public void Profile_EnsureDefaultStation_CreatesDefaultIfEmpty() {
            var profile = new ScrimProfile {
                StationName = "Default FM",
                StreamMountPoint = "stream"
            };
            profile.Stations.Clear();

            profile.EnsureDefaultStation();

            Assert.Single(profile.Stations);
            var st = profile.Stations[0];
            Assert.Equal("Default FM", st.StationName);
            Assert.Equal("stream", st.MountPoint);
            Assert.Equal(profile.ActiveStationId, st.Id);
        }

        [Fact]
        public void MultiStationManager_AddAndRetrieveStations_WorksCorrectly() {
            var profile = new ScrimProfile();
            profile.EnsureDefaultStation();

            var profileMock = new Mock<IProfileManager>();
            profileMock.Setup(p => p.CurrentProfile).Returns(profile);

            using var manager = new MultiStationManager(profileMock.Object);

            var initialStations = manager.GetAllStations();
            Assert.NotEmpty(initialStations);

            var newStation = new StationConfig {
                Id = "spotify-station",
                Name = "Spotify Stream",
                StationName = "Spotify Beats",
                MountPoint = "spotify",
                SourceType = AudioSourceType.ProcessLoopback,
                TargetProcessName = "Spotify.exe"
            };

            var pipeline = manager.AddStation(newStation);
            Assert.NotNull(pipeline);
            Assert.Equal("spotify", pipeline.Config.MountPoint);

            var byMount = manager.GetStationByMount("spotify");
            Assert.NotNull(byMount);
            Assert.Equal("spotify-station", byMount.Config.Id);

            manager.SetActiveStation("spotify-station");
            var active = manager.GetActiveStation();
            Assert.Equal("spotify-station", active.Config.Id);
        }

        [Fact]
        public async Task HttpStreamServer_MultiStationRouting_ReturnsStationsAndScopedBranding() {
            int testPort = 44299;
            var hub = new BroadcastHub();
            var metaMock = new Mock<IMetadataService>();
            metaMock.Setup(m => m.CurrentMetadata).Returns(new MediaMetadata());

            var profile = new ScrimProfile {
                Port = testPort,
                EnableNetworkAccess = false,
                StationName = "Main Console Station"
            };
            profile.EnsureDefaultStation();

            // Add 2 extra stations (e.g. Spotify and Chrome)
            profile.Stations.Add(new StationConfig {
                Id = "spotify-channel",
                Name = "Spotify Lo-Fi",
                StationName = "Lo-Fi 24/7",
                MountPoint = "spotify",
                SourceType = AudioSourceType.ProcessLoopback,
                TargetProcessName = "Spotify.exe",
                WebTheme = "cyberpunk",
                AccentColor = "#10b981"
            });
            profile.Stations.Add(new StationConfig {
                Id = "vault-channel",
                Name = "The Vault 80s",
                StationName = "80s Rewind",
                MountPoint = "vault",
                SourceType = AudioSourceType.BuiltInPlayer,
                WebTheme = "retro",
                AccentColor = "#f59e0b"
            });

            var profileMock = new Mock<IProfileManager>();
            profileMock.Setup(p => p.CurrentProfile).Returns(profile);

            var networkMock = new Mock<INetworkDiscoveryService>();
            var requestController = new SongRequestController();
            var chatService = new LiveChatService();
            var themeService = new ThemeService();
            var stationManager = new MultiStationManager(profileMock.Object);

            var server = new HttpStreamServer(
                hub,
                metaMock.Object,
                requestController,
                profileMock.Object,
                networkMock.Object,
                themeService,
                chatService,
                stationManager: stationManager
            );

            try {
                server.Start(testPort);
                using var client = new HttpClient();

                // 1. GET /api/stations returns all 3 stations
                var stationsRes = await client.GetAsync($"http://localhost:{testPort}/api/stations");
                Assert.Equal(HttpStatusCode.OK, stationsRes.StatusCode);
                string stationsJson = await stationsRes.Content.ReadAsStringAsync();
                Assert.Contains("spotify", stationsJson);
                Assert.Contains("vault", stationsJson);
                Assert.Contains("Spotify Lo-Fi", stationsJson);
                Assert.Contains("The Vault 80s", stationsJson);

                // 2. GET /api/branding?station=spotify returns Spotify-scoped branding
                var spotifyBrandingRes = await client.GetAsync($"http://localhost:{testPort}/api/branding?station=spotify");
                Assert.Equal(HttpStatusCode.OK, spotifyBrandingRes.StatusCode);
                string spotifyBrandingJson = await spotifyBrandingRes.Content.ReadAsStringAsync();
                Assert.Contains("Lo-Fi 24/7", spotifyBrandingJson);
                Assert.Contains("cyberpunk", spotifyBrandingJson);
                Assert.Contains("/spotify", spotifyBrandingJson);

                // 3. GET /api/vault/branding (path-based routing) returns Vault branding
                var vaultBrandingRes = await client.GetAsync($"http://localhost:{testPort}/api/vault/branding");
                Assert.Equal(HttpStatusCode.OK, vaultBrandingRes.StatusCode);
                string vaultBrandingJson = await vaultBrandingRes.Content.ReadAsStringAsync();
                Assert.Contains("80s Rewind", vaultBrandingJson);
                Assert.Contains("retro", vaultBrandingJson);
                Assert.Contains("/vault", vaultBrandingJson);

                // 4. GET /manifest.webmanifest returns default station manifest
                var defaultManifestRes = await client.GetAsync($"http://localhost:{testPort}/manifest.webmanifest");
                Assert.Equal(HttpStatusCode.OK, defaultManifestRes.StatusCode);
                Assert.Contains("application/manifest+json", defaultManifestRes.Content.Headers.ContentType?.ToString() ?? "");
                string defaultManifestJson = await defaultManifestRes.Content.ReadAsStringAsync();
                Assert.Contains("Main Console Station", defaultManifestJson);
                Assert.Contains("/pwa/stream", defaultManifestJson);

                // 5. GET /manifest.webmanifest?station=spotify returns Spotify-specific manifest
                var spotifyManifestRes = await client.GetAsync($"http://localhost:{testPort}/manifest.webmanifest?station=spotify");
                Assert.Equal(HttpStatusCode.OK, spotifyManifestRes.StatusCode);
                string spotifyManifestJson = await spotifyManifestRes.Content.ReadAsStringAsync();
                Assert.Contains("Lo-Fi 24/7", spotifyManifestJson);
                Assert.Contains("/pwa/spotify", spotifyManifestJson);
                Assert.Contains("/?station=spotify", spotifyManifestJson);
                Assert.Contains("#10b981", spotifyManifestJson);

                // 6. GET /manifest.webmanifest?station=vault returns Vault-specific manifest
                var vaultManifestRes = await client.GetAsync($"http://localhost:{testPort}/manifest.webmanifest?station=vault");
                Assert.Equal(HttpStatusCode.OK, vaultManifestRes.StatusCode);
                string vaultManifestJson = await vaultManifestRes.Content.ReadAsStringAsync();
                Assert.Contains("80s Rewind", vaultManifestJson);
                Assert.Contains("/pwa/vault", vaultManifestJson);
                Assert.Contains("/?station=vault", vaultManifestJson);
                Assert.Contains("#f59e0b", vaultManifestJson);
            } finally {
                server.Stop();
                stationManager.Dispose();
            }
        }
    }
}
