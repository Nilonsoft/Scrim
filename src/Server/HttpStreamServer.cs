using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Scrim.Configuration;
using Scrim.Metadata;

namespace Scrim.Server {
    public class HttpStreamServer : IStreamServer {
        private readonly BroadcastHub _hub;
        private readonly IMetadataService _metadataService;
        private readonly SongRequestController _requestController;
        private readonly IProfileManager _profileManager;
        private readonly INetworkDiscoveryService _networkDiscovery;
        private readonly IThemeService _themeService;
        private readonly ILiveChatService _chatService;
        private readonly ISongReactionService _reactionService;
        private readonly ISongHistoryService _historyService;

        public event Action? HistorySettingsChanged;

        public void BroadcastHistoryUpdate() {
            HistorySettingsChanged?.Invoke();
        }
        private HttpListener? _listener;
        private TcpListener? _bridgeListener;
        private CancellationTokenSource? _cts;

        public HttpStreamServer(BroadcastHub hub, IMetadataService metadataService, SongRequestController requestController, IProfileManager profileManager, INetworkDiscoveryService networkDiscovery, IThemeService? themeService = null, ILiveChatService? chatService = null, ISongReactionService? reactionService = null, ISongHistoryService? historyService = null) {
            _hub = hub;
            _metadataService = metadataService;
            _requestController = requestController;
            _profileManager = profileManager;
            _networkDiscovery = networkDiscovery;
            _themeService = themeService ?? new ThemeService();
            _chatService = chatService ?? new LiveChatService();
            _chatService.SyncBannedUsers(_profileManager.CurrentProfile.BannedUsers);
            _reactionService = reactionService ?? new SongReactionService(metadataService);
            _historyService = historyService ?? new SongHistoryService(metadataService);
        }

        private string GetCustomThemeJson(string themeId) {
            var themeDef = _themeService.GetTheme(themeId);
            if (themeDef != null && themeDef.IsCustom) {
                var resolved = themeDef.GetResolvedCssVariables();
                return "{" + string.Join(",", resolved.Select(kvp => $"\"{EscapeJson(kvp.Key)}\":\"{EscapeJson(kvp.Value)}\"")) + "}";
            }
            return "{}";
        }

        private static int FindAvailablePort(int startingPort) {
            try {
                var activeTcpPorts = IPGlobalProperties.GetIPGlobalProperties()
                    .GetActiveTcpListeners()
                    .Select(endpoint => endpoint.Port)
                    .ToHashSet();

                int port = startingPort;
                while (activeTcpPorts.Contains(port) && port < 65535) {
                    port++;
                }
                return port;
            } catch {
                return startingPort;
            }
        }

        public void Start(int port) {
            if ((_listener != null && _listener.IsListening) || _bridgeListener != null) return;

            int targetPort = FindAvailablePort(port);
            var profile = _profileManager.CurrentProfile;

            for (int attempt = 0; attempt < 10; attempt++) {
                var listener = new HttpListener();
                listener.Prefixes.Add($"http://localhost:{targetPort}/");
                listener.Prefixes.Add($"http://127.0.0.1:{targetPort}/");

                bool addedNetworkPrefixes = false;
                if (profile.EnableNetworkAccess) {
                    try {
                        listener.Prefixes.Add($"http://+:{targetPort}/");
                        addedNetworkPrefixes = true;
                    } catch { }

                    foreach (var ip in (_networkDiscovery.GetAllLocalIps() ?? Enumerable.Empty<string>())) {
                        try {
                            listener.Prefixes.Add($"http://{ip}:{targetPort}/");
                            addedNetworkPrefixes = true;
                        } catch { }
                    }
                }

                try {
                    listener.Start();
                    _listener = listener;
                    if (targetPort != port) {
                        profile.Port = targetPort;
                        _profileManager.SaveProfile(profile);
                    }
                    if (profile.EnableNetworkAccess && profile.EnableUpnpPortForwarding) {
                        Task.Run(() => _networkDiscovery.TryMapUpnpPort(targetPort));
                    }
                    break;
                } catch (Exception) {
                    try { listener.Close(); } catch { }

                    // If network / wildcard prefixes failed (e.g. Access is Denied without admin rights):
                    // Start an internal loopback HttpListener on an available high port,
                    // and bind a non-elevated socket TcpListener on targetPort (0.0.0.0:targetPort).
                    // This allows LAN, WAN, and custom hostnames (e.g. 192.168.x.x) to connect cleanly
                    // without HTTP.sys rejecting them with "HTTP Error 400. The request hostname is invalid."
                    if (addedNetworkPrefixes) {
                        int internalLoopbackPort = FindAvailablePort(targetPort + 1000);
                        var loopbackListener = new HttpListener();
                        loopbackListener.Prefixes.Add($"http://127.0.0.1:{internalLoopbackPort}/");
                        loopbackListener.Prefixes.Add($"http://localhost:{internalLoopbackPort}/");

                        TcpListener? bridge = null;
                        try {
                            loopbackListener.Start();
                            try {
                                bridge = new TcpListener(IPAddress.IPv6Any, targetPort);
                                bridge.Server.DualMode = true;
                                bridge.Start();
                            } catch {
                                bridge?.Stop();
                                bridge = new TcpListener(IPAddress.Any, targetPort);
                                bridge.Start();
                            }

                            _listener = loopbackListener;
                            _bridgeListener = bridge;

                            if (targetPort != port) {
                                profile.Port = targetPort;
                                _profileManager.SaveProfile(profile);
                            }
                            if (profile.EnableNetworkAccess && profile.EnableUpnpPortForwarding) {
                                Task.Run(() => _networkDiscovery.TryMapUpnpPort(targetPort));
                            }
                            break;
                        } catch {
                            try { loopbackListener.Close(); } catch { }
                            try { bridge?.Stop(); } catch { }
                            _bridgeListener = null;
                        }
                    }

                    // Advance port if binding failed
                    targetPort++;
                }
            }

            if (_listener == null || !_listener.IsListening) return;

            _cts = new CancellationTokenSource();
            Task.Run(() => AcceptLoop(_cts.Token));

            if (_bridgeListener != null) {
                int internalPort = _listener.Prefixes
                    .Select(p => new Uri(p).Port)
                    .FirstOrDefault();
                if (internalPort > 0) {
                    Task.Run(() => BridgeAcceptLoop(_cts.Token, internalPort));
                }
            }
        }

        private async Task BridgeAcceptLoop(CancellationToken token, int internalPort) {
            while (!token.IsCancellationRequested && _bridgeListener != null) {
                TcpClient client;
                try {
                    client = await _bridgeListener.AcceptTcpClientAsync(token);
                } catch {
                    break;
                }

                string mount = GetNormalizedMountPoint();
                _ = Task.Run(() => ForwardClientAsync(client, internalPort, mount, token));
            }
        }

