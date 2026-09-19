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

        [Fact]
        public async Task ChatApi_EnforcesBlacklist_AndRejectsBlacklistedNicknames() {
            int testPort = 19387;
            var hub = new BroadcastHub();
            var metaMock = new Mock<IMetadataService>();
            metaMock.Setup(m => m.CurrentMetadata).Returns(new MediaMetadata());

            var profileManagerMock = new Mock<IProfileManager>();
            var profile = new ScrimProfile {
                Port = testPort,
                EnableNetworkAccess = false,
                EnableChat = true,
                NicknameBlacklist = new System.Collections.Generic.List<string> { "badword", "spammer" }
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

                // 1. GET /api/chat includes blacklist
                var getRes = await client.GetAsync($"http://localhost:{testPort}/api/chat");
                Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);
                string getJson = await getRes.Content.ReadAsStringAsync();
                Assert.Contains("\"badword\"", getJson);
                Assert.Contains("\"spammer\"", getJson);

                // 2. Allowed sender succeeds
                var goodContent = new StringContent("{\"sender\":\"CoolListener\",\"text\":\"Hello!\"}", System.Text.Encoding.UTF8, "application/json");
                var goodRes = await client.PostAsync($"http://localhost:{testPort}/api/chat", goodContent);
                Assert.Equal(HttpStatusCode.OK, goodRes.StatusCode);

                // 3. Blacklisted sender fails with 400 Bad Request
                var badContent = new StringContent("{\"sender\":\"Spammer123\",\"text\":\"Spam message\"}", System.Text.Encoding.UTF8, "application/json");
                var badRes = await client.PostAsync($"http://localhost:{testPort}/api/chat", badContent);
                Assert.Equal(HttpStatusCode.BadRequest, badRes.StatusCode);

                // Only 1 message was added
                Assert.Single(chatService.GetRecentMessages());

                // 4. Nickname assignment triggers event
                string assignedTarget = "";
                string assignedNew = "";
                chatService.NicknameAssigned += (t, a) => {
                    assignedTarget = t;
                    assignedNew = a;
                };
                chatService.AssignNickname("CoolListener", "SuperFan");
                Assert.Equal("CoolListener", assignedTarget);
                Assert.Equal("SuperFan", assignedNew);

                // 5. Clear chat clears messages and fires event
                bool clearedFired = false;
                chatService.ChatCleared += () => clearedFired = true;
                chatService.Clear();
                Assert.True(clearedFired);
                Assert.Empty(chatService.GetRecentMessages());
            } finally {
                server.Stop();
            }
        }

        [Fact]
        public async Task WebPlayer_ConnectsCleanly_WhenNetworkAccessEnabled() {
            int testPort = 19445;
            var hub = new BroadcastHub();
            var metaMock = new Mock<IMetadataService>();
            metaMock.Setup(m => m.CurrentMetadata).Returns(new MediaMetadata());

            var profileManagerMock = new Mock<IProfileManager>();
            var profile = new ScrimProfile {
                Port = testPort,
                EnableNetworkAccess = true,
                EnableChat = true
            };
            profileManagerMock.Setup(p => p.CurrentProfile).Returns(profile);

            var networkMock = new Mock<INetworkDiscoveryService>();
            networkMock.Setup(n => n.GetAllLocalIps()).Returns(new List<string> { "192.168.1.100" });
            var requestController = new SongRequestController();
            var chatService = new LiveChatService();
            var themeService = new ThemeService();
            var reactionService = new SongReactionService();
            var historyService = new SongHistoryService();

            var server = new HttpStreamServer(hub, metaMock.Object, requestController, profileManagerMock.Object, networkMock.Object, themeService, chatService, reactionService, historyService);

            try {
                server.Start(testPort);

                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(5);

                // 1. GET / (index.html)
                var indexRes = await client.GetAsync($"http://localhost:{testPort}/");
                Assert.Equal(HttpStatusCode.OK, indexRes.StatusCode);

                // 2. GET /api/status
                var statusRes = await client.GetAsync($"http://localhost:{testPort}/api/status");
                Assert.Equal(HttpStatusCode.OK, statusRes.StatusCode);

                // 3. GET /api/metadata
                var metaRes = await client.GetAsync($"http://localhost:{testPort}/api/metadata");
                Assert.Equal(HttpStatusCode.OK, metaRes.StatusCode);

                // 4. SSE /api/events
                using var sseRes = await client.GetAsync($"http://localhost:{testPort}/api/events", HttpCompletionOption.ResponseHeadersRead);
                Assert.Equal(HttpStatusCode.OK, sseRes.StatusCode);
                using var stream = await sseRes.Content.ReadAsStreamAsync();
                using var reader = new System.IO.StreamReader(stream);

                var lineTask = reader.ReadLineAsync();
                var completed = await Task.WhenAny(lineTask, Task.Delay(3000));
                Assert.Same(lineTask, completed);
                var line = await lineTask;
                Assert.NotNull(line);
                Assert.StartsWith("data: ", line);
            } finally {
                server.Stop();
            }
        }
    }
}
