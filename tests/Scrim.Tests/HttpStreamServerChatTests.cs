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

        [Fact]
        public async Task StreamServer_CustomMountPoint_ServesAudioAndPlaylists() {
            int testPort = 19446;
            var hub = new BroadcastHub();
            var metaMock = new Mock<IMetadataService>();
            metaMock.Setup(m => m.CurrentMetadata).Returns(new MediaMetadata());

            var profileManagerMock = new Mock<IProfileManager>();
            var profile = new ScrimProfile {
                Port = testPort,
                EnableNetworkAccess = true,
                StreamMountPoint = "radio"
            };
            profileManagerMock.Setup(p => p.CurrentProfile).Returns(profile);

            var networkMock = new Mock<INetworkDiscoveryService>();
            var requestController = new SongRequestController();
            var chatService = new LiveChatService();
            var themeService = new ThemeService();

            var server = new HttpStreamServer(hub, metaMock.Object, requestController, profileManagerMock.Object, networkMock.Object, themeService, chatService);

            var audioChannel = System.Threading.Channels.Channel.CreateUnbounded<byte[]>();
            hub.StartBroadcasting(audioChannel.Reader);
            _ = audioChannel.Writer.WriteAsync(new byte[] { 0xFF, 0xFB, 0x90, 0x00 });

            try {
                server.Start(testPort);

                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(5);

                // 1. Status API reports custom streamUrl
                var statusRes = await client.GetAsync($"http://localhost:{testPort}/api/status");
                Assert.Equal(HttpStatusCode.OK, statusRes.StatusCode);
                var statusJson = await statusRes.Content.ReadAsStringAsync();
                Assert.Contains("\"streamUrl\":\"/radio\"", statusJson);

                // 2. Custom M3U playlist references /radio
                var m3uRes = await client.GetAsync($"http://localhost:{testPort}/radio.m3u");
                Assert.Equal(HttpStatusCode.OK, m3uRes.StatusCode);
                var m3uText = await m3uRes.Content.ReadAsStringAsync();
                Assert.Contains("/radio", m3uText);
                Assert.Contains($"http://localhost:{testPort}/radio", m3uText);

                // 3. Audio stream connects on custom path /radio
                using var customStreamRes = await client.GetAsync($"http://localhost:{testPort}/radio", HttpCompletionOption.ResponseHeadersRead);
                Assert.Equal(HttpStatusCode.OK, customStreamRes.StatusCode);
                Assert.Equal("audio/mpeg", customStreamRes.Content.Headers.ContentType?.MediaType);

                // 4. Fallback /stream also still connects
                using var fallbackStreamRes = await client.GetAsync($"http://localhost:{testPort}/stream", HttpCompletionOption.ResponseHeadersRead);
                Assert.Equal(HttpStatusCode.OK, fallbackStreamRes.StatusCode);
                Assert.Equal("audio/mpeg", fallbackStreamRes.Content.Headers.ContentType?.MediaType);
            } finally {
                server.Stop();
            }
        }

        [Fact]
        public async Task StreamServer_LocalNetworkRestriction_EnforcesPrivateStream() {
            int testPort = 19447;
            var hub = new BroadcastHub();
            var metaMock = new Mock<IMetadataService>();
            metaMock.Setup(m => m.CurrentMetadata).Returns(new MediaMetadata { Title = "Secret Song" });

            var profileManagerMock = new Mock<IProfileManager>();
            var profile = new ScrimProfile {
                Port = testPort,
                EnableNetworkAccess = true,
                RestrictToLocalNetwork = true,
                StreamMountPoint = "live"
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
                client.Timeout = TimeSpan.FromSeconds(5);

                // 1. Localhost client has full access
                var localStatusRes = await client.GetAsync($"http://localhost:{testPort}/api/status");
                Assert.Equal(HttpStatusCode.OK, localStatusRes.StatusCode);
                var localStatusJson = await localStatusRes.Content.ReadAsStringAsync();
                Assert.Contains("\"isPrivate\":false", localStatusJson);
                Assert.Contains("\"restrictToLocal\":true", localStatusJson);

                var localMetaRes = await client.GetAsync($"http://localhost:{testPort}/api/metadata");
                Assert.Equal(HttpStatusCode.OK, localMetaRes.StatusCode);
                var localMetaJson = await localMetaRes.Content.ReadAsStringAsync();
                Assert.Contains("Secret Song", localMetaJson);

                // 2. Remote / non-local client (simulated via X-Forwarded-For public IP)
                using var remoteClient = new HttpClient();
                remoteClient.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.195");
                remoteClient.Timeout = TimeSpan.FromSeconds(5);

                // Remote status reports isPrivate: true
                var remoteStatusRes = await remoteClient.GetAsync($"http://localhost:{testPort}/api/status");
                Assert.Equal(HttpStatusCode.OK, remoteStatusRes.StatusCode);
                var remoteStatusJson = await remoteStatusRes.Content.ReadAsStringAsync();
                Assert.Contains("\"isPrivate\":true", remoteStatusJson);

                // Remote metadata returns 403
                var remoteMetaRes = await remoteClient.GetAsync($"http://localhost:{testPort}/api/metadata");
                Assert.Equal(HttpStatusCode.Forbidden, remoteMetaRes.StatusCode);

                // Remote audio stream returns 403 Forbidden
                var remoteAudioRes = await remoteClient.GetAsync($"http://localhost:{testPort}/live");
                Assert.Equal(HttpStatusCode.Forbidden, remoteAudioRes.StatusCode);
                var remoteAudioJson = await remoteAudioRes.Content.ReadAsStringAsync();
                Assert.Contains("Private Stream", remoteAudioJson);

                // Remote chat POST returns 403 Forbidden
                var postContent = new StringContent("{\"sender\":\"RemoteUser\",\"text\":\"Hello\"}", System.Text.Encoding.UTF8, "application/json");
                var remoteChatRes = await remoteClient.PostAsync($"http://localhost:{testPort}/api/chat", postContent);
                Assert.Equal(HttpStatusCode.Forbidden, remoteChatRes.StatusCode);
            } finally {
                server.Stop();
            }
        }

        [Fact]
        public async Task StreamServer_HttpsAndReverseProxy_FormatsUrlsAndPlaylists() {
            int testPort = 19449;
            var hub = new BroadcastHub();
            var metaMock = new Mock<IMetadataService>();
            metaMock.Setup(m => m.CurrentMetadata).Returns(new MediaMetadata());

            var profileManagerMock = new Mock<IProfileManager>();
            var profile = new ScrimProfile {
                Port = testPort,
                EnableNetworkAccess = true,
                StreamMountPoint = "radio",
                UseHttps = true,
                UseReverseProxy = true,
                CustomPublicUrl = "stream.caddyradio.com:4242"
            };
            profileManagerMock.Setup(p => p.CurrentProfile).Returns(profile);

            var networkMock = new Mock<INetworkDiscoveryService>();
            networkMock.Setup(n => n.PrimaryLocalIp).Returns("192.168.1.50");
            networkMock.Setup(n => n.PublicIp).Returns("203.0.113.88");
            var requestController = new SongRequestController();
            var chatService = new LiveChatService();
            var themeService = new ThemeService();

            var server = new HttpStreamServer(hub, metaMock.Object, requestController, profileManagerMock.Object, networkMock.Object, themeService, chatService);

            try {
                server.Start(testPort);

                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(5);

                // 1. Status endpoint reflects useHttps and useReverseProxy flags
                var statusRes = await client.GetAsync($"http://localhost:{testPort}/api/status");
                Assert.Equal(HttpStatusCode.OK, statusRes.StatusCode);
                var statusJson = await statusRes.Content.ReadAsStringAsync();
                Assert.Contains("\"useHttps\":true", statusJson);
                Assert.Contains("\"useReverseProxy\":true", statusJson);

                // 2. Network endpoint formats publicUrl as https:// without port
                var networkRes = await client.GetAsync($"http://localhost:{testPort}/api/network");
                Assert.Equal(HttpStatusCode.OK, networkRes.StatusCode);
                var networkJson = await networkRes.Content.ReadAsStringAsync();
                Assert.Contains("\"publicUrl\":\"https://stream.caddyradio.com\"", networkJson);

                // 3. M3U playlist omits port and uses https
                var m3uReq = new HttpRequestMessage(HttpMethod.Get, $"http://localhost:{testPort}/radio.m3u");
                m3uReq.Headers.Add("X-Forwarded-Host", "stream.caddyradio.com");
                var m3uRes = await client.SendAsync(m3uReq);
                Assert.Equal(HttpStatusCode.OK, m3uRes.StatusCode);
                var m3uText = await m3uRes.Content.ReadAsStringAsync();
                Assert.Contains("https://stream.caddyradio.com/radio", m3uText);
                Assert.DoesNotContain(":19449", m3uText);
                Assert.DoesNotContain(":4242", m3uText);

                // 4. PLS playlist omits port and uses https
                var plsReq = new HttpRequestMessage(HttpMethod.Get, $"http://localhost:{testPort}/radio.pls");
                plsReq.Headers.Add("X-Forwarded-Host", "stream.caddyradio.com");
                var plsRes = await client.SendAsync(plsReq);
                Assert.Equal(HttpStatusCode.OK, plsRes.StatusCode);
                var plsText = await plsRes.Content.ReadAsStringAsync();
                Assert.Contains("https://stream.caddyradio.com/radio", plsText);
            } finally {
                server.Stop();
            }
        }

        [Fact]
        public void ThemeService_BuiltInThemes_ResolveCompleteConsoleTokens() {
            var themeService = new ThemeService();
            var themes = themeService.GetAvailableThemes();

            Assert.True(themes.Count >= 11);

            // Test Synthwave 80s
            var synthwave = themeService.GetTheme("synthwave");
            Assert.NotNull(synthwave);
            Assert.Equal("#f43f5e", synthwave.AccentColor);
            Assert.Equal("#0d041a", synthwave.BgDark);

            var synthVars = synthwave.GetConsoleCssVariables();
            Assert.Equal("#0d041a", synthVars["--bg-main"]);
            Assert.Equal("#170b2e", synthVars["--card-bg"]);
            Assert.Equal("#f43f5e", synthVars["--accent-blue"]);
            Assert.Contains("--card-bg-gradient", synthVars.Keys);

            // Test custom accent override
            var customAccentVars = synthwave.GetConsoleCssVariables("#00ffaa");
            Assert.Equal("#00ffaa", customAccentVars["--accent-blue"]);
            Assert.Equal("#00ffaa", customAccentVars["--accent-color"]);

            // Test Goth theme
            var goth = themeService.GetTheme("goth");
            Assert.NotNull(goth);
            Assert.Equal("#e11d48", goth.AccentColor);
            Assert.Equal("#050608", goth.BgDark);

            var gothVars = goth.GetConsoleCssVariables();
            Assert.Equal("#050608", gothVars["--bg-main"]);
            Assert.Equal("#0b0d11", gothVars["--card-bg"]);
            Assert.Equal("#e11d48", gothVars["--accent-blue"]);
        }

        [Fact]
        public void GreenRoomChat_DirectMethods_StoreAndCapMessages() {
            var hub = new BroadcastHub();
            var metaMock = new Mock<IMetadataService>();
            metaMock.Setup(m => m.CurrentMetadata).Returns(new MediaMetadata());
            var profileManagerMock = new Mock<IProfileManager>();
            profileManagerMock.Setup(p => p.CurrentProfile).Returns(new ScrimProfile());
            var networkMock = new Mock<INetworkDiscoveryService>();
            var requestController = new SongRequestController();
            var chatService = new LiveChatService();
            var themeService = new ThemeService();

            var server = new HttpStreamServer(hub, metaMock.Object, requestController, profileManagerMock.Object, networkMock.Object, themeService, chatService);

            ChatMessage? receivedMsg = null;
            server.GreenRoomMessagePosted += msg => receivedMsg = msg;

            server.PostGreenRoomMessage("Host", "Testing 123", isHost: true, color: "#a855f7");

            Assert.NotNull(receivedMsg);
            Assert.Equal("Host", receivedMsg.Sender);
            Assert.Equal("Testing 123", receivedMsg.Text);
            Assert.True(receivedMsg.IsHost);
            Assert.Equal("#a855f7", receivedMsg.Color);

            var messages = server.GetGreenRoomMessages();
            Assert.Single(messages);
            Assert.Equal("Testing 123", messages[0].Text);

            // Verify cap at 50 messages
            for (int i = 0; i < 60; i++) {
                server.PostGreenRoomMessage("DJ Guest", $"Note {i}", isHost: false);
            }

            var cappedMessages = server.GetGreenRoomMessages();
            Assert.Equal(50, cappedMessages.Count);
            Assert.Equal("Note 59", cappedMessages[^1].Text);
        }

        [Fact]
        public async Task GreenRoomChat_HttpEndpoints_GetAndPost() {
            int testPort = 19392;
            var hub = new BroadcastHub();
            var metaMock = new Mock<IMetadataService>();
            metaMock.Setup(m => m.CurrentMetadata).Returns(new MediaMetadata());

            var profileManagerMock = new Mock<IProfileManager>();
            var profile = new ScrimProfile {
                Port = testPort,
                EnableNetworkAccess = false,
                EnableChat = true,
                DefaultVisualizerMode = "wave",
                VisualizerReactors = new System.Collections.Generic.List<VisualizerReactorConfig> {
                    new VisualizerReactorConfig { Id = "bars", Label = "Bars", Emoji = "📊", BaseMode = "bars", IsEnabled = true, IsDefault = false, IsBuiltin = true },
                    new VisualizerReactorConfig { Id = "wave", Label = "Wave", Emoji = "📈", BaseMode = "wave", IsEnabled = true, IsDefault = true, IsBuiltin = true },
                    new VisualizerReactorConfig { Id = "custom_neon", Label = "Neon Glow", Emoji = "🌟", BaseMode = "spectrum", IsEnabled = true, IsDefault = false, IsBuiltin = false }
                }
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

                // 1. Check Branding returns visualizer reactors and defaultVisualizerMode
                var brandingRes = await client.GetAsync($"http://localhost:{testPort}/api/branding");
                Assert.Equal(HttpStatusCode.OK, brandingRes.StatusCode);
                string brandingJson = await brandingRes.Content.ReadAsStringAsync();
                Assert.Contains("\"defaultVisualizerMode\":\"wave\"", brandingJson);
                Assert.Contains("\"custom_neon\"", brandingJson);
                Assert.Contains("\"Neon Glow\"", brandingJson);

                // 2. GET /greenroom serves HTML portal
                var portalRes = await client.GetAsync($"http://localhost:{testPort}/greenroom");
                Assert.Equal(HttpStatusCode.OK, portalRes.StatusCode);
                string portalHtml = await portalRes.Content.ReadAsStringAsync();
                Assert.Contains("SCRIM BACKSTAGE", portalHtml);
                Assert.Contains("Green Room Passcode", portalHtml);

                // 3. GET /api/greenroom/chat without passcode returns 401 Unauthorized
                var unauthGetRes = await client.GetAsync($"http://localhost:{testPort}/api/greenroom/chat");
                Assert.Equal(HttpStatusCode.Unauthorized, unauthGetRes.StatusCode);

                // 4. POST /api/greenroom/auth with invalid passcode returns 401
                var badAuthReq = new StringContent("{\"passcode\":\"wrongpin\"}", System.Text.Encoding.UTF8, "application/json");
                var badAuthRes = await client.PostAsync($"http://localhost:{testPort}/api/greenroom/auth", badAuthReq);
                Assert.Equal(HttpStatusCode.Unauthorized, badAuthRes.StatusCode);

                // 5. POST /api/greenroom/auth with valid passcode returns 200 OK
                var goodAuthReq = new StringContent("{\"passcode\":\"party4242\"}", System.Text.Encoding.UTF8, "application/json");
                var goodAuthRes = await client.PostAsync($"http://localhost:{testPort}/api/greenroom/auth", goodAuthReq);
                Assert.Equal(HttpStatusCode.OK, goodAuthRes.StatusCode);
                string authJson = await goodAuthRes.Content.ReadAsStringAsync();
                Assert.Contains("\"success\":true", authJson);

                // 6. GET /api/greenroom/chat with header X-GreenRoom-Passcode succeeds and initially empty
                var getReqWithHeader = new HttpRequestMessage(HttpMethod.Get, $"http://localhost:{testPort}/api/greenroom/chat");
                getReqWithHeader.Headers.Add("X-GreenRoom-Passcode", "party4242");
                var getRes = await client.SendAsync(getReqWithHeader);
                Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);
                string getJson = await getRes.Content.ReadAsStringAsync();
                Assert.Contains("\"messages\":[]", getJson);

                // 7. POST to /api/greenroom/chat with body passcode
                var postContent = new StringContent("{\"sender\":\"DJ Alex\",\"text\":\"Track 2 BPM 128 in A Minor ready\",\"color\":\"#10b981\",\"passcode\":\"party4242\"}", System.Text.Encoding.UTF8, "application/json");
                var postRes = await client.PostAsync($"http://localhost:{testPort}/api/greenroom/chat", postContent);
                Assert.Equal(HttpStatusCode.OK, postRes.StatusCode);

                // 8. GET /api/greenroom/chat with ?pin= query parameter contains posted message
                var getRes2 = await client.GetAsync($"http://localhost:{testPort}/api/greenroom/chat?pin=party4242");
                Assert.Equal(HttpStatusCode.OK, getRes2.StatusCode);
                string getJson2 = await getRes2.Content.ReadAsStringAsync();
                Assert.Contains("DJ Alex", getJson2);
                Assert.Contains("Track 2 BPM 128 in A Minor ready", getJson2);
                Assert.Contains("#10b981", getJson2);
            } finally {
                server.Stop();
            }
        }
    }
}