        private static async Task ForwardClientAsync(TcpClient client, int internalPort, string mountPoint, CancellationToken token) {
            try {
                using (client)
                using (var server = new TcpClient()) {
                    client.NoDelay = true;
                    server.NoDelay = true;
                    await server.ConnectAsync(IPAddress.Loopback, internalPort, token);
                    using var clientStream = client.GetStream();
                    using var serverStream = server.GetStream();

                    var buffer = new byte[32768];
                    int totalRead = 0;
                    int headerEnd = -1;

                    while (totalRead < buffer.Length) {
                        int read = await clientStream.ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead), token);
                        if (read <= 0) break;
                        totalRead += read;

                        for (int i = 0; i <= totalRead - 4; i++) {
                            if (buffer[i] == '\r' && buffer[i + 1] == '\n' &&
                                buffer[i + 2] == '\r' && buffer[i + 3] == '\n') {
                                headerEnd = i + 4;
                                break;
                            }
                        }
                        if (headerEnd != -1) break;

                        for (int i = 0; i <= totalRead - 2; i++) {
                            if (buffer[i] == '\n' && buffer[i + 1] == '\n') {
                                headerEnd = i + 2;
                                break;
                            }
                        }
                        if (headerEnd != -1) break;
                    }

                    if (headerEnd == -1) return;

                    string headerText = System.Text.Encoding.ASCII.GetString(buffer, 0, headerEnd);
                    string originalHost = "";
                    bool hasConnectionHeader = false;
                    var rawLines = headerText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                    var validLines = rawLines.Where(l => !string.IsNullOrEmpty(l)).ToList();
                    bool isStreamOrSse = validLines.Count > 0 && (validLines[0].Contains("/api/events") || validLines[0].Contains("/stream") || validLines[0].Contains("/live") || (!string.IsNullOrEmpty(mountPoint) && validLines[0].Contains($"/{mountPoint}")));

                    for (int i = 0; i < validLines.Count; i++) {
                        if (validLines[i].StartsWith("Host:", StringComparison.OrdinalIgnoreCase)) {
                            originalHost = validLines[i].Substring(5).Trim();
                            validLines[i] = $"Host: 127.0.0.1:{internalPort}";
                        } else if (validLines[i].StartsWith("Connection:", StringComparison.OrdinalIgnoreCase) && !isStreamOrSse) {
                            validLines[i] = "Connection: close";
                            hasConnectionHeader = true;
                        }
                    }

                    if (!hasConnectionHeader && !isStreamOrSse) {
                        validLines.Add("Connection: close");
                    }
                    if (!string.IsNullOrEmpty(originalHost)) {
                        validLines.Add($"X-Forwarded-Host: {originalHost}");
                        validLines.Add("X-Forwarded-Proto: http");
                    }

                    string modHeaders = string.Join("\r\n", validLines) + "\r\n\r\n";
                    byte[] modHeaderBytes = System.Text.Encoding.ASCII.GetBytes(modHeaders);
                    await serverStream.WriteAsync(modHeaderBytes, token);

                    int remaining = totalRead - headerEnd;
                    if (remaining > 0) {
                        await serverStream.WriteAsync(buffer.AsMemory(headerEnd, remaining), token);
                    }

                    var t1 = clientStream.CopyToAsync(serverStream, token);
                    var t2 = serverStream.CopyToAsync(clientStream, token);
                    await Task.WhenAny(t1, t2);
                }
            } catch { }
        }

        private async Task AcceptLoop(CancellationToken token) {
            while (!token.IsCancellationRequested) {
                HttpListenerContext context;
                try {
                    context = await _listener!.GetContextAsync();
                } catch {
                    break;
                }

                // Global CORS preflight OPTIONS handling
                if (context.Request.HttpMethod == "OPTIONS") {
                    try {
                        context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                        context.Response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                        context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Accept, Authorization");
                        context.Response.StatusCode = 200;
                        context.Response.Close();
                    } catch { }
                    continue;
                }

                string path = context.Request.Url?.AbsolutePath ?? "/";
                string userAgent = context.Request.UserAgent ?? "";
                bool isMediaPlayer = userAgent.Contains("VLC", StringComparison.OrdinalIgnoreCase) ||
                                     userAgent.Contains("LibVLC", StringComparison.OrdinalIgnoreCase) ||
                                     userAgent.Contains("foobar2000", StringComparison.OrdinalIgnoreCase) ||
                                     userAgent.Contains("Winamp", StringComparison.OrdinalIgnoreCase) ||
                                     userAgent.Contains("Lavf", StringComparison.OrdinalIgnoreCase) ||
                                     userAgent.Contains("mpv", StringComparison.OrdinalIgnoreCase) ||
                                     userAgent.Contains("Audacious", StringComparison.OrdinalIgnoreCase) ||
                                     userAgent.Contains("iTunes", StringComparison.OrdinalIgnoreCase) ||
                                     userAgent.Contains("QuickTime", StringComparison.OrdinalIgnoreCase) ||
                                     userAgent.Contains("Wget", StringComparison.OrdinalIgnoreCase) ||
                                     userAgent.Contains("curl", StringComparison.OrdinalIgnoreCase);

                bool isStreamRoute = IsStreamPath(path) || (path == "/" && isMediaPlayer);
                bool isRestricted = _profileManager.CurrentProfile.RestrictToLocalNetwork && !IsLocalNetworkClient(context);

                if (isStreamRoute) {
                    if (isRestricted) {
                        context.Response.StatusCode = 403;
                        context.Response.ContentType = "application/json";
                        context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                        byte[] errBytes = System.Text.Encoding.UTF8.GetBytes("{\"error\":\"Private Stream\",\"message\":\"This broadcast is restricted to local network listeners.\",\"isPrivate\":true}");
                        context.Response.ContentLength64 = errBytes.Length;
                        context.Response.OutputStream.Write(errBytes, 0, errBytes.Length);
                        context.Response.Close();
                    } else {
                        _ = HandleStreamClient(context, token);
                    }
                } else if (path == "/listen.m3u" || path == "/playlist.m3u" || path == "/stream.m3u" || string.Equals(path, $"/{GetNormalizedMountPoint()}.m3u", StringComparison.OrdinalIgnoreCase)) {
                    HandleM3uRequest(context);
                } else if (path == "/listen.pls" || path == "/playlist.pls" || path == "/stream.pls" || string.Equals(path, $"/{GetNormalizedMountPoint()}.pls", StringComparison.OrdinalIgnoreCase)) {
                    HandlePlsRequest(context);
                } else if (path == "/api/events") {
                    _ = HandleSseClient(context, token);
                } else if (path == "/api/network") {
                    HandleNetworkRequest(context);
                } else if (path == "/api/albumart") {
                    if (isRestricted) {
                        context.Response.StatusCode = 403;
                        context.Response.Close();
                    } else {
                        HandleAlbumArtRequest(context);
                    }
                } else if (path == "/api/metadata") {
                    if (isRestricted) {
                        context.Response.StatusCode = 403;
                        context.Response.ContentType = "application/json";
                        context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                        byte[] priv = System.Text.Encoding.UTF8.GetBytes("{\"isPrivate\":true,\"title\":\"Private Stream\",\"artist\":\"Restricted to Local Network\"}");
                        context.Response.OutputStream.Write(priv, 0, priv.Length);
                        context.Response.Close();
                    } else {
                        HandleMetadataRequest(context);
                    }
                } else if (path == "/api/branding") {
                    HandleBrandingRequest(context);
                } else if (path == "/api/requests" && context.Request.HttpMethod == "POST") {
                    if (isRestricted) {
                        context.Response.StatusCode = 403;
                        context.Response.Close();
                    } else {
                        HandleSongRequest(context);
                    }
                } else if (path == "/api/chat" && context.Request.HttpMethod == "POST") {
                    if (isRestricted) {
                        context.Response.StatusCode = 403;
                        context.Response.Close();
                    } else {
                        HandleChatPostRequest(context);
                    }
                } else if (path == "/api/chat" && context.Request.HttpMethod == "GET") {
                    if (isRestricted) {
                        context.Response.ContentType = "application/json";
                        context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                        byte[] emptyChat = System.Text.Encoding.UTF8.GetBytes("{\"enabled\":false,\"messages\":[]}");
                        context.Response.OutputStream.Write(emptyChat, 0, emptyChat.Length);
                        context.Response.Close();
                    } else {
                        HandleChatGetRequest(context);
                    }
                } else if (path == "/api/reactions/clear" && context.Request.HttpMethod == "POST") {
                    HandleReactionClearRequest(context);
                } else if (path == "/api/reactions" && context.Request.HttpMethod == "POST") {
                    if (isRestricted) {
                        context.Response.StatusCode = 403;
                        context.Response.Close();
                    } else {
                        HandleReactionPostRequest(context);
                    }
                } else if (path == "/api/reactions" && context.Request.HttpMethod == "GET") {
                    HandleReactionGetRequest(context);
                } else if (path == "/api/history/clear" && context.Request.HttpMethod == "POST") {
                    HandleHistoryClearRequest(context);
                } else if (path == "/api/history" && context.Request.HttpMethod == "GET") {
                    if (isRestricted) {
                        context.Response.ContentType = "application/json";
                        context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                        byte[] emptyHist = System.Text.Encoding.UTF8.GetBytes("[]");
                        context.Response.OutputStream.Write(emptyHist, 0, emptyHist.Length);
                        context.Response.Close();
                    } else {
                        HandleHistoryGetRequest(context);
                    }
                } else if (path == "/api/status") {
                    HandleStatusRequest(context);
                } else {
                    // Serve Web Player assets: /, /index.html, /player.css, /player.js, /assets/*, etc.
                    Scrim.Web.EmbeddedWebPlayer.ServeAsync(context);
                }
            }
        }

        private string GetNormalizedMountPoint() {
            string raw = _profileManager.CurrentProfile.StreamMountPoint?.Trim().TrimStart('/') ?? "stream";
            if (string.IsNullOrEmpty(raw)) raw = "stream";
            return raw.ToLowerInvariant();
        }

        private bool IsStreamPath(string path) {
            string mount = GetNormalizedMountPoint();
            string p = (path ?? "").Trim().ToLowerInvariant();
            return p == $"/{mount}" || p == $"/{mount}.mp3" ||
                   p == "/stream" || p == "/stream.mp3" ||
                   p == "/live" || p == "/listen";
        }

        private bool IsLocalNetworkClient(HttpListenerContext context) {
            var remoteEp = context.Request.RemoteEndPoint;
            if (remoteEp == null) return true;
            var ip = remoteEp.Address;

            string? forwarded = context.Request.Headers["X-Forwarded-For"];
            if (!string.IsNullOrEmpty(forwarded)) {
                string firstIp = forwarded.Split(',')[0].Trim();
                if (IPAddress.TryParse(firstIp, out var fIp)) {
                    ip = fIp;
                }
            }

            if (IPAddress.IsLoopback(ip)) return true;

            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) {
                byte[] bytes = ip.GetAddressBytes();
                if (bytes[0] == 10) return true;
                if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;
                if (bytes[0] == 192 && bytes[1] == 168) return true;
                if (bytes[0] == 169 && bytes[1] == 254) return true;
                return false;
            }

            if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal) return true;
            byte[] v6Bytes = ip.GetAddressBytes();
            if ((v6Bytes[0] & 0xFE) == 0xFC) return true;

            return false;
        }

        private string ResolveClientHostAndPort(HttpListenerContext context, out string portStr) {
            var profile = _profileManager.CurrentProfile;
            int port = profile.Port;
            portStr = profile.UseReverseProxy ? "" : $":{port}";

            if (!string.IsNullOrWhiteSpace(profile.CustomPublicUrl)) {
                string c = profile.CustomPublicUrl.Trim();
                if (c.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) c = c.Substring(7);
                else if (c.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) c = c.Substring(8);
                int colon = c.LastIndexOf(':');
                if (colon > 0 && int.TryParse(c.Substring(colon + 1), out int customPort)) {
                    portStr = profile.UseReverseProxy ? "" : $":{customPort}";
                    return c.Substring(0, colon);
                }
                return c;
            }

            string rawHost = context.Request.Headers["X-Forwarded-Host"] ?? context.Request.Url?.Host ?? "localhost";
            return rawHost.Contains(':') ? rawHost.Split(':')[0] : rawHost;
        }

        private void HandleM3uRequest(HttpListenerContext context) {
            var response = context.Response;
            response.ContentType = "audio/x-mpegurl; charset=utf-8";
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            var profile = _profileManager.CurrentProfile;
            string host = ResolveClientHostAndPort(context, out string portStr);
            string streamPath = GetNormalizedMountPoint();
            string scheme = profile.UseHttps ? "https" : (string.Equals(context.Request.Headers["X-Forwarded-Proto"], "https", StringComparison.OrdinalIgnoreCase) ? "https" : "http");
            string m3uContent = $"#EXTM3U\r\n#EXTINF:-1,{profile.StationName ?? "Scrim Broadcast"}\r\n{scheme}://{host}{portStr}/{streamPath}\r\n";
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(m3uContent);
            response.ContentLength64 = bytes.Length;
            response.OutputStream.Write(bytes, 0, bytes.Length);
            response.Close();
        }

        private void HandlePlsRequest(HttpListenerContext context) {
            var response = context.Response;
            response.ContentType = "audio/x-scpls; charset=utf-8";
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            var profile = _profileManager.CurrentProfile;
            string host = ResolveClientHostAndPort(context, out string portStr);
            string streamPath = GetNormalizedMountPoint();
            string scheme = profile.UseHttps ? "https" : (string.Equals(context.Request.Headers["X-Forwarded-Proto"], "https", StringComparison.OrdinalIgnoreCase) ? "https" : "http");
            string plsContent = $"[playlist]\r\nNumberOfEntries=1\r\nFile1={scheme}://{host}{portStr}/{streamPath}\r\nTitle1={profile.StationName ?? "Scrim Broadcast"}\r\nLength1=-1\r\nVersion=2\r\n";
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(plsContent);
            response.ContentLength64 = bytes.Length;
            response.OutputStream.Write(bytes, 0, bytes.Length);
            response.Close();
        }

        private void HandleStatusRequest(HttpListenerContext context) {
            var response = context.Response;
            response.ContentType = "application/json";
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            bool isLive = _hub.IsBroadcasting;
            var profile = _profileManager.CurrentProfile;
            string format = profile.AudioFormat?.ToUpperInvariant() ?? "MP3";
            int bitrate = profile.Bitrate;
            bool isRestricted = profile.RestrictToLocalNetwork && !IsLocalNetworkClient(context);
            string streamMount = GetNormalizedMountPoint();
            string json = $"{{\"isLive\":{(isLive ? "true" : "false")},\"listeners\":{_hub.ActiveClientCount},\"format\":\"{EscapeJson(format)}\",\"bitrate\":{bitrate},\"streamUrl\":\"/{streamMount}\",\"isPrivate\":{(isRestricted ? "true" : "false")},\"restrictToLocal\":{(profile.RestrictToLocalNetwork ? "true" : "false")},\"useHttps\":{(profile.UseHttps ? "true" : "false")},\"useReverseProxy\":{(profile.UseReverseProxy ? "true" : "false")}}}";
            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(json);
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.Close();
        }

        private async Task HandleStreamClient(HttpListenerContext context, CancellationToken token) {
            var response = context.Response;
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Cache-Control", "no-cache, no-store, must-revalidate");
            response.Headers.Add("Pragma", "no-cache");
            response.Headers.Add("Expires", "0");
            response.Headers.Add("Accept-Ranges", "none");
            response.Headers.Add("icy-name", _profileManager.CurrentProfile.StationName ?? "Scrim Broadcast Station");
            response.Headers.Add("icy-genre", "Live Stream");
            response.Headers.Add("icy-br", _profileManager.CurrentProfile.Bitrate.ToString());

            if (!_hub.IsBroadcasting) {
                response.StatusCode = 503;
                response.ContentType = "application/json";
                byte[] offlineBytes = System.Text.Encoding.UTF8.GetBytes("{\"error\":\"Station is offline\",\"isLive\":false}");
                response.ContentLength64 = offlineBytes.Length;
                response.OutputStream.Write(offlineBytes, 0, offlineBytes.Length);
                response.Close();
                return;
            }

            string format = _profileManager.CurrentProfile.AudioFormat?.ToLowerInvariant() ?? "mp3";
            response.ContentType = format switch {
                "opus" => "audio/ogg; codecs=opus",
                "aac" => "audio/aac",
                "flac" => "audio/flac",
                _ => "audio/mpeg"
            };
            response.SendChunked = true;

            var client = _hub.RegisterClient();
            try {
                using var stream = response.OutputStream;
                while (!token.IsCancellationRequested) {
                    var frame = await client.AudioChannel.Reader.ReadAsync(token);
                    await stream.WriteAsync(frame, token);
                    await stream.FlushAsync(token);
                }
            } catch {
            } finally {
                _hub.UnregisterClient(client.ClientId);
                response.Close();
            }
        }

        private async Task HandleSseClient(HttpListenerContext context, CancellationToken token) {
            var response = context.Response;
            response.ContentType = "text/event-stream";
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Cache-Control", "no-cache");
            response.Headers.Add("Connection", "keep-alive");

            var writeLock = new SemaphoreSlim(1, 1);
            var immediateChannel = System.Threading.Channels.Channel.CreateUnbounded<string>();

            Action<ChatMessage> onMessage = (msg) => {
                string chatJson = $"{{\"type\":\"chat\",\"id\":\"{EscapeJson(msg.Id)}\",\"sender\":\"{EscapeJson(msg.Sender)}\",\"text\":\"{EscapeJson(msg.Text)}\",\"timestamp\":\"{msg.Timestamp:o}\",\"isHost\":{(msg.IsHost ? "true" : "false")},\"color\":\"{EscapeJson(msg.Color)}\"}}";
                immediateChannel.Writer.TryWrite($"data: {chatJson}\n\n");
            };

            Action onClear = () => {
                immediateChannel.Writer.TryWrite("data: {\"type\":\"chat_clear\"}\n\n");
            };

            Action<bool> onStatus = (enabled) => {
                immediateChannel.Writer.TryWrite($"data: {{\"type\":\"chat_status\",\"enabled\":{(enabled ? "true" : "false")}}}\n\n");
            };

            Action<string, string> onAssign = (target, assigned) => {
                string assignJson = $"{{\"type\":\"assign_nickname\",\"target\":\"{EscapeJson(target)}\",\"assigned\":\"{EscapeJson(assigned)}\"}}";
                immediateChannel.Writer.TryWrite($"data: {assignJson}\n\n");
            };

            Action<string> onBanned = (bannedId) => {
                immediateChannel.Writer.TryWrite($"data: {{\"type\":\"chat_user_banned\",\"userId\":\"{EscapeJson(bannedId)}\"}}\n\n");
            };

            Action<string> onUnbanned = (unbannedId) => {
                immediateChannel.Writer.TryWrite($"data: {{\"type\":\"chat_user_unbanned\",\"userId\":\"{EscapeJson(unbannedId)}\"}}\n\n");
            };

            Action<string, ReactionCounts> onReaction = (type, counts) => {
                string reactionJson = $"{{\"type\":\"reaction\",\"reaction\":\"{EscapeJson(type)}\",\"counts\":{{\"thumbsUp\":{counts.ThumbsUp},\"thumbsDown\":{counts.ThumbsDown},\"heart\":{counts.Heart}}}}}";
                immediateChannel.Writer.TryWrite($"data: {reactionJson}\n\n");
                var currentHistory = _historyService.GetHistory(_profileManager.CurrentProfile.SongHistoryLimit);
                string historyJson = FormatHistoryJson(currentHistory);
                immediateChannel.Writer.TryWrite($"data: {{\"type\":\"history_update\",\"history\":{historyJson}}}\n\n");
            };

            Action<ReactionCounts> onResetReactions = (counts) => {
                string resetJson = $"{{\"type\":\"reaction_reset\",\"counts\":{{\"thumbsUp\":{counts.ThumbsUp},\"thumbsDown\":{counts.ThumbsDown},\"heart\":{counts.Heart}}}}}";
                immediateChannel.Writer.TryWrite($"data: {resetJson}\n\n");
                var currentHistory = _historyService.GetHistory(_profileManager.CurrentProfile.SongHistoryLimit);
                string historyJson = FormatHistoryJson(currentHistory);
                immediateChannel.Writer.TryWrite($"data: {{\"type\":\"history_update\",\"history\":{historyJson}}}\n\n");
            };

            Action<IReadOnlyList<SongHistoryItem>> onHistoryChanged = (items) => {
                string historyJson = FormatHistoryJson(items.Take(_profileManager.CurrentProfile.SongHistoryLimit).ToList());
                immediateChannel.Writer.TryWrite($"data: {{\"type\":\"history_update\",\"history\":{historyJson}}}\n\n");
            };

            Action onHistorySettings = () => {
                var currentHistory = _historyService.GetHistory(_profileManager.CurrentProfile.SongHistoryLimit);
                string historyJson = FormatHistoryJson(currentHistory);
                immediateChannel.Writer.TryWrite($"data: {{\"type\":\"history_update\",\"history\":{historyJson}}}\n\n");
            };

            string BuildMetadataJson(MediaMetadata meta) {
                bool hasArt = (meta.AlbumArt != null && meta.AlbumArt.Length > 0) || !string.IsNullOrEmpty(meta.AlbumArtUrl);
                string artUrl = hasArt ? (string.IsNullOrEmpty(meta.AlbumArtUrl) ? "/api/albumart" : meta.AlbumArtUrl) : "";
                double durationSec = meta.Duration.TotalSeconds;
                double positionSec = meta.Position.TotalSeconds;
                bool isPlaying = meta.IsPlaying;
                return $"{{\"type\":\"metadata\",\"title\":\"{EscapeJson(meta.Title)}\",\"artist\":\"{EscapeJson(meta.Artist)}\",\"album\":\"{EscapeJson(meta.Album)}\",\"hasArt\":{(hasArt ? "true" : "false")},\"albumArtUrl\":\"{EscapeJson(artUrl)}\",\"duration\":{durationSec.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)},\"position\":{positionSec.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)},\"isPlaying\":{(isPlaying ? "true" : "false")}}}";
            }

            string BuildStatsJson(bool isLive) {
                var profile = _profileManager.CurrentProfile;
                string formatStr = profile.AudioFormat?.ToUpperInvariant() ?? "MP3";
                int bitrateVal = profile.Bitrate;
                string streamMount = GetNormalizedMountPoint();
                return $"{{\"type\":\"stats\",\"listeners\":{_hub.ActiveClientCount},\"isLive\":{(isLive ? "true" : "false")},\"format\":\"{EscapeJson(formatStr)}\",\"bitrate\":{bitrateVal},\"streamUrl\":\"/{streamMount}\",\"isPrivate\":false}}";
            }

            string BuildQueueJson() {
                var requests = _requestController.GetLiveQueue().Select(r => $"{{\"query\":\"{EscapeJson(r.Query)}\",\"dedication\":\"{EscapeJson(r.Dedication)}\",\"status\":\"{EscapeJson(r.Status)}\"}}");
                return $"{{\"type\":\"queue\",\"requests\":[{string.Join(",", requests)}]}}";
            }

            string BuildBrandingJson() {
                var profile = _profileManager.CurrentProfile;
                var navLinksArray = string.Join(",", profile.CustomNavLinks.Select(l => $"{{\"label\":\"{EscapeJson(l.Label)}\",\"url\":\"{EscapeJson(l.Url)}\"}}"));
                string customThemeJson = GetCustomThemeJson(profile.WebTheme ?? "dark");
                string streamMount = GetNormalizedMountPoint();
                return $"{{\"type\":\"branding\",\"stationName\":\"{EscapeJson(profile.StationName)}\",\"pageTitle\":\"{EscapeJson(profile.PageTitle)}\",\"showTitle\":\"{EscapeJson(profile.ShowTitle)}\",\"hostName\":\"{EscapeJson(profile.HostName)}\",\"genreTag\":\"{EscapeJson(profile.GenreTag)}\",\"tagline\":\"{EscapeJson(profile.StationTagline)}\",\"accentColor\":\"{EscapeJson(profile.AccentColor)}\",\"theme\":\"{EscapeJson(profile.WebTheme ?? "dark")}\",\"customThemeVariables\":{customThemeJson},\"logoUrl\":\"{EscapeJson(profile.LogoUrl)}\",\"navLinks\":\"{EscapeJson(profile.NavLinks)}\",\"customNavLinks\":[{navLinksArray}],\"streamUrl\":\"/{streamMount}\",\"isPrivate\":false,\"restrictToLocal\":{(profile.RestrictToLocalNetwork ? "true" : "false")}}}";
            }

            EventHandler<MediaMetadata> onMetadata = (_, meta) => {
                immediateChannel.Writer.TryWrite($"data: {BuildMetadataJson(meta)}\n\n");
            };

            EventHandler<bool> onBroadcastChanged = (_, isLive) => {
                immediateChannel.Writer.TryWrite($"data: {BuildStatsJson(isLive)}\n\n");
            };

            _chatService.MessagePosted += onMessage;
            _chatService.ChatCleared += onClear;
            _chatService.ChatStatusChanged += onStatus;
            _chatService.NicknameAssigned += onAssign;
            _chatService.UserBanned += onBanned;
            _chatService.UserUnbanned += onUnbanned;
            _reactionService.ReactionReceived += onReaction;
            _reactionService.CountsReset += onResetReactions;
            _historyService.HistoryChanged += onHistoryChanged;
            _metadataService.MetadataChanged += onMetadata;
            _hub.BroadcastingStateChanged += onBroadcastChanged;
            HistorySettingsChanged += onHistorySettings;

            try {
                using var writer = new StreamWriter(response.OutputStream);

                async Task SendEvent(string eventData) {
                    await writeLock.WaitAsync(token);
                    try {
                        await writer.WriteAsync(eventData);
                        await writer.FlushAsync();
                    } finally {
                        writeLock.Release();
                    }
                }

                // Initial Reaction State Push
                var initialCounts = _reactionService.CurrentCounts;
                await SendEvent($"data: {{\"type\":\"reaction_init\",\"counts\":{{\"thumbsUp\":{initialCounts.ThumbsUp},\"thumbsDown\":{initialCounts.ThumbsDown},\"heart\":{initialCounts.Heart}}}}}\n\n");

                // Initial Song History Push
                var initialHistory = _historyService.GetHistory(_profileManager.CurrentProfile.SongHistoryLimit);
                await SendEvent($"data: {{\"type\":\"history_init\",\"history\":{FormatHistoryJson(initialHistory)}}}\n\n");

                // Initial Chat State Push
                var recentChat = _chatService.GetRecentMessages().Select(m =>
                    $"{{\"id\":\"{EscapeJson(m.Id)}\",\"sender\":\"{EscapeJson(m.Sender)}\",\"text\":\"{EscapeJson(m.Text)}\",\"timestamp\":\"{m.Timestamp:o}\",\"isHost\":{(m.IsHost ? "true" : "false")},\"color\":\"{EscapeJson(m.Color)}\"}}"
                );
                await SendEvent($"data: {{\"type\":\"chat_init\",\"enabled\":{(_profileManager.CurrentProfile.EnableChat ? "true" : "false")},\"messages\":[{string.Join(",", recentChat)}]}}\n\n");

                // Initial Instant Stats, Metadata, Queue, and Branding Push
                await SendEvent($"data: {BuildStatsJson(_hub.IsBroadcasting)}\n\n");
                await SendEvent($"data: {BuildMetadataJson(_metadataService.CurrentMetadata)}\n\n");
                await SendEvent($"data: {BuildQueueJson()}\n\n");
                await SendEvent($"data: {BuildBrandingJson()}\n\n");

                // Immediate Event Consumer (sub-millisecond latency for chat, reactions, and live status changes)
                var immediateTask = Task.Run(async () => {
                    try {
                        await foreach (var item in immediateChannel.Reader.ReadAllAsync(token)) {
                            await SendEvent(item);
                        }
                    } catch { }
                }, token);

                // Periodic Heartbeat & Metadata Broadcast (every 2 seconds)
                var periodicTask = Task.Run(async () => {
                    try {
                        while (!token.IsCancellationRequested) {
                            await Task.Delay(2000, token);
                            await SendEvent($"data: {BuildMetadataJson(_metadataService.CurrentMetadata)}\n\n");
                            await SendEvent($"data: {BuildStatsJson(_hub.IsBroadcasting)}\n\n");
                            await SendEvent($"data: {BuildQueueJson()}\n\n");
                            await SendEvent($"data: {BuildBrandingJson()}\n\n");
                        }
                    } catch { }
                }, token);

                await Task.WhenAny(immediateTask, periodicTask);
            } catch {
            } finally {
                _chatService.MessagePosted -= onMessage;
                _chatService.ChatCleared -= onClear;
                _chatService.ChatStatusChanged -= onStatus;
                _chatService.NicknameAssigned -= onAssign;
                _chatService.UserBanned -= onBanned;
                _chatService.UserUnbanned -= onUnbanned;
                _reactionService.ReactionReceived -= onReaction;
                _reactionService.CountsReset -= onResetReactions;
                _historyService.HistoryChanged -= onHistoryChanged;
                _metadataService.MetadataChanged -= onMetadata;
                _hub.BroadcastingStateChanged -= onBroadcastChanged;
                HistorySettingsChanged -= onHistorySettings;
                writeLock.Dispose();
                response.Close();
            }
        }

        private void HandleChatGetRequest(HttpListenerContext context) {
            try {
                var response = context.Response;
                response.ContentType = "application/json; charset=utf-8";
                response.Headers.Add("Access-Control-Allow-Origin", "*");
                string rawClientId = context.Request.QueryString["clientId"] ?? "";
                string userHash = ComputeUserHash(context, rawClientId);
                bool isBanned = _chatService.IsUserBanned(userHash);
                bool isEnabled = _profileManager.CurrentProfile.EnableChat;
                var blacklist = _profileManager.CurrentProfile.NicknameBlacklist;
                string blacklistJson = string.Join(",", blacklist.Select(b => $"\"{EscapeJson(b)}\""));
                var messages = _chatService.GetRecentMessages().Select(m => 
                    $"{{\"id\":\"{EscapeJson(m.Id)}\",\"sender\":\"{EscapeJson(m.Sender)}\",\"text\":\"{EscapeJson(m.Text)}\",\"timestamp\":\"{m.Timestamp:o}\",\"isHost\":{(m.IsHost ? "true" : "false")},\"color\":\"{EscapeJson(m.Color)}\"}}"
                );
                string json = $"{{\"enabled\":{(isEnabled ? "true" : "false")},\"isBanned\":{(isBanned ? "true" : "false")},\"blacklist\":[{blacklistJson}],\"messages\":[{string.Join(",", messages)}]}}";
                byte[] buffer = System.Text.Encoding.UTF8.GetBytes(json);
                response.ContentLength64 = buffer.Length;
                response.OutputStream.Write(buffer, 0, buffer.Length);
            } catch {
                context.Response.StatusCode = 500;
            } finally {
                context.Response.Close();
            }
        }

        private void HandleChatPostRequest(HttpListenerContext context) {
            try {
                if (!_profileManager.CurrentProfile.EnableChat) {
                    context.Response.StatusCode = 403;
                    byte[] err = System.Text.Encoding.UTF8.GetBytes("{\"error\":\"Chat is currently disabled\"}");
                    context.Response.ContentType = "application/json; charset=utf-8";
                    context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                    context.Response.OutputStream.Write(err, 0, err.Length);
                    context.Response.Close();
                    return;
                }

                using var reader = new StreamReader(context.Request.InputStream, System.Text.Encoding.UTF8);
                string body = reader.ReadToEnd();
                var textMatch = System.Text.RegularExpressions.Regex.Match(body, "\"text\"\\s*:\\s*\"(.*?)\"");
                var senderMatch = System.Text.RegularExpressions.Regex.Match(body, "\"sender\"\\s*:\\s*\"(.*?)\"");
                var clientMatch = System.Text.RegularExpressions.Regex.Match(body, "\"clientId\"\\s*:\\s*\"(.*?)\"");

                string rawClientId = clientMatch.Success ? clientMatch.Groups[1].Value : (context.Request.QueryString["clientId"] ?? "");
                string userHash = ComputeUserHash(context, rawClientId);

                if (_chatService.IsUserBanned(userHash)) {
                    context.Response.StatusCode = 403;
                    byte[] err = System.Text.Encoding.UTF8.GetBytes("{\"error\":\"You have been banned from sending messages in this chat.\",\"isBanned\":true}");
                    context.Response.ContentType = "application/json; charset=utf-8";
                    context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                    context.Response.OutputStream.Write(err, 0, err.Length);
                    context.Response.Close();
                    return;
                }

                string text = textMatch.Success ? textMatch.Groups[1].Value : "";
                string sender = senderMatch.Success ? senderMatch.Groups[1].Value : "Anonymous";

                text = System.Text.RegularExpressions.Regex.Unescape(text);
                sender = System.Text.RegularExpressions.Regex.Unescape(sender);

                if (!_chatService.IsNicknameAllowed(sender, _profileManager.CurrentProfile.NicknameBlacklist)) {
                    context.Response.StatusCode = 400;
                    byte[] err = System.Text.Encoding.UTF8.GetBytes("{\"error\":\"This nickname is not permitted on this station\"}");
                    context.Response.ContentType = "application/json; charset=utf-8";
                    context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                    context.Response.OutputStream.Write(err, 0, err.Length);
                    context.Response.Close();
                    return;
                }

                if (!string.IsNullOrWhiteSpace(text)) {
                    _chatService.AddMessage(sender, text, isHost: false, userId: userHash);
                }

                context.Response.ContentType = "application/json; charset=utf-8";
                context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                context.Response.StatusCode = 200;
                byte[] ok = System.Text.Encoding.UTF8.GetBytes("{\"success\":true}");
                context.Response.OutputStream.Write(ok, 0, ok.Length);
            } catch {
                context.Response.StatusCode = 400;
            } finally {
                context.Response.Close();
            }
        }

        private string ComputeUserHash(HttpListenerContext context, string? rawClientId) {
            string ip = context.Request.Headers["X-Forwarded-For"] ?? context.Request.RemoteEndPoint?.Address.ToString() ?? "unknown";
            string userAgent = context.Request.Headers["User-Agent"] ?? "";
            string client = string.IsNullOrWhiteSpace(rawClientId) ? "anon" : rawClientId.Trim();
            string raw = $"{ip}_{client}_{userAgent}";
            using var sha = System.Security.Cryptography.SHA256.Create();
            byte[] bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(bytes).Substring(0, 16);
        }

        private void HandleReactionGetRequest(HttpListenerContext context) {
            try {
                var response = context.Response;
                response.ContentType = "application/json; charset=utf-8";
                response.Headers.Add("Access-Control-Allow-Origin", "*");
                string rawClientId = context.Request.QueryString["clientId"] ?? "";
                string userHash = ComputeUserHash(context, rawClientId);

                var counts = _reactionService.CurrentCounts;
                string? userReaction = _reactionService.GetUserReaction(userHash);
                string userReactionJson = userReaction != null ? $"\"{EscapeJson(userReaction)}\"" : "null";
                string json = $"{{\"thumbsUp\":{counts.ThumbsUp},\"thumbsDown\":{counts.ThumbsDown},\"heart\":{counts.Heart},\"userReaction\":{userReactionJson}}}";
                byte[] buffer = System.Text.Encoding.UTF8.GetBytes(json);
                response.ContentLength64 = buffer.Length;
                response.OutputStream.Write(buffer, 0, buffer.Length);
            } catch {
                context.Response.StatusCode = 500;
            } finally {
                context.Response.Close();
            }
        }

        private void HandleReactionPostRequest(HttpListenerContext context) {
            try {
                using var reader = new StreamReader(context.Request.InputStream, System.Text.Encoding.UTF8);
                string body = reader.ReadToEnd();
                var typeMatch = System.Text.RegularExpressions.Regex.Match(body, "\"type\"\\s*:\\s*\"(.*?)\"");
                string reactionType = typeMatch.Success ? typeMatch.Groups[1].Value : (context.Request.QueryString["type"] ?? "");

                var clientMatch = System.Text.RegularExpressions.Regex.Match(body, "\"clientId\"\\s*:\\s*\"(.*?)\"");
                string rawClientId = clientMatch.Success ? clientMatch.Groups[1].Value : (context.Request.QueryString["clientId"] ?? "");
                string userHash = ComputeUserHash(context, rawClientId);

                var counts = _reactionService.AddOrSwitchReaction(userHash, reactionType, out string? activeReaction);

                context.Response.ContentType = "application/json; charset=utf-8";
                context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                context.Response.StatusCode = 200;
                string activeReactionJson = activeReaction != null ? $"\"{EscapeJson(activeReaction)}\"" : "null";
                string json = $"{{\"success\":true,\"userReaction\":{activeReactionJson},\"counts\":{{\"thumbsUp\":{counts.ThumbsUp},\"thumbsDown\":{counts.ThumbsDown},\"heart\":{counts.Heart}}}}}";
                byte[] ok = System.Text.Encoding.UTF8.GetBytes(json);
                context.Response.OutputStream.Write(ok, 0, ok.Length);
            } catch {
                context.Response.StatusCode = 400;
            } finally {
                context.Response.Close();
            }
        }

        private void HandleReactionClearRequest(HttpListenerContext context) {
            try {
                using var reader = new StreamReader(context.Request.InputStream, System.Text.Encoding.UTF8);
                string body = reader.ReadToEnd();
                var keyMatch = System.Text.RegularExpressions.Regex.Match(body, "\"songKey\"\\s*:\\s*\"(.*?)\"");
                string songKey = keyMatch.Success ? keyMatch.Groups[1].Value : (context.Request.QueryString["songKey"] ?? "");
                if (!string.IsNullOrWhiteSpace(songKey)) {
                    _reactionService.ResetSong(songKey);
                } else {
                    _reactionService.Reset();
                }
                context.Response.ContentType = "application/json; charset=utf-8";
                context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                context.Response.StatusCode = 200;
                byte[] ok = System.Text.Encoding.UTF8.GetBytes("{\"success\":true}");
                context.Response.OutputStream.Write(ok, 0, ok.Length);
            } catch {
                context.Response.StatusCode = 400;
            } finally {
                context.Response.Close();
            }
        }

        private void HandleHistoryGetRequest(HttpListenerContext context) {
            try {
                var response = context.Response;
                response.ContentType = "application/json; charset=utf-8";
                response.Headers.Add("Access-Control-Allow-Origin", "*");
                int limit = _profileManager.CurrentProfile.SongHistoryLimit;
                if (int.TryParse(context.Request.QueryString["limit"], out int qLimit) && qLimit > 0) {
                    limit = qLimit;
                }
                var items = _historyService.GetHistory(limit);
                string json = FormatHistoryJson(items);
                byte[] buffer = System.Text.Encoding.UTF8.GetBytes(json);
                response.ContentLength64 = buffer.Length;
                response.OutputStream.Write(buffer, 0, buffer.Length);
            } catch {
                context.Response.StatusCode = 500;
            } finally {
                context.Response.Close();
            }
        }

        private void HandleHistoryClearRequest(HttpListenerContext context) {
            try {
                _historyService.Clear();
                context.Response.ContentType = "application/json; charset=utf-8";
                context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                context.Response.StatusCode = 200;
                byte[] ok = System.Text.Encoding.UTF8.GetBytes("{\"success\":true}");
                context.Response.OutputStream.Write(ok, 0, ok.Length);
            } catch {
                context.Response.StatusCode = 400;
            } finally {
                context.Response.Close();
            }
        }

        private string FormatHistoryJson(IReadOnlyList<SongHistoryItem> items) {
            var elements = items.Select(item => {
                string artUrl = !string.IsNullOrEmpty(item.AlbumArtUrl) ? item.AlbumArtUrl : "";
                bool hasArt = !string.IsNullOrEmpty(artUrl) || (item.AlbumArt != null && item.AlbumArt.Length > 0);
                var counts = _reactionService.GetSongReactions(item.Title, item.Artist);
                return $"{{\"id\":\"{item.Id}\",\"title\":\"{EscapeJson(item.Title)}\",\"artist\":\"{EscapeJson(item.Artist)}\",\"album\":\"{EscapeJson(item.Album)}\",\"playedAt\":\"{EscapeJson(item.PlayedAtFormatted)}\",\"hasArt\":{(hasArt ? "true" : "false")},\"albumArtUrl\":\"{EscapeJson(artUrl)}\",\"thumbsUp\":{counts.ThumbsUp},\"thumbsDown\":{counts.ThumbsDown},\"heart\":{counts.Heart}}}";
            });
            return "[" + string.Join(",", elements) + "]";
        }

        private void HandleBrandingRequest(HttpListenerContext context) {
            try {
                var profile = _profileManager.CurrentProfile;
                var navLinksArray = string.Join(",", profile.CustomNavLinks.Select(l => $"{{\"label\":\"{EscapeJson(l.Label)}\",\"url\":\"{EscapeJson(l.Url)}\"}}"));
                string customThemeJson = GetCustomThemeJson(profile.WebTheme ?? "dark");
                bool isRestricted = profile.RestrictToLocalNetwork && !IsLocalNetworkClient(context);
                string streamMount = GetNormalizedMountPoint();
                string json = $"{{\"stationName\":\"{EscapeJson(profile.StationName)}\",\"pageTitle\":\"{EscapeJson(profile.PageTitle)}\",\"showTitle\":\"{EscapeJson(profile.ShowTitle)}\",\"hostName\":\"{EscapeJson(profile.HostName)}\",\"genreTag\":\"{EscapeJson(profile.GenreTag)}\",\"tagline\":\"{EscapeJson(profile.StationTagline)}\",\"accentColor\":\"{EscapeJson(profile.AccentColor)}\",\"theme\":\"{EscapeJson(profile.WebTheme ?? "dark")}\",\"customThemeVariables\":{customThemeJson},\"logoUrl\":\"{EscapeJson(profile.LogoUrl)}\",\"navLinks\":\"{EscapeJson(profile.NavLinks)}\",\"customNavLinks\":[{navLinksArray}],\"streamUrl\":\"/{streamMount}\",\"isPrivate\":{(isRestricted ? "true" : "false")},\"restrictToLocal\":{(profile.RestrictToLocalNetwork ? "true" : "false")}}}";
                byte[] buffer = System.Text.Encoding.UTF8.GetBytes(json);
                context.Response.ContentType = "application/json";
                context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                context.Response.ContentLength64 = buffer.Length;
                context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            } catch {
                context.Response.StatusCode = 500;
            } finally {
                context.Response.Close();
            }
        }

        private void HandleSongRequest(HttpListenerContext context) {
            try {
                using var reader = new StreamReader(context.Request.InputStream, System.Text.Encoding.UTF8);
                string body = reader.ReadToEnd();
                var queryMatch = System.Text.RegularExpressions.Regex.Match(body, "\"query\"\\s*:\\s*\"(.*?)\"");
                var dedicationMatch = System.Text.RegularExpressions.Regex.Match(body, "\"dedication\"\\s*:\\s*\"(.*?)\"");

                string query = queryMatch.Success ? queryMatch.Groups[1].Value : "";
                string dedication = dedicationMatch.Success ? dedicationMatch.Groups[1].Value : "";

                query = System.Text.RegularExpressions.Regex.Unescape(query);
                dedication = System.Text.RegularExpressions.Regex.Unescape(dedication);

                if (!string.IsNullOrWhiteSpace(query)) {
                    _requestController.SubmitRequest(query, dedication);
                }
                context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                context.Response.StatusCode = 200;
            } catch {
                context.Response.StatusCode = 400;
            } finally {
                context.Response.Close();
            }
        }

        private void HandleNetworkRequest(HttpListenerContext context) {
            try {
                var profile = _profileManager.CurrentProfile;
                int port = profile.Port;
                string scheme = profile.UseHttps ? "https" : "http";
                string portStr = profile.UseReverseProxy ? "" : $":{port}";
                var response = context.Response;
                response.ContentType = "application/json";
                response.Headers.Add("Access-Control-Allow-Origin", "*");
                string localUrl = $"{scheme}://{_networkDiscovery.PrimaryLocalIp}:{port}";
                string publicUrl;
                if (!string.IsNullOrEmpty(profile.CustomPublicUrl)) {
                    string c = profile.CustomPublicUrl.Trim();
                    if (c.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) c = c.Substring(7);
                    else if (c.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) c = c.Substring(8);
                    if (profile.UseReverseProxy) {
                        int colon = c.LastIndexOf(':');
                        if (colon > 0 && int.TryParse(c.Substring(colon + 1), out _)) c = c.Substring(0, colon);
                    }
                    publicUrl = $"{scheme}://{c}";
                } else {
                    string ip = (!string.IsNullOrEmpty(_networkDiscovery.PublicIp) && _networkDiscovery.PublicIp != "Discovering..." && _networkDiscovery.PublicIp != "Unavailable")
                        ? _networkDiscovery.PublicIp
                        : _networkDiscovery.PrimaryLocalIp;
                    publicUrl = $"{scheme}://{ip}{portStr}";
                }
                string json = $"{{\"localUrl\":\"{localUrl}\",\"publicUrl\":\"{publicUrl}\",\"primaryLocalIp\":\"{_networkDiscovery.PrimaryLocalIp}\",\"publicIp\":\"{_networkDiscovery.PublicIp}\",\"upnpStatus\":\"{EscapeJson(_networkDiscovery.UpnpStatus)}\",\"isUpnpMapped\":{(_networkDiscovery.IsUpnpMapped ? "true" : "false")},\"useHttps\":{(profile.UseHttps ? "true" : "false")},\"useReverseProxy\":{(profile.UseReverseProxy ? "true" : "false")}}}";
                byte[] buffer = System.Text.Encoding.UTF8.GetBytes(json);
                response.ContentLength64 = buffer.Length;
                response.OutputStream.Write(buffer, 0, buffer.Length);
            } catch {
                context.Response.StatusCode = 500;
            } finally {
                context.Response.Close();
            }
        }

        private void HandleAlbumArtRequest(HttpListenerContext context) {
            try {
                var response = context.Response;
                response.Headers.Add("Access-Control-Allow-Origin", "*");
                response.Headers.Add("Cache-Control", "public, max-age=60");

                var meta = _metadataService.CurrentMetadata;
                if (meta.AlbumArt != null && meta.AlbumArt.Length > 0) {
                    response.ContentType = (meta.AlbumArt.Length > 8 && meta.AlbumArt[0] == 0x89 && meta.AlbumArt[1] == 0x50) 
                        ? "image/png" 
                        : "image/jpeg";
                    response.ContentLength64 = meta.AlbumArt.Length;
                    response.OutputStream.Write(meta.AlbumArt, 0, meta.AlbumArt.Length);
                    response.Close();
                    return;
                }

                if (!string.IsNullOrEmpty(meta.AlbumArtUrl)) {
                    response.Redirect(meta.AlbumArtUrl);
                    response.Close();
                    return;
                }

                response.StatusCode = 404;
            } catch {
                context.Response.StatusCode = 500;
            } finally {
                context.Response.Close();
            }
        }

        private void HandleMetadataRequest(HttpListenerContext context) {
            try {
                var response = context.Response;
                response.ContentType = "application/json";
                response.Headers.Add("Access-Control-Allow-Origin", "*");
                response.Headers.Add("Cache-Control", "no-cache");

                var meta = _metadataService.CurrentMetadata;
                bool hasArt = (meta.AlbumArt != null && meta.AlbumArt.Length > 0) || !string.IsNullOrEmpty(meta.AlbumArtUrl);
                string artUrl = hasArt ? (string.IsNullOrEmpty(meta.AlbumArtUrl) ? "/api/albumart" : meta.AlbumArtUrl) : "";
                double durationSec = meta.Duration.TotalSeconds;
                double positionSec = meta.Position.TotalSeconds;
                bool isPlaying = meta.IsPlaying;
                string json = $"{{\"title\":\"{EscapeJson(meta.Title)}\",\"artist\":\"{EscapeJson(meta.Artist)}\",\"album\":\"{EscapeJson(meta.Album)}\",\"hasArt\":{(hasArt ? "true" : "false")},\"albumArtUrl\":\"{EscapeJson(artUrl)}\",\"duration\":{durationSec.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)},\"position\":{positionSec.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)},\"isPlaying\":{(isPlaying ? "true" : "false")}}}";

                byte[] buffer = System.Text.Encoding.UTF8.GetBytes(json);
                response.ContentLength64 = buffer.Length;
                response.OutputStream.Write(buffer, 0, buffer.Length);
            } catch {
                context.Response.StatusCode = 500;
            } finally {
                context.Response.Close();
            }
        }

        private string EscapeJson(string? value) {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        public void Stop() {
            _cts?.Cancel();
            try {
                _bridgeListener?.Stop();
            } catch { }
            _bridgeListener = null;

            try {
                _listener?.Stop();
                _listener?.Close();
            } catch { }
            _listener = null;
            if (_profileManager.CurrentProfile.EnableUpnpPortForwarding) {
                _networkDiscovery.TryUnmapUpnpPort(_profileManager.CurrentProfile.Port);
            }
        }

        public void Dispose() {
            Stop();
        }
    }
}
