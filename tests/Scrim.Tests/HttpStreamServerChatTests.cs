using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Moq;
using Scrim.Configuration;
using Scrim.Metadata;
using Scrim.Server;
using Xunit;

namespace Scrim.Tests {
    public class HttpStreamServerChatTests {
        [Fact]
        public async Task ChatApi_GetAndPost_WorkAsExpected_AndEnforceDisabledStatus() {
            int testPort = 19385;
            var hub = new BroadcastHub();
            var metaMock = new Mock<IMetadataService>();
            metaMock.Setup(m => m.CurrentMetadata).Returns(new MediaMetadata());

            var profileManagerMock = new Mock<IProfileManager>();
            var profile = new ScrimProfile {
                Port = testPort,
                EnableNetworkAccess = false,
                EnableChat = true
            };
            profileManagerMock.Setup(p => p.CurrentProfile).Returns(profile);

            var networkMock = new Mock<INetworkDiscoveryService>();
            var requestController = new SongRequestController();
            var chatService = new LiveChatService();
            var themeService = new ThemeService();

            var server = new HttpStreamServer(hub, metaMock.Object, requestController, profileManagerMock.Object, networkMock.Object, themeService, chatService);

            try {
                server.Start(testPort);

                using var client = new HttpClient();

                // 1. Initial GET /api/chat should have enabled: true and empty messages
                var getRes = await client.GetAsync($"http://localhost:{testPort}/api/chat");
                Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);
                string getJson = await getRes.Content.ReadAsStringAsync();
                Assert.Contains("\"enabled\":true", getJson);
                Assert.Contains("\"messages\":[]", getJson);

                // 2. Post a message to /api/chat
                var postContent = new StringContent("{\"sender\":\"Listener99\",\"text\":\"Great track!\"}", System.Text.Encoding.UTF8, "application/json");
                var postRes = await client.PostAsync($"http://localhost:{testPort}/api/chat", postContent);
                Assert.Equal(HttpStatusCode.OK, postRes.StatusCode);

                // Verify message is in chatService
                var msgs = chatService.GetRecentMessages();
                Assert.Single(msgs);
                Assert.Equal("Listener99", msgs[0].Sender);
                Assert.Equal("Great track!", msgs[0].Text);
                Assert.False(msgs[0].IsHost);

                // 3. GET /api/chat now contains the message
                var getRes2 = await client.GetAsync($"http://localhost:{testPort}/api/chat");
                string getJson2 = await getRes2.Content.ReadAsStringAsync();
                Assert.Contains("Listener99", getJson2);
                Assert.Contains("Great track!", getJson2);

                // 4. Host sends message via chatService
                chatService.AddMessage("DJ Dave", "Thanks for tuning in!", isHost: true);
                var getRes3 = await client.GetAsync($"http://localhost:{testPort}/api/chat");
                string getJson3 = await getRes3.Content.ReadAsStringAsync();
                Assert.Contains("DJ Dave", getJson3);
                Assert.Contains("\"isHost\":true", getJson3);

                // 5. Broadcaster disables chat -> POST /api/chat returns 403 Forbidden
                profile.EnableChat = false;
                var postDisabledContent = new StringContent("{\"sender\":\"Troll\",\"text\":\"Spam\"}", System.Text.Encoding.UTF8, "application/json");
                var postDisabledRes = await client.PostAsync($"http://localhost:{testPort}/api/chat", postDisabledContent);
                Assert.Equal(HttpStatusCode.Forbidden, postDisabledRes.StatusCode);

                // Message was NOT added
                Assert.Equal(2, chatService.GetRecentMessages().Count);
            } finally {
                server.Stop();
            }
        }

        [Fact]
        public async Task SongRequestApi_WithDedication_StoresDedication() {
            int testPort = 19386;
            var hub = new BroadcastHub();
            var metaMock = new Mock<IMetadataService>();
            metaMock.Setup(m => m.CurrentMetadata).Returns(new MediaMetadata());

            var profileManagerMock = new Mock<IProfileManager>();
            var profile = new ScrimProfile { Port = testPort, EnableNetworkAccess = false };
            profileManagerMock.Setup(p => p.CurrentProfile).Returns(profile);

            var networkMock = new Mock<INetworkDiscoveryService>();
            var requestController = new SongRequestController();
            var server = new HttpStreamServer(hub, metaMock.Object, requestController, profileManagerMock.Object, networkMock.Object);

            try {
                server.Start(testPort);

                using var client = new HttpClient();
                var postContent = new StringContent("{\"query\":\"Hotel California\",\"dedication\":\"For my best friend Leo\"}", System.Text.Encoding.UTF8, "application/json");
                var response = await client.PostAsync($"http://localhost:{testPort}/api/requests", postContent);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                var queue = requestController.GetLiveQueue().ToList();
                Assert.Single(queue);
                Assert.Equal("Hotel California", queue[0].Query);
                Assert.Equal("For my best friend Leo", queue[0].Dedication);
            } finally {
                server.Stop();
            }
        }
    }
}
