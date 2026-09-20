using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Scrim.Plugins;

namespace Scrim.Plugin.TwitchBot {
    public class TwitchBotPlugin : IScrimPlugin {
        public string Name => "Twitch Chat Bot & Song Requests";
        public string Version => "1.0.0";
        public string Author => "Scrim Studio";

        private IScrimHost? _host;
        private TwitchConfig _config = new();
        private CancellationTokenSource? _cts;
        private ClientWebSocket? _ws;

        public void Initialize(IScrimHost host) {
            _host = host;

            string? pluginDir = null;
            try {
                pluginDir = Path.GetDirectoryName(typeof(TwitchBotPlugin).Assembly.Location);
            } catch { }

            _config = TwitchConfig.Load(pluginDir);

            if (string.IsNullOrWhiteSpace(_config.Channel) || string.IsNullOrWhiteSpace(_config.OauthToken)) {
                Console.WriteLine($"[{Name}] Initialized. Twitch credentials not configured. Configure Plugins/twitch_config.json or set SCRIM_TWITCH_CHANNEL & SCRIM_TWITCH_OAUTH.");
                return;
            }

            _cts = new CancellationTokenSource();
            Task.Run(() => RunBotAsync(_cts.Token));
            Console.WriteLine($"[{Name}] Initialized. Connecting to Twitch channel #{_config.Channel}...");
        }

        private async Task RunBotAsync(CancellationToken token) {
            while (!token.IsCancellationRequested) {
                try {
                    using (_ws = new ClientWebSocket()) {
                        var uri = new Uri("wss://irc-ws.chat.twitch.tv:443");
                        await _ws.ConnectAsync(uri, token);

                        string pass = _config.OauthToken.StartsWith("oauth:", StringComparison.OrdinalIgnoreCase) 
                            ? _config.OauthToken 
                            : $"oauth:{_config.OauthToken}";
                        string nick = string.IsNullOrWhiteSpace(_config.BotUsername) ? _config.Channel : _config.BotUsername;
                        string channel = _config.Channel.TrimStart('#').ToLowerInvariant();

                        await SendRawAsync(_ws, $"PASS {pass}", token);
                        await SendRawAsync(_ws, $"NICK {nick}", token);
                        await SendRawAsync(_ws, $"JOIN #{channel}", token);

                        Console.WriteLine($"[{Name}] Connected to Twitch chat as {nick} on #{channel}!");

                        var buffer = new byte[4096];
                        while (_ws.State == WebSocketState.Open && !token.IsCancellationRequested) {
                            var result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                            if (result.MessageType == WebSocketMessageType.Close) {
                                break;
                            }

                            string raw = System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count);
                            using var reader = new StringReader(raw);
                            string? line;
                            while ((line = reader.ReadLine()) != null) {
                                await ProcessIrcLineAsync(_ws, line, channel, token);
                            }
                        }
                    }
                } catch (OperationCanceledException) {
                    break;
                } catch (Exception ex) {
                    Console.WriteLine($"[{Name}] Twitch connection lost: {ex.Message}. Reconnecting in 10s...");
                    try {
                        await Task.Delay(10000, token);
                    } catch { }
                }
            }
        }

        private async Task ProcessIrcLineAsync(ClientWebSocket ws, string line, string channel, CancellationToken token) {
            if (line.StartsWith("PING")) {
                await SendRawAsync(ws, "PONG :tmi.twitch.tv", token);
                return;
            }

            // Parse PRIVMSG :user!user@user.tmi.twitch.tv PRIVMSG #channel :message
            if (line.Contains("PRIVMSG")) {
                int userEnd = line.IndexOf('!');
                string sender = (userEnd > 1 && line.StartsWith(':')) ? line.Substring(1, userEnd - 1) : "viewer";

                int msgStart = line.IndexOf(" :", StringComparison.Ordinal);
                if (msgStart != -1) {
                    string message = line.Substring(msgStart + 2).Trim();
                    await HandleChatMessageAsync(ws, sender, message, channel, token);
                }
            }
        }

        private async Task HandleChatMessageAsync(ClientWebSocket ws, string sender, string message, string channel, CancellationToken token) {
            string prefix = _config.CommandPrefix;

            if (message.Equals($"{prefix}song", StringComparison.OrdinalIgnoreCase) || 
                message.Equals($"{prefix}nowplaying", StringComparison.OrdinalIgnoreCase)) {
                var meta = _host?.Metadata.CurrentMetadata;
                string title = meta?.Title ?? "Awaiting Track";
                string artist = meta?.Artist ?? "";
                string station = _host?.ProfileManager.CurrentProfile.StationName ?? "Scrim Studio";
                await SendChatMessageAsync(ws, channel, $"🎶 Now Playing on {station}: {title} - {artist}", token);
            } else if ((message.StartsWith($"{prefix}request ", StringComparison.OrdinalIgnoreCase) || 
                        message.StartsWith($"{prefix}sr ", StringComparison.OrdinalIgnoreCase)) && _config.EnableSongRequests) {
                int splitIdx = message.IndexOf(' ');
                string query = message.Substring(splitIdx + 1).Trim();
                if (!string.IsNullOrWhiteSpace(query)) {
                    _host?.SongRequests.SubmitRequest(query, $"Twitch request by @{sender}");
                    await SendChatMessageAsync(ws, channel, $"✅ @{sender} Added \"{query}\" to the station request queue!", token);
                }
            }
        }

        private async Task SendChatMessageAsync(ClientWebSocket ws, string channel, string message, CancellationToken token) {
            await SendRawAsync(ws, $"PRIVMSG #{channel} :{message}", token);
        }

        private static async Task SendRawAsync(ClientWebSocket ws, string text, CancellationToken token) {
            if (ws.State == WebSocketState.Open) {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(text + "\r\n");
                await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, token);
            }
        }

        public void Shutdown() {
            _cts?.Cancel();
            try {
                _ws?.Dispose();
            } catch { }
            Console.WriteLine($"[{Name}] Shutdown complete.");
        }
    }
}
