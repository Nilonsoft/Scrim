using System;
using System.Net.Http;
using System.Threading.Tasks;
using Moq;
using Scrim.Configuration;
using Scrim.Metadata;
using Scrim.Server;
using Xunit;

namespace Scrim.Tests {
    public class HttpStreamServerTests {
        [Fact]
        public async Task HttpStreamServer_StartsCleanlyOnLoopback_AndServesEndpoints() {
            int testPort = 19283;
            var hub = new BroadcastHub();
            var metaMock = new Mock<IMetadataService>();
            metaMock.Setup(m => m.CurrentMetadata).Returns(new MediaMetadata { Title = "Test Song", Artist = "Test Artist" });

            var profileManagerMock = new Mock<IProfileManager>();
            var profile = new ScrimProfile {
                Port = testPort,
                EnableNetworkAccess = true, // Ensures fallback to loopback is exercised when non-elevated
                StationName = "TEST STATION"
            };
            profileManagerMock.Setup(p => p.CurrentProfile).Returns(profile);

            var networkMock = new Mock<INetworkDiscoveryService>();
            networkMock.Setup(n => n.GetAllLocalIps()).Returns(new System.Collections.Generic.List<string> { "192.168.1.99" });
            networkMock.Setup(n => n.GetLocalShareUrl(It.IsAny<int>())).Returns($"http://192.168.1.99:{testPort}");
            networkMock.Setup(n => n.GetPublicShareUrl(It.IsAny<int>())).Returns($"http://1.2.3.4:{testPort}");

            var requestController = new SongRequestController();
            var server = new HttpStreamServer(hub, metaMock.Object, requestController, profileManagerMock.Object, networkMock.Object);

            try {
                server.Start(testPort);

                using var client = new HttpClient();
                // Test GET /api/branding via localhost
                var response = await client.GetAsync($"http://localhost:{testPort}/api/branding");
                Assert.True(response.IsSuccessStatusCode);
                string brandingJson = await response.Content.ReadAsStringAsync();
                Assert.Contains("TEST STATION", brandingJson);

                // Test GET /api/branding via 127.0.0.1 (exercises bridge when non-elevated)
                var ipResponse = await client.GetAsync($"http://127.0.0.1:{testPort}/api/branding");
                Assert.True(ipResponse.IsSuccessStatusCode);
                string ipBranding = await ipResponse.Content.ReadAsStringAsync();
                Assert.Contains("TEST STATION", ipBranding);

                // Test GET /api/branding via active LAN IP (reproduces the user scenario: e.g. 192.168.8.205)
                foreach (var ip in System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName()).AddressList) {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !System.Net.IPAddress.IsLoopback(ip)) {
                        var lanResponse = await client.GetAsync($"http://{ip}:{testPort}/api/branding");
                        Assert.True(lanResponse.IsSuccessStatusCode);
                        string lanBranding = await lanResponse.Content.ReadAsStringAsync();
                        Assert.Contains("TEST STATION", lanBranding);
                        break;
                    }
                }

                // Test OPTIONS CORS preflight
                var request = new HttpRequestMessage(HttpMethod.Options, $"http://localhost:{testPort}/api/branding");
                var optionsResponse = await client.SendAsync(request);
                Assert.True(optionsResponse.IsSuccessStatusCode);
                Assert.True(optionsResponse.Headers.Contains("Access-Control-Allow-Origin"));

                // Test PWA manifest endpoint
                var manifestResponse = await client.GetAsync($"http://localhost:{testPort}/manifest.webmanifest");
                Assert.True(manifestResponse.IsSuccessStatusCode);
                Assert.Contains("application/manifest+json", manifestResponse.Content.Headers.ContentType?.ToString() ?? "");

                // Test PWA service worker endpoint
                var swResponse = await client.GetAsync($"http://localhost:{testPort}/sw.js");
                Assert.True(swResponse.IsSuccessStatusCode);
                Assert.True(swResponse.Headers.Contains("Service-Worker-Allowed"));
            } finally {
                server.Stop();
            }
        }

        [Fact]
        public void ThemeService_InitializesAllBuiltInThemes() {
            var themeService = new ThemeService();
            var themes = themeService.GetAvailableThemes();

            Assert.True(themes.Count >= 11);
            string[] expectedThemeIds = new[] {
                "dark", "goth", "pink", "flowers", "light", "rock",
                "synthwave", "cyberpunk", "space", "lofi", "ocean"
            };

            foreach (var id in expectedThemeIds) {
                var theme = themeService.GetTheme(id);
                Assert.NotNull(theme);
                Assert.False(string.IsNullOrWhiteSpace(theme.Name));
                Assert.False(string.IsNullOrWhiteSpace(theme.Icon));
                Assert.False(string.IsNullOrWhiteSpace(theme.AccentColor));
            }
        }

        [Fact]
        public void HttpStreamServer_BroadcastBrandingUpdate_FiresBrandingSettingsChangedEvent() {
            var hub = new BroadcastHub();
            var metaMock = new Mock<IMetadataService>();
            metaMock.Setup(m => m.CurrentMetadata).Returns(new MediaMetadata { Title = "Test Song", Artist = "Test Artist" });
            var profileManagerMock = new Mock<IProfileManager>();
            profileManagerMock.Setup(p => p.CurrentProfile).Returns(new ScrimProfile { StationName = "ORIGINAL" });
            var networkMock = new Mock<INetworkDiscoveryService>();
            var requestController = new SongRequestController();
            var server = new HttpStreamServer(hub, metaMock.Object, requestController, profileManagerMock.Object, networkMock.Object);

            bool eventFired = false;
            server.BrandingSettingsChanged += () => eventFired = true;

            server.BroadcastBrandingUpdate();

            Assert.True(eventFired);
        }
    }
}

