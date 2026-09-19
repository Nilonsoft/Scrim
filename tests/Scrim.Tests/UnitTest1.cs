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
                // Test GET /api/branding
                var response = await client.GetAsync($"http://localhost:{testPort}/api/branding");
                Assert.True(response.IsSuccessStatusCode);
                string brandingJson = await response.Content.ReadAsStringAsync();
                Assert.Contains("TEST STATION", brandingJson);

                // Test OPTIONS CORS preflight
                var request = new HttpRequestMessage(HttpMethod.Options, $"http://localhost:{testPort}/api/branding");
                var optionsResponse = await client.SendAsync(request);
                Assert.True(optionsResponse.IsSuccessStatusCode);
                Assert.True(optionsResponse.Headers.Contains("Access-Control-Allow-Origin"));
            } finally {
                server.Stop();
            }
        }
    }
}

