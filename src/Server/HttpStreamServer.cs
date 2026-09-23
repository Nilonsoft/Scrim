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
        private readonly Scrim.Audio.StreamRelayService? _relayService;
        private readonly IMultiStationManager? _stationManager;

        public event Action? HistorySettingsChanged;
        public event Action? BrandingSettingsChanged;
        public event Action<string>? PartyCelebrationReceived;

        public void BroadcastHistoryUpdate() {
            HistorySettingsChanged?.Invoke();
        }

        public void BroadcastBrandingUpdate() {
            BrandingSettingsChanged?.Invoke();
        }

        public void BroadcastPartyCelebration(string sender) {
            PartyCelebrationReceived?.Invoke(sender);
        }
        private HttpListener? _listener;
        private TcpListener? _bridgeListener;
        private CancellationTokenSource? _cts;

        public HttpStreamServer(BroadcastHub hub, IMetadataService metadataService, SongRequestController requestController, IProfileManager profileManager, INetworkDiscoveryService networkDiscovery, IThemeService? themeService = null, ILiveChatService? chatService = null, ISongReactionService? reactionService = null, ISongHistoryService? historyService = null, Scrim.Audio.StreamRelayService? relayService = null, IMultiStationManager? stationManager = null) {
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
            _relayService = relayService;
            _stationManager = stationManager;
            if (_relayService != null) {
                _relayService.StreamStatusUpdated += (_) => BroadcastBrandingUpdate();
                _relayService.StreamsChanged += BroadcastBrandingUpdate;
            }
            if (_stationManager != null) {
                _stationManager.StationsChanged += BroadcastBrandingUpdate;
                _stationManager.StationBroadcastingStateChanged += (_, _) => BroadcastBrandingUpdate();
            }
        }

        private PollState? _currentPoll;
        public PollState? CurrentPoll => _currentPoll;
        public event EventHandler<PollState?>? PollChanged;

        public void CreatePoll(string question, System.Collections.Generic.List<string> options) {
            var poll = new PollState {
                Question = question,
                Options = options.Select(o => new PollOption { Text = o, Votes = 0 }).ToList()
            };
            _currentPoll = poll;
            PollChanged?.Invoke(this, poll);
        }

        public void EndPoll() {
            _currentPoll = null;
            PollChanged?.Invoke(this, null);
        }

        public bool RecordPollVote(string pollId, int optionIndex) {
            if (_currentPoll == null || _currentPoll.Id != pollId) return false;
            if (optionIndex < 0 || optionIndex >= _currentPoll.Options.Count) return false;
            _currentPoll.Options[optionIndex].Votes++;
            PollChanged?.Invoke(this, _currentPoll);
            return true;
        }

        public string BuildPollJson(PollState? poll) {
            if (poll == null) return "{\"type\":\"poll_ended\"}";
            var optionsJson = string.Join(",", poll.Options.Select(o => $"{{\"text\":\"{EscapeJson(o.Text)}\",\"votes\":{o.Votes}}}"));
            return $"{{\"type\":\"poll_update\",\"poll\":{{\"id\":\"{EscapeJson(poll.Id)}\",\"question\":\"{EscapeJson(poll.Question)}\",\"totalVotes\":{poll.TotalVotes},\"options\":[{optionsJson}]}}}}";
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

        public bool IsListening => _listener != null && _listener.IsListening;

        public void Start(int port) {
            if (_listener != null && _listener.IsListening && (_bridgeListener == null || _bridgeListener.Server.IsBound)) return;
            Stop();

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
                    if (token.IsCancellationRequested || _bridgeListener == null) {
                        break;
                    }
                    try { await Task.Delay(50, token); } catch { break; }
                    continue;
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
                    client.ReceiveTimeout = 30000;
                    client.SendTimeout = 30000;
                    server.ReceiveTimeout = 30000;
                    server.SendTimeout = 30000;

                    await server.ConnectAsync(IPAddress.Loopback, internalPort, token);
                    using var clientStream = client.GetStream();
                    using var serverStream = server.GetStream();

                    var buffer = new byte[32768];
                    int totalRead = 0;
                    int headerEnd = -1;

                    using var headerTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                    headerTimeoutCts.CancelAfter(TimeSpan.FromSeconds(15));
                    var headerToken = headerTimeoutCts.Token;

                    while (totalRead < buffer.Length) {
                        int read = await clientStream.ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead), headerToken);
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
                    if (_listener == null || !_listener.IsListening) {
                        break;
                    }
                    context = await _listener.GetContextAsync();
                } catch (Exception) {
                    if (token.IsCancellationRequested || _listener == null || !_listener.IsListening) {
                        break;
                    }
                    try { await Task.Delay(50, token); } catch { break; }
                    continue;
                }

                // Process each request asynchronously so the accept loop is never blocked
                // and a failure in one client request can never crash the server!
                _ = Task.Run(() => {
                    try {
                        ProcessContext(context, token);
                    } catch (Exception) {
                        try {
                            context.Response.StatusCode = 500;
                            context.Response.Close();
                        } catch { }
                    }
                }, token);
            }
        }

        private void ProcessContext(HttpListenerContext context, CancellationToken token) {
            // Global CORS preflight OPTIONS handling
            if (context.Request.HttpMethod == "OPTIONS") {
                try {
                    context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                    context.Response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                    context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Accept, Authorization");
                    context.Response.StatusCode = 200;
                    context.Response.Close();
                } catch { }
                return;
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
            } else if (path == "/api/stations") {
                HandleStationsRequest(context);
            } else if (path == "/manifest.webmanifest" || path == "/manifest.json" || path == "/assets/manifest.webmanifest" || IsStationApiRoute(path, "manifest.webmanifest")) {
                HandleManifestRequest(context);
            } else if (path == "/api/events" || IsStationApiRoute(path, "events")) {
                _ = HandleSseClient(context, token);
            } else if (path == "/api/network") {
                HandleNetworkRequest(context);
            } else if (path == "/api/albumart" || IsStationApiRoute(path, "albumart")) {
                if (isRestricted) {
                    context.Response.StatusCode = 403;
                    context.Response.Close();
                } else {
                    HandleAlbumArtRequest(context);
                }
            } else if (path == "/api/metadata" || IsStationApiRoute(path, "metadata")) {
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
            } else if (path == "/api/branding" || IsStationApiRoute(path, "branding")) {
                HandleBrandingRequest(context);
            } else if (path == "/api/banner" || IsStationApiRoute(path, "banner")) {
                HandleBannerRequest(context);
            } else if (path == "/api/logo" || IsStationApiRoute(path, "logo")) {
                HandleLogoRequest(context);
            } else if ((path == "/api/requests" || IsStationApiRoute(path, "requests")) && context.Request.HttpMethod == "POST") {
                if (isRestricted) {
                    context.Response.StatusCode = 403;
                    context.Response.Close();
                } else {
                    HandleSongRequest(context);
                }
            } else if ((path == "/api/chat" || IsStationApiRoute(path, "chat")) && context.Request.HttpMethod == "POST") {
                if (isRestricted) {
                    context.Response.StatusCode = 403;
                    context.Response.Close();
                } else {
                    HandleChatPostRequest(context);
                }
            } else if ((path == "/api/chat" || IsStationApiRoute(path, "chat")) && context.Request.HttpMethod == "GET") {
                if (isRestricted) {
                    context.Response.ContentType = "application/json";
                    context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                    byte[] emptyChat = System.Text.Encoding.UTF8.GetBytes("{\"enabled\":false,\"messages\":[]}");
                    context.Response.OutputStream.Write(emptyChat, 0, emptyChat.Length);
                    context.Response.Close();
                } else {
                    HandleChatGetRequest(context);
                }
            } else if ((path == "/api/reactions/clear" || IsStationApiRoute(path, "reactions/clear")) && context.Request.HttpMethod == "POST") {
                HandleReactionClearRequest(context);
            } else if ((path == "/api/reactions" || IsStationApiRoute(path, "reactions")) && context.Request.HttpMethod == "POST") {
                if (isRestricted) {
                    context.Response.StatusCode = 403;
                    context.Response.Close();
                } else {
                    HandleReactionPostRequest(context);
                }
            } else if ((path == "/api/reactions" || IsStationApiRoute(path, "reactions")) && context.Request.HttpMethod == "GET") {
                HandleReactionGetRequest(context);
            } else if ((path == "/api/history/clear" || IsStationApiRoute(path, "history/clear")) && context.Request.HttpMethod == "POST") {
                HandleHistoryClearRequest(context);
            } else if ((path == "/api/history" || IsStationApiRoute(path, "history")) && context.Request.HttpMethod == "GET") {
                if (isRestricted) {
                    context.Response.ContentType = "application/json";
                    context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                    byte[] emptyHist = System.Text.Encoding.UTF8.GetBytes("[]");
                    context.Response.OutputStream.Write(emptyHist, 0, emptyHist.Length);
                    context.Response.Close();
                } else {
                    HandleHistoryGetRequest(context);
                }
            } else if (path == "/api/poll/vote" && context.Request.HttpMethod == "POST") {
                HandlePollVote(context);
            } else if (path == "/api/poll/current" && context.Request.HttpMethod == "GET") {
                HandleCurrentPoll(context);
            } else if (path == "/api/party/celebrate" && context.Request.HttpMethod == "POST") {
                HandlePartyCelebrateRequest(context);
            } else if (path == "/api/greenroom/chat" && context.Request.HttpMethod == "POST") {
                HandleGreenRoomPost(context);
            } else if (path == "/api/greenroom/chat" && context.Request.HttpMethod == "GET") {
                HandleGreenRoomGet(context);
            } else if (path == "/api/greenroom/auth" && context.Request.HttpMethod == "POST") {
                HandleGreenRoomAuth(context);
            } else if (path == "/greenroom" || path == "/greenroom/") {
                HandleGreenRoomWebPortal(context);
            } else if (path == "/api/status") {
                HandleStatusRequest(context);
            } else {
                // Serve Web Player assets: /, /index.html, /player.css, /player.js, /assets/*, etc.
                Scrim.Web.EmbeddedWebPlayer.ServeAsync(context);
            }
        }

        public StationPipeline? ResolveStationPipeline(HttpListenerContext context) {
            if (_stationManager == null) return null;

            string? queryStation = context.Request.QueryString["station"] ?? context.Request.QueryString["mount"];
            if (!string.IsNullOrWhiteSpace(queryStation)) {
                return _stationManager.GetStationByMount(queryStation) ?? _stationManager.GetStationById(queryStation);
            }

            string path = context.Request.Url?.AbsolutePath ?? "/";
            var parts = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2) {
                if (parts[0].Equals("api", StringComparison.OrdinalIgnoreCase)) {
                    string candidate = parts[1];
                    var pipeline = _stationManager.GetStationByMount(candidate) ?? _stationManager.GetStationById(candidate);
                    if (pipeline != null) return pipeline;
                } else {
                    string candidate = parts[0];
                    var pipeline = _stationManager.GetStationByMount(candidate) ?? _stationManager.GetStationById(candidate);
                    if (pipeline != null) return pipeline;
                }
            } else if (parts.Length == 1 && !parts[0].Equals("api", StringComparison.OrdinalIgnoreCase)) {
                string candidate = parts[0];
                var pipeline = _stationManager.GetStationByMount(candidate) ?? _stationManager.GetStationById(candidate);
                if (pipeline != null) return pipeline;
            }

            return _stationManager.GetActiveStation();
        }

        private static bool IsStationApiRoute(string path, string endpoint) {
            if (string.IsNullOrWhiteSpace(path)) return false;
            var parts = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3 && parts[0].Equals("api", StringComparison.OrdinalIgnoreCase)) {
                string candidate = parts[1];
                if (candidate.Equals("greenroom", StringComparison.OrdinalIgnoreCase) ||
                    candidate.Equals("poll", StringComparison.OrdinalIgnoreCase) ||
                    candidate.Equals("party", StringComparison.OrdinalIgnoreCase)) {
                    return false;
                }
                string sub = string.Join("/", parts.Skip(2));
                return sub.Equals(endpoint, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        private void HandleStationsRequest(HttpListenerContext context) {
            try {
                var response = context.Response;
                response.ContentType = "application/json; charset=utf-8";
                response.Headers.Add("Access-Control-Allow-Origin", "*");

                var stations = _stationManager != null
                    ? _stationManager.GetAllStations()
                    : new System.Collections.Generic.List<StationPipeline>();

                var items = stations.Select(s => {
                    string m = s.Config.MountPoint?.Trim().Trim('/') ?? "stream";
                    return $"{{\"id\":\"{EscapeJson(s.Config.Id)}\",\"name\":\"{EscapeJson(s.Config.Name)}\",\"stationName\":\"{EscapeJson(s.Config.StationName)}\",\"mount\":\"{EscapeJson(m)}\",\"streamUrl\":\"/{EscapeJson(m)}\",\"isLive\":{(s.IsLive ? "true" : "false")},\"listeners\":{s.Hub.ActiveClientCount},\"genre\":\"{EscapeJson(s.Config.GenreTag)}\",\"theme\":\"{EscapeJson(s.Config.WebTheme)}\",\"accentColor\":\"{EscapeJson(s.Config.AccentColor)}\",\"sourceType\":\"{s.Config.SourceType}\"}}";
                });

                string json = $"[{string.Join(",", items)}]";
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(json);
                response.ContentLength64 = bytes.Length;
                response.OutputStream.Write(bytes, 0, bytes.Length);
            } catch {
                context.Response.StatusCode = 500;
            } finally {
                context.Response.Close();
            }
        }

        private void HandleManifestRequest(HttpListenerContext context) {
            try {
                var response = context.Response;
                response.ContentType = "application/manifest+json; charset=utf-8";
                response.Headers.Add("Access-Control-Allow-Origin", "*");
                response.Headers.Add("Cache-Control", "no-cache, must-revalidate");

                var station = ResolveStationPipeline(context);
                var profile = _profileManager.CurrentProfile;
                var stConfig = station?.Config;

                string stationName = !string.IsNullOrWhiteSpace(stConfig?.StationName) ? stConfig.StationName : (!string.IsNullOrWhiteSpace(profile.StationName) ? profile.StationName : "Scrim Radio");
                string shortName = !string.IsNullOrWhiteSpace(stConfig?.Name) ? stConfig.Name : stationName;
                string mount = (stConfig?.MountPoint ?? GetNormalizedMountPoint()).Trim().Trim('/');
                string desc = !string.IsNullOrWhiteSpace(stConfig?.StationTagline) ? stConfig.StationTagline : (!string.IsNullOrWhiteSpace(profile.BroadcasterBio) ? profile.BroadcasterBio : "High-fidelity live audio broadcasting and interactive web player");
                string themeColor = !string.IsNullOrWhiteSpace(stConfig?.AccentColor) ? stConfig.AccentColor : (!string.IsNullOrWhiteSpace(profile.AccentColor) ? profile.AccentColor : "#00d2ff");

                bool isDefaultMount = string.Equals(mount, "stream", StringComparison.OrdinalIgnoreCase) || (profile.Stations.Count > 0 && profile.Stations[0].Id == stConfig?.Id);
                string startUrl = isDefaultMount ? "/" : $"/?station={Uri.EscapeDataString(mount)}";
                string appId = $"/pwa/{mount}";

                string logoUrl = !string.IsNullOrWhiteSpace(stConfig?.LogoUrl) ? stConfig.LogoUrl : GetEffectiveLogoUrl();
                string iconJson;
                if (!string.IsNullOrWhiteSpace(logoUrl) && !logoUrl.Contains("icon.svg")) {
                    iconJson = $@"
    {{
      ""src"": ""{EscapeJson(logoUrl)}"",
      ""sizes"": ""any"",
      ""type"": ""image/png"",
      ""purpose"": ""any maskable""
    }},
    {{
      ""src"": ""/assets/icon.svg"",
      ""sizes"": ""any"",
      ""type"": ""image/svg+xml"",
      ""purpose"": ""any""
    }},
    {{
      ""src"": ""/assets/icon-192.png"",
      ""sizes"": ""192x192"",
      ""type"": ""image/png"",
      ""purpose"": ""any""
    }},
    {{
      ""src"": ""/assets/icon-512.png"",
      ""sizes"": ""512x512"",
      ""type"": ""image/png"",
      ""purpose"": ""any""
    }}";
                } else {
                    iconJson = @"
    {
      ""src"": ""/assets/icon.svg"",
      ""sizes"": ""any"",
      ""type"": ""image/svg+xml"",
      ""purpose"": ""any""
    },
    {
      ""src"": ""/assets/icon.svg"",
      ""sizes"": ""any"",
      ""type"": ""image/svg+xml"",
      ""purpose"": ""maskable""
    },
    {
      ""src"": ""/assets/icon-192.png"",
      ""sizes"": ""192x192"",
      ""type"": ""image/png"",
      ""purpose"": ""any""
    },
    {
      ""src"": ""/assets/icon-512.png"",
      ""sizes"": ""512x512"",
      ""type"": ""image/png"",
      ""purpose"": ""any""
    }";
                }

                string manifest = $@"{{
  ""id"": ""{EscapeJson(appId)}"",
  ""name"": ""{EscapeJson(stationName)}"",
  ""short_name"": ""{EscapeJson(shortName)}"",
  ""description"": ""{EscapeJson(desc)}"",
  ""start_url"": ""{EscapeJson(startUrl)}"",
  ""scope"": ""/"",
  ""display"": ""standalone"",
  ""orientation"": ""any"",
  ""background_color"": ""#0d0f12"",
  ""theme_color"": ""{EscapeJson(themeColor)}"",
  ""categories"": [
    ""music"",
    ""entertainment"",
    ""audio""
  ],
  ""icons"": [{iconJson}
  ]
}}";

                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(manifest);
                response.ContentLength64 = bytes.Length;
                response.OutputStream.Write(bytes, 0, bytes.Length);
            } catch {
                context.Response.StatusCode = 500;
            } finally {
                context.Response.Close();
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
            if (p == $"/{mount}" || p == $"/{mount}.mp3" ||
                p == "/stream" || p == "/stream.mp3" ||
                p == "/live" || p == "/listen") {
                return true;
            }

            if (_stationManager != null) {
                foreach (var station in _stationManager.GetAllStations()) {
                    string sMount = (station.Config.MountPoint ?? "").Trim().Trim('/').ToLowerInvariant();
                    if (!string.IsNullOrEmpty(sMount) && (p == $"/{sMount}" || p == $"/{sMount}.mp3")) {
                        return true;
                    }
                }
            }

            return false;
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
                portStr = profile.UseReverseProxy ? "" : $":{profile.Port}";
                return c;
            }

            string rawHost = context.Request.Headers["X-Forwarded-Host"] ?? context.Request.Headers["Host"] ?? context.Request.Url?.Host ?? "localhost";
            rawHost = rawHost.Trim();
            if (rawHost.Contains(':')) {
                var parts = rawHost.Split(':');
                portStr = (profile.UseReverseProxy || parts[1] == "80" || parts[1] == "443") ? "" : $":{parts[1]}";
                return parts[0];
            } else {
                return rawHost;
            }
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
            var station = ResolveStationPipeline(context);
            var hub = station?.Hub ?? _hub;
            var currentProfile = _profileManager.CurrentProfile;
            var stationConfig = station?.Config;

            var response = context.Response;
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Cache-Control", "no-cache, no-store, must-revalidate");
            response.Headers.Add("Pragma", "no-cache");
            response.Headers.Add("Expires", "0");
            response.Headers.Add("Accept-Ranges", "none");
            response.Headers.Add("X-Powered-By", "Scrim");

            string stationName = stationConfig?.StationName ?? currentProfile.StationName ?? "Scrim Broadcast Station";
            int bitrate = stationConfig?.Bitrate ?? currentProfile.Bitrate;
            string hostName = stationConfig?.HostName ?? currentProfile.HostName;

            response.Headers.Add("icy-name", stationName);
            response.Headers.Add("icy-genre", stationConfig?.GenreTag ?? "Live Stream");
            response.Headers.Add("icy-br", bitrate.ToString());
            if (!string.IsNullOrWhiteSpace(hostName)) {
                response.Headers.Add("X-Scrim-Host", hostName);
            }
            string effectiveLogo = !string.IsNullOrWhiteSpace(stationConfig?.LogoUrl) ? stationConfig.LogoUrl : GetEffectiveLogoUrl();
            if (!string.IsNullOrWhiteSpace(effectiveLogo)) {
                response.Headers.Add("X-Scrim-Avatar", effectiveLogo);
            }

            if (!hub.IsBroadcasting) {
                response.StatusCode = 503;
                response.ContentType = "application/json";
                byte[] offlineBytes = System.Text.Encoding.UTF8.GetBytes("{\"error\":\"Station is offline\",\"isLive\":false}");
                response.ContentLength64 = offlineBytes.Length;
                response.OutputStream.Write(offlineBytes, 0, offlineBytes.Length);
                response.Close();
                return;
            }

            string format = (stationConfig?.AudioFormat ?? currentProfile.AudioFormat)?.ToLowerInvariant() ?? "mp3";
            response.ContentType = format switch {
                "opus" => "audio/ogg; codecs=opus",
                "aac" => "audio/aac",
                "flac" => "audio/flac",
                _ => "audio/mpeg"
            };
            response.SendChunked = true;

            string clientIp = context.Request.RemoteEndPoint?.Address.ToString() ?? "127.0.0.1";
            string clientUa = context.Request.UserAgent ?? "Web / Audio Player";
            string mount = context.Request.Url?.AbsolutePath.TrimStart('/') ?? (stationConfig?.MountPoint ?? "stream");
            using var clientCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            var client = hub.RegisterClient(clientIp, clientUa, mount, clientCts);
            try {
                using var stream = response.OutputStream;
                while (!clientCts.Token.IsCancellationRequested) {
                    var frame = await client.AudioChannel.Reader.ReadAsync(clientCts.Token);
                    await stream.WriteAsync(frame, clientCts.Token);
                    await stream.FlushAsync(clientCts.Token);
                }
            } catch {
            } finally {
                hub.UnregisterClient(client.ClientId);
                response.Close();
            }
        }

        private async Task HandleSseClient(HttpListenerContext context, CancellationToken token) {
            var response = context.Response;
            response.ContentType = "text/event-stream";
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Cache-Control", "no-cache");
            response.Headers.Add("Connection", "keep-alive");

            var station = ResolveStationPipeline(context);
            var chatSvc = station?.ChatService ?? _chatService;
            var reactionSvc = station?.ReactionService ?? _reactionService;
            var historySvc = station?.HistoryService ?? _historyService;
            var hub = station?.Hub ?? _hub;
            var reqCtrl = station?.RequestController ?? _requestController;
            var stationConfig = station?.Config;

            var writeLock = new SemaphoreSlim(1, 1);
            var immediateChannel = System.Threading.Channels.Channel.CreateUnbounded<string>();

            Action<ChatMessage> onMessage = (msg) => {
                string chatJson = $"{{\"type\":\"chat\",\"id\":\"{EscapeJson(msg.Id)}\",\"sender\":\"{EscapeJson(msg.Sender)}\",\"text\":\"{EscapeJson(msg.Text)}\",\"timestamp\":\"{msg.Timestamp:o}\",\"isHost\":{(msg.IsHost ? "true" : "false")},\"color\":\"{EscapeJson(msg.Color)}\"}}";
                immediateChannel.Writer.TryWrite($"data: {chatJson}\n\n");
            };

            Action<string> onMessageRemoved = (msgId) => {
                immediateChannel.Writer.TryWrite($"data: {{\"type\":\"chat_message_deleted\",\"id\":\"{EscapeJson(msgId)}\"}}\n\n");
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
                var currentHistory = historySvc.GetHistory(_profileManager.CurrentProfile.SongHistoryLimit);
                string historyJson = FormatHistoryJson(currentHistory, reactionSvc);
                immediateChannel.Writer.TryWrite($"data: {{\"type\":\"history_update\",\"history\":{historyJson}}}\n\n");
            };

            Action<ReactionCounts> onResetReactions = (counts) => {
                string resetJson = $"{{\"type\":\"reaction_reset\",\"counts\":{{\"thumbsUp\":{counts.ThumbsUp},\"thumbsDown\":{counts.ThumbsDown},\"heart\":{counts.Heart}}}}}";
                immediateChannel.Writer.TryWrite($"data: {resetJson}\n\n");
                var currentHistory = historySvc.GetHistory(_profileManager.CurrentProfile.SongHistoryLimit);
                string historyJson = FormatHistoryJson(currentHistory, reactionSvc);
                immediateChannel.Writer.TryWrite($"data: {{\"type\":\"history_update\",\"history\":{historyJson}}}\n\n");
            };

            Action<IReadOnlyList<SongHistoryItem>> onHistoryChanged = (items) => {
                string historyJson = FormatHistoryJson(items.Take(_profileManager.CurrentProfile.SongHistoryLimit).ToList(), reactionSvc);
                immediateChannel.Writer.TryWrite($"data: {{\"type\":\"history_update\",\"history\":{historyJson}}}\n\n");
            };

            Action onHistorySettings = () => {
                var currentHistory = historySvc.GetHistory(_profileManager.CurrentProfile.SongHistoryLimit);
                string historyJson = FormatHistoryJson(currentHistory, reactionSvc);
                immediateChannel.Writer.TryWrite($"data: {{\"type\":\"history_update\",\"history\":{historyJson}}}\n\n");
            };

            Action onBrandingSettings = () => {
                immediateChannel.Writer.TryWrite($"data: {BuildBrandingJson()}\n\n");
            };

            string BuildMetadataJson(MediaMetadata meta) {
                var activeRelay = _relayService?.ActivePassthroughStream;
                string title = meta.Title;
                string artist = meta.Artist;
                if (activeRelay != null && activeRelay.PassthroughTrackMetadata && !string.IsNullOrWhiteSpace(activeRelay.CurrentTrackTitle)) {
                    title = activeRelay.CurrentTrackTitle;
                    artist = !string.IsNullOrWhiteSpace(activeRelay.CurrentTrackArtist) ? activeRelay.CurrentTrackArtist : activeRelay.EffectiveDjName;
                }

                bool hasArt = (meta.AlbumArt != null && meta.AlbumArt.Length > 0) || !string.IsNullOrEmpty(meta.AlbumArtUrl);
                string artUrl = hasArt ? (string.IsNullOrEmpty(meta.AlbumArtUrl) ? "/api/albumart" : meta.AlbumArtUrl) : "";
                double durationSec = meta.Duration.TotalSeconds;
                double positionSec = meta.Position.TotalSeconds;
                bool isPlaying = meta.IsPlaying;
                return $"{{\"type\":\"metadata\",\"title\":\"{EscapeJson(title)}\",\"artist\":\"{EscapeJson(artist)}\",\"album\":\"{EscapeJson(meta.Album)}\",\"hasArt\":{(hasArt ? "true" : "false")},\"albumArtUrl\":\"{EscapeJson(artUrl)}\",\"duration\":{durationSec.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)},\"position\":{positionSec.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)},\"isPlaying\":{(isPlaying ? "true" : "false")}}}";
            }

            string BuildStatsJson(bool isLive) {
                var profile = _profileManager.CurrentProfile;
                string formatStr = (stationConfig?.AudioFormat ?? profile.AudioFormat)?.ToUpperInvariant() ?? "MP3";
                int bitrateVal = stationConfig?.Bitrate ?? profile.Bitrate;
                string streamMount = stationConfig?.MountPoint?.Trim().Trim('/') ?? GetNormalizedMountPoint();
                return $"{{\"type\":\"stats\",\"listeners\":{hub.ActiveClientCount},\"isLive\":{(isLive ? "true" : "false")},\"format\":\"{EscapeJson(formatStr)}\",\"bitrate\":{bitrateVal},\"streamUrl\":\"/{streamMount}\",\"isPrivate\":false}}";
            }

            string BuildQueueJson() {
                var requests = reqCtrl.GetLiveQueue().Select(r => $"{{\"query\":\"{EscapeJson(r.Query)}\",\"dedication\":\"{EscapeJson(r.Dedication)}\",\"status\":\"{EscapeJson(r.Status)}\"}}");
                return $"{{\"type\":\"queue\",\"requests\":[{string.Join(",", requests)}]}}";
            }

            string BuildBrandingJson() {
                var profile = _profileManager.CurrentProfile;
                var navLinksArray = string.Join(",", profile.CustomNavLinks.Select(l => $"{{\"label\":\"{EscapeJson(l.Label)}\",\"url\":\"{EscapeJson(l.Url)}\"}}"));
                string themeStr = stationConfig?.WebTheme ?? profile.WebTheme ?? "dark";
                string customThemeJson = GetCustomThemeJson(themeStr);
                string streamMount = stationConfig?.MountPoint?.Trim().Trim('/') ?? GetNormalizedMountPoint();
                string bannerUrl = GetEffectiveBannerUrl();
                string logoUrl = GetEffectiveLogoUrl();

                string stationName = stationConfig?.StationName ?? profile.StationName;
                string pageTitle = stationConfig?.PageTitle ?? profile.PageTitle;
                string showTitle = stationConfig?.ShowTitle ?? profile.ShowTitle;
                string hostName = stationConfig?.HostName ?? profile.HostName;
                string genreTag = stationConfig?.GenreTag ?? profile.GenreTag;
                string tagline = stationConfig?.StationTagline ?? profile.StationTagline;
                string accentColor = stationConfig?.AccentColor ?? profile.AccentColor;
                bool enableSongRequests = stationConfig?.EnableSongRequests ?? profile.EnableSongRequests;

                var activeRelay = _relayService?.ActivePassthroughStream;
                string effectiveHost = activeRelay != null && !string.IsNullOrWhiteSpace(activeRelay.EffectiveDjName) ? activeRelay.EffectiveDjName : hostName;
                string effectiveLogo = activeRelay != null && !string.IsNullOrWhiteSpace(activeRelay.EffectiveAvatarUrl) ? activeRelay.EffectiveAvatarUrl : logoUrl;

                string partyHubJson = activeRelay != null
                    ? $",\"isPartyHub\":true,\"guestDj\":{{\"name\":\"{EscapeJson(activeRelay.EffectiveDjName)}\",\"avatarUrl\":\"{EscapeJson(activeRelay.EffectiveAvatarUrl)}\",\"bio\":\"{EscapeJson(activeRelay.OriginBio)}\",\"discord\":\"{EscapeJson(activeRelay.OriginDiscord)}\",\"twitch\":\"{EscapeJson(activeRelay.OriginTwitch)}\",\"twitter\":\"{EscapeJson(activeRelay.OriginTwitter)}\"}},\"hostDj\":{{\"name\":\"{EscapeJson(hostName)}\",\"avatarUrl\":\"{EscapeJson(logoUrl)}\"}}"
                    : $",\"isPartyHub\":false,\"hostDj\":{{\"name\":\"{EscapeJson(hostName)}\",\"avatarUrl\":\"{EscapeJson(logoUrl)}\"}}";

                string reactorsJson = BuildVisualizerReactorsJson();
                string defaultVisMode = !string.IsNullOrWhiteSpace(profile.DefaultVisualizerMode) ? profile.DefaultVisualizerMode : "bars";

                return $"{{\"type\":\"branding\",\"stationName\":\"{EscapeJson(stationName)}\",\"pageTitle\":\"{EscapeJson(pageTitle)}\",\"showTitle\":\"{EscapeJson(showTitle)}\",\"hostName\":\"{EscapeJson(effectiveHost)}\",\"genreTag\":\"{EscapeJson(genreTag)}\",\"tagline\":\"{EscapeJson(tagline)}\",\"accentColor\":\"{EscapeJson(accentColor)}\",\"theme\":\"{EscapeJson(themeStr)}\",\"customThemeVariables\":{customThemeJson},\"enableDynamicBackdrop\":{(profile.EnableDynamicBackdrop ? "true" : "false")},\"enableSongRequests\":{(enableSongRequests ? "true" : "false")},\"broadcasterBio\":\"{EscapeJson(profile.BroadcasterBio)}\",\"socialDiscord\":\"{EscapeJson(profile.SocialDiscord)}\",\"socialTwitch\":\"{EscapeJson(profile.SocialTwitch)}\",\"socialTwitter\":\"{EscapeJson(profile.SocialTwitter)}\",\"scheduleDescription\":\"{EscapeJson(profile.ScheduleDescription)}\",\"logoUrl\":\"{EscapeJson(effectiveLogo)}\",\"bannerUrl\":\"{EscapeJson(bannerUrl)}\",\"navLinks\":\"{EscapeJson(profile.NavLinks)}\",\"customNavLinks\":[{navLinksArray}],\"streamUrl\":\"/{streamMount}\",\"isPrivate\":false,\"restrictToLocal\":{(profile.RestrictToLocalNetwork ? "true" : "false")}{partyHubJson},\"visualizerReactors\":{reactorsJson},\"defaultVisualizerMode\":\"{EscapeJson(defaultVisMode)}\"}}";
            }

            EventHandler<MediaMetadata> onMetadata = (_, meta) => {
                immediateChannel.Writer.TryWrite($"data: {BuildMetadataJson(meta)}\n\n");
            };

            EventHandler<bool> onBroadcastChanged = (_, isLive) => {
                immediateChannel.Writer.TryWrite($"data: {BuildStatsJson(isLive)}\n\n");
            };

            chatSvc.MessagePosted += onMessage;
            chatSvc.MessageRemoved += onMessageRemoved;
            chatSvc.ChatCleared += onClear;
            chatSvc.ChatStatusChanged += onStatus;
            chatSvc.NicknameAssigned += onAssign;
            chatSvc.UserBanned += onBanned;
            chatSvc.UserUnbanned += onUnbanned;
            reactionSvc.ReactionReceived += onReaction;
            reactionSvc.CountsReset += onResetReactions;
            historySvc.HistoryChanged += onHistoryChanged;
            _metadataService.MetadataChanged += onMetadata;
            hub.BroadcastingStateChanged += onBroadcastChanged;
            HistorySettingsChanged += onHistorySettings;
            BrandingSettingsChanged += onBrandingSettings;

            EventHandler<PollState?> onPoll = (_, poll) => {
                immediateChannel.Writer.TryWrite($"data: {BuildPollJson(poll)}\n\n");
            };
            PollChanged += onPoll;

            Action<string> onCelebration = (sender) => {
                immediateChannel.Writer.TryWrite($"data: {{\"type\":\"party_celebrate\",\"sender\":\"{EscapeJson(sender)}\"}}\n\n");
            };
            PartyCelebrationReceived += onCelebration;

            Action<ChatMessage> onGreenRoom = (msg) => {
                string grJson = $"{{\"type\":\"greenroom_chat\",\"id\":\"{EscapeJson(msg.Id)}\",\"sender\":\"{EscapeJson(msg.Sender)}\",\"text\":\"{EscapeJson(msg.Text)}\",\"timestamp\":\"{msg.Timestamp:o}\",\"isHost\":{(msg.IsHost ? "true" : "false")},\"color\":\"{EscapeJson(msg.Color)}\"}}";
                immediateChannel.Writer.TryWrite($"data: {grJson}\n\n");
            };
            GreenRoomMessagePosted += onGreenRoom;

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
                var initialCounts = reactionSvc.CurrentCounts;
                await SendEvent($"data: {{\"type\":\"reaction_init\",\"counts\":{{\"thumbsUp\":{initialCounts.ThumbsUp},\"thumbsDown\":{initialCounts.ThumbsDown},\"heart\":{initialCounts.Heart}}}}}\n\n");

                // Initial Song History Push
                var initialHistory = historySvc.GetHistory(_profileManager.CurrentProfile.SongHistoryLimit);
                await SendEvent($"data: {{\"type\":\"history_init\",\"history\":{FormatHistoryJson(initialHistory, reactionSvc)}}}\n\n");

                // Initial Chat State Push
                bool isChatEnabled = stationConfig?.EnableChat ?? _profileManager.CurrentProfile.EnableChat;
                var recentChat = chatSvc.GetRecentMessages().Select(m =>
                    $"{{\"id\":\"{EscapeJson(m.Id)}\",\"sender\":\"{EscapeJson(m.Sender)}\",\"text\":\"{EscapeJson(m.Text)}\",\"timestamp\":\"{m.Timestamp:o}\",\"isHost\":{(m.IsHost ? "true" : "false")},\"color\":\"{EscapeJson(m.Color)}\"}}"
                );
                await SendEvent($"data: {{\"type\":\"chat_init\",\"enabled\":{(isChatEnabled ? "true" : "false")},\"messages\":[{string.Join(",", recentChat)}]}}\n\n");

                // Initial Instant Stats, Metadata, Queue, and Branding Push
                await SendEvent($"data: {BuildStatsJson(hub.IsBroadcasting)}\n\n");
                await SendEvent($"data: {BuildMetadataJson(_metadataService.CurrentMetadata)}\n\n");
                await SendEvent($"data: {BuildQueueJson()}\n\n");
                await SendEvent($"data: {BuildBrandingJson()}\n\n");
                if (_currentPoll != null) {
                    await SendEvent($"data: {BuildPollJson(_currentPoll)}\n\n");
                }

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
                            await SendEvent($"data: {BuildStatsJson(hub.IsBroadcasting)}\n\n");
                            await SendEvent($"data: {BuildQueueJson()}\n\n");
                            await SendEvent($"data: {BuildBrandingJson()}\n\n");
                        }
                    } catch { }
                }, token);

                await Task.WhenAny(immediateTask, periodicTask);
            } catch {
            } finally {
                chatSvc.MessagePosted -= onMessage;
                chatSvc.MessageRemoved -= onMessageRemoved;
                chatSvc.ChatCleared -= onClear;
                chatSvc.ChatStatusChanged -= onStatus;
                chatSvc.NicknameAssigned -= onAssign;
                chatSvc.UserBanned -= onBanned;
                chatSvc.UserUnbanned -= onUnbanned;
                reactionSvc.ReactionReceived -= onReaction;
                reactionSvc.CountsReset -= onResetReactions;
                historySvc.HistoryChanged -= onHistoryChanged;
                _metadataService.MetadataChanged -= onMetadata;
                hub.BroadcastingStateChanged -= onBroadcastChanged;
                HistorySettingsChanged -= onHistorySettings;
                BrandingSettingsChanged -= onBrandingSettings;
                PollChanged -= onPoll;
                PartyCelebrationReceived -= onCelebration;
                GreenRoomMessagePosted -= onGreenRoom;
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
                var station = ResolveStationPipeline(context);
                var chatSvc = station?.ChatService ?? _chatService;
                bool isBanned = chatSvc.IsUserBanned(userHash);
                bool isEnabled = station?.Config.EnableChat ?? _profileManager.CurrentProfile.EnableChat;
                var blacklist = _profileManager.CurrentProfile.NicknameBlacklist;
                string blacklistJson = string.Join(",", blacklist.Select(b => $"\"{EscapeJson(b)}\""));

                string hostName = station?.Config.HostName ?? _profileManager.CurrentProfile.HostName;
                if (!string.IsNullOrWhiteSpace(hostName)) {
                    chatSvc.ReserveHostNickname(hostName);
                }

                // Return taken nicknames for other users so the client can ensure uniqueness
                var taken = chatSvc.GetClaimedNicknames(excludeUserId: userHash);
                string takenJson = string.Join(",", taken.Select(n => $"\"{EscapeJson(n)}\""));

                var messages = chatSvc.GetRecentMessages().Select(m => 
                    $"{{\"id\":\"{EscapeJson(m.Id)}\",\"sender\":\"{EscapeJson(m.Sender)}\",\"text\":\"{EscapeJson(m.Text)}\",\"timestamp\":\"{m.Timestamp:o}\",\"isHost\":{(m.IsHost ? "true" : "false")},\"color\":\"{EscapeJson(m.Color)}\"}}"
                );
                string json = $"{{\"enabled\":{(isEnabled ? "true" : "false")},\"isBanned\":{(isBanned ? "true" : "false")},\"blacklist\":[{blacklistJson}],\"takenNicknames\":[{takenJson}],\"messages\":[{string.Join(",", messages)}]}}";
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
                var station = ResolveStationPipeline(context);
                var chatSvc = station?.ChatService ?? _chatService;
                bool isEnabled = station?.Config.EnableChat ?? _profileManager.CurrentProfile.EnableChat;
                if (!isEnabled) {
                    context.Response.StatusCode = 403;
                    byte[] err = System.Text.Encoding.UTF8.GetBytes("{\"error\":\"Chat is currently disabled\"}");
                    context.Response.ContentType = "application/json; charset=utf-8";
                    context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                    context.Response.OutputStream.Write(err, 0, err.Length);
                    return;
                }

                using var reader = new StreamReader(context.Request.InputStream, System.Text.Encoding.UTF8);
                string body = reader.ReadToEnd();

                string text = "";
                string sender = "Anonymous";
                string rawClientId = "";

                if (!string.IsNullOrWhiteSpace(body)) {
                    try {
                        using var doc = System.Text.Json.JsonDocument.Parse(body);
                        var root = doc.RootElement;
                        if (root.TryGetProperty("text", out var tProp)) {
                            text = tProp.GetString() ?? "";
                        }
                        if (root.TryGetProperty("sender", out var sProp)) {
                            sender = sProp.GetString() ?? "Anonymous";
                        }
                        if (root.TryGetProperty("clientId", out var cProp)) {
                            rawClientId = cProp.GetString() ?? "";
                        }
                    } catch {
                        var textMatch = System.Text.RegularExpressions.Regex.Match(body, "\"text\"\\s*:\\s*\"(.*?)\"");
                        var senderMatch = System.Text.RegularExpressions.Regex.Match(body, "\"sender\"\\s*:\\s*\"(.*?)\"");
                        var clientMatch = System.Text.RegularExpressions.Regex.Match(body, "\"clientId\"\\s*:\\s*\"(.*?)\"");
                        if (textMatch.Success) text = textMatch.Groups[1].Value;
                        if (senderMatch.Success) sender = senderMatch.Groups[1].Value;
                        if (clientMatch.Success) rawClientId = clientMatch.Groups[1].Value;
                    }
                }

                if (string.IsNullOrWhiteSpace(rawClientId)) {
                    rawClientId = context.Request.QueryString["clientId"] ?? "";
                }

                string userHash = ComputeUserHash(context, rawClientId);

                if (chatSvc.IsUserBanned(userHash)) {
                    context.Response.StatusCode = 403;
                    byte[] err = System.Text.Encoding.UTF8.GetBytes("{\"error\":\"You have been banned from sending messages in this chat.\",\"isBanned\":true}");
                    context.Response.ContentType = "application/json; charset=utf-8";
                    context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                    context.Response.OutputStream.Write(err, 0, err.Length);
                    return;
                }

                if (!chatSvc.IsNicknameAllowed(sender, _profileManager.CurrentProfile.NicknameBlacklist)) {
                    context.Response.StatusCode = 400;
                    byte[] err = System.Text.Encoding.UTF8.GetBytes("{\"error\":\"This nickname is not permitted on this station\"}");
                    context.Response.ContentType = "application/json; charset=utf-8";
                    context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                    context.Response.OutputStream.Write(err, 0, err.Length);
                    return;
                }

                string hostName = station?.Config.HostName ?? _profileManager.CurrentProfile.HostName;
                if (!string.IsNullOrWhiteSpace(hostName)) {
                    chatSvc.ReserveHostNickname(hostName);
                }

                if (!chatSvc.TryClaimNickname(sender, userHash, isHost: false, out string claimError)) {
                    context.Response.StatusCode = 400;
                    byte[] err = System.Text.Encoding.UTF8.GetBytes($"{{\"error\":\"{EscapeJson(claimError)}\",\"nameTaken\":true}}");
                    context.Response.ContentType = "application/json; charset=utf-8";
                    context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                    context.Response.OutputStream.Write(err, 0, err.Length);
                    return;
                }

                ChatMessage? msg = null;
                if (!string.IsNullOrWhiteSpace(text)) {
                    msg = chatSvc.AddMessage(sender, text, isHost: false, userId: userHash);
                }

                context.Response.ContentType = "application/json; charset=utf-8";
                context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                context.Response.StatusCode = 200;

                string msgJson = msg != null 
                    ? $"{{\"id\":\"{EscapeJson(msg.Id)}\",\"sender\":\"{EscapeJson(msg.Sender)}\",\"text\":\"{EscapeJson(msg.Text)}\",\"timestamp\":\"{msg.Timestamp:o}\",\"isHost\":false,\"color\":\"{EscapeJson(msg.Color)}\"}}"
                    : "null";

                byte[] ok = System.Text.Encoding.UTF8.GetBytes($"{{\"success\":true,\"message\":{msgJson}}}");
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

                var station = ResolveStationPipeline(context);
                var reactionSvc = station?.ReactionService ?? _reactionService;

                var counts = reactionSvc.CurrentCounts;
                string? userReaction = reactionSvc.GetUserReaction(userHash);
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

                var station = ResolveStationPipeline(context);
                var reactionSvc = station?.ReactionService ?? _reactionService;

                var counts = reactionSvc.AddOrSwitchReaction(userHash, reactionType, out string? activeReaction);

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

                var station = ResolveStationPipeline(context);
                var reactionSvc = station?.ReactionService ?? _reactionService;

                if (!string.IsNullOrWhiteSpace(songKey)) {
                    reactionSvc.ResetSong(songKey);
                } else {
                    reactionSvc.Reset();
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
                var station = ResolveStationPipeline(context);
                var historySvc = station?.HistoryService ?? _historyService;
                var reactionSvc = station?.ReactionService ?? _reactionService;

                var items = historySvc.GetHistory(limit);
                string json = FormatHistoryJson(items, reactionSvc);
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
                var station = ResolveStationPipeline(context);
                var historySvc = station?.HistoryService ?? _historyService;
                historySvc.Clear();
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

        private string FormatHistoryJson(IReadOnlyList<SongHistoryItem> items, ISongReactionService? reactionService = null) {
            var reactionSvc = reactionService ?? _reactionService;
            var elements = items.Select(item => {
                string artUrl = !string.IsNullOrEmpty(item.AlbumArtUrl) ? item.AlbumArtUrl : "";
                bool hasArt = !string.IsNullOrEmpty(artUrl) || (item.AlbumArt != null && item.AlbumArt.Length > 0);
                var counts = reactionSvc.GetSongReactions(item.Title, item.Artist);
                return $"{{\"id\":\"{item.Id}\",\"title\":\"{EscapeJson(item.Title)}\",\"artist\":\"{EscapeJson(item.Artist)}\",\"album\":\"{EscapeJson(item.Album)}\",\"playedAt\":\"{EscapeJson(item.PlayedAtFormatted)}\",\"hasArt\":{(hasArt ? "true" : "false")},\"albumArtUrl\":\"{EscapeJson(artUrl)}\",\"thumbsUp\":{counts.ThumbsUp},\"thumbsDown\":{counts.ThumbsDown},\"heart\":{counts.Heart}}}";
            });
            return "[" + string.Join(",", elements) + "]";
        }

        private string GetEffectiveBannerUrl() {
            var profile = _profileManager.CurrentProfile;
            if (string.IsNullOrWhiteSpace(profile.BannerUrl)) {
                return "";
            }
            string val = profile.BannerUrl.Trim();
            if (val.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                val.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                val.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) {
                return val;
            }
            string? resolved = _profileManager.ResolveAssetPath(val);
            if (!string.IsNullOrWhiteSpace(resolved) && File.Exists(resolved)) {
                long lastModified = File.GetLastWriteTimeUtc(resolved).Ticks;
                return $"/api/banner?t={lastModified}";
            }
            return val;
        }

        private string GetEffectiveLogoUrl() {
            var profile = _profileManager.CurrentProfile;
            if (string.IsNullOrWhiteSpace(profile.LogoUrl)) {
                return "";
            }
            string val = profile.LogoUrl.Trim();
            if (val.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                val.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                val.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) {
                return val;
            }
            string? resolved = _profileManager.ResolveAssetPath(val);
            if (!string.IsNullOrWhiteSpace(resolved) && File.Exists(resolved)) {
                long lastModified = File.GetLastWriteTimeUtc(resolved).Ticks;
                return $"/api/logo?t={lastModified}";
            }
            return val;
        }

        private void HandleBannerRequest(HttpListenerContext context) {
            try {
                var response = context.Response;
                response.Headers.Add("Access-Control-Allow-Origin", "*");
                response.Headers.Add("Cache-Control", "no-cache, no-store, must-revalidate");

                var profile = _profileManager.CurrentProfile;
                string? resolved = _profileManager.ResolveAssetPath(profile.BannerUrl);
                if (!string.IsNullOrWhiteSpace(resolved) && File.Exists(resolved)) {
                    string ext = Path.GetExtension(resolved).ToLowerInvariant();
                    response.ContentType = ext switch {
                        ".png" => "image/png",
                        ".gif" => "image/gif",
                        ".webp" => "image/webp",
                        ".svg" => "image/svg+xml",
                        _ => "image/jpeg"
                    };
                    byte[] bytes = File.ReadAllBytes(resolved);
                    response.ContentLength64 = bytes.Length;
                    response.OutputStream.Write(bytes, 0, bytes.Length);
                } else {
                    response.StatusCode = 404;
                }
            } catch {
                context.Response.StatusCode = 500;
            } finally {
                context.Response.Close();
            }
        }

        private void HandleLogoRequest(HttpListenerContext context) {
            try {
                var response = context.Response;
                response.Headers.Add("Access-Control-Allow-Origin", "*");
                response.Headers.Add("Cache-Control", "no-cache, no-store, must-revalidate");

                var profile = _profileManager.CurrentProfile;
                string? resolved = _profileManager.ResolveAssetPath(profile.LogoUrl);
                if (!string.IsNullOrWhiteSpace(resolved) && File.Exists(resolved)) {
                    string ext = Path.GetExtension(resolved).ToLowerInvariant();
                    response.ContentType = ext switch {
                        ".png" => "image/png",
                        ".gif" => "image/gif",
                        ".webp" => "image/webp",
                        ".svg" => "image/svg+xml",
                        _ => "image/jpeg"
                    };
                    byte[] bytes = File.ReadAllBytes(resolved);
                    response.ContentLength64 = bytes.Length;
                    response.OutputStream.Write(bytes, 0, bytes.Length);
                } else {
                    response.StatusCode = 404;
                }
            } catch {
                context.Response.StatusCode = 500;
            } finally {
                context.Response.Close();
            }
        }

        private void HandleBrandingRequest(HttpListenerContext context) {
            try {
                var profile = _profileManager.CurrentProfile;
                var station = ResolveStationPipeline(context);
                var stationConfig = station?.Config;

                var navLinksArray = string.Join(",", profile.CustomNavLinks.Select(l => $"{{\"label\":\"{EscapeJson(l.Label)}\",\"url\":\"{EscapeJson(l.Url)}\"}}"));
                string themeStr = stationConfig?.WebTheme ?? profile.WebTheme ?? "dark";
                string customThemeJson = GetCustomThemeJson(themeStr);
                bool isRestricted = profile.RestrictToLocalNetwork && !IsLocalNetworkClient(context);
                string streamMount = stationConfig?.MountPoint?.Trim().Trim('/') ?? GetNormalizedMountPoint();
                string bannerUrl = GetEffectiveBannerUrl();
                string logoUrl = GetEffectiveLogoUrl();

                string stationName = stationConfig?.StationName ?? profile.StationName;
                string pageTitle = stationConfig?.PageTitle ?? profile.PageTitle;
                string showTitle = stationConfig?.ShowTitle ?? profile.ShowTitle;
                string hostName = stationConfig?.HostName ?? profile.HostName;
                string genreTag = stationConfig?.GenreTag ?? profile.GenreTag;
                string tagline = stationConfig?.StationTagline ?? profile.StationTagline;
                string accentColor = stationConfig?.AccentColor ?? profile.AccentColor;
                bool enableSongRequests = stationConfig?.EnableSongRequests ?? profile.EnableSongRequests;

                var activeRelay = _relayService?.ActivePassthroughStream;
                string effectiveHost = activeRelay != null && !string.IsNullOrWhiteSpace(activeRelay.EffectiveDjName) ? activeRelay.EffectiveDjName : hostName;
                string effectiveLogo = activeRelay != null && !string.IsNullOrWhiteSpace(activeRelay.EffectiveAvatarUrl) ? activeRelay.EffectiveAvatarUrl : logoUrl;

                string partyHubJson = activeRelay != null
                    ? $",\"isPartyHub\":true,\"guestDj\":{{\"name\":\"{EscapeJson(activeRelay.EffectiveDjName)}\",\"avatarUrl\":\"{EscapeJson(activeRelay.EffectiveAvatarUrl)}\",\"bio\":\"{EscapeJson(activeRelay.OriginBio)}\",\"discord\":\"{EscapeJson(activeRelay.OriginDiscord)}\",\"twitch\":\"{EscapeJson(activeRelay.OriginTwitch)}\",\"twitter\":\"{EscapeJson(activeRelay.OriginTwitter)}\"}},\"hostDj\":{{\"name\":\"{EscapeJson(hostName)}\",\"avatarUrl\":\"{EscapeJson(logoUrl)}\"}}"
                    : $",\"isPartyHub\":false,\"hostDj\":{{\"name\":\"{EscapeJson(hostName)}\",\"avatarUrl\":\"{EscapeJson(logoUrl)}\"}}";

                string reactorsJson = BuildVisualizerReactorsJson();
                string defaultVisMode = !string.IsNullOrWhiteSpace(profile.DefaultVisualizerMode) ? profile.DefaultVisualizerMode : "bars";

                string json = $"{{\"stationName\":\"{EscapeJson(stationName)}\",\"pageTitle\":\"{EscapeJson(pageTitle)}\",\"showTitle\":\"{EscapeJson(showTitle)}\",\"hostName\":\"{EscapeJson(effectiveHost)}\",\"genreTag\":\"{EscapeJson(genreTag)}\",\"tagline\":\"{EscapeJson(tagline)}\",\"accentColor\":\"{EscapeJson(accentColor)}\",\"theme\":\"{EscapeJson(themeStr)}\",\"customThemeVariables\":{customThemeJson},\"enableDynamicBackdrop\":{(profile.EnableDynamicBackdrop ? "true" : "false")},\"enableSongRequests\":{(enableSongRequests ? "true" : "false")},\"broadcasterBio\":\"{EscapeJson(profile.BroadcasterBio)}\",\"socialDiscord\":\"{EscapeJson(profile.SocialDiscord)}\",\"socialTwitch\":\"{EscapeJson(profile.SocialTwitch)}\",\"socialTwitter\":\"{EscapeJson(profile.SocialTwitter)}\",\"scheduleDescription\":\"{EscapeJson(profile.ScheduleDescription)}\",\"logoUrl\":\"{EscapeJson(effectiveLogo)}\",\"bannerUrl\":\"{EscapeJson(bannerUrl)}\",\"navLinks\":\"{EscapeJson(profile.NavLinks)}\",\"customNavLinks\":[{navLinksArray}],\"streamUrl\":\"/{streamMount}\",\"isPrivate\":{(isRestricted ? "true" : "false")},\"restrictToLocal\":{(profile.RestrictToLocalNetwork ? "true" : "false")}{partyHubJson},\"visualizerReactors\":{reactorsJson},\"defaultVisualizerMode\":\"{EscapeJson(defaultVisMode)}\"}}";
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

        private void HandlePartyCelebrateRequest(HttpListenerContext context) {
            try {
                using var reader = new StreamReader(context.Request.InputStream, System.Text.Encoding.UTF8);
                string body = reader.ReadToEnd();
                var senderMatch = System.Text.RegularExpressions.Regex.Match(body, "\"sender\"\\s*:\\s*\"(.*?)\"");
                string sender = senderMatch.Success ? senderMatch.Groups[1].Value : "Listener";
                BroadcastPartyCelebration(sender);
                context.Response.ContentType = "application/json";
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

        private readonly System.Collections.Concurrent.ConcurrentQueue<ChatMessage> _greenRoomMessages = new();
        public event Action<ChatMessage>? GreenRoomMessagePosted;

        public IReadOnlyList<ChatMessage> GetGreenRoomMessages() {
            return _greenRoomMessages.ToArray();
        }

        public void PostGreenRoomMessage(string sender, string text, bool isHost = false, string color = "#00d2ff") {
            var msg = new ChatMessage {
                Id = Guid.NewGuid().ToString("N"),
                Sender = sender,
                Text = text,
                Timestamp = DateTime.UtcNow,
                IsHost = isHost,
                Color = color
            };
            _greenRoomMessages.Enqueue(msg);
            while (_greenRoomMessages.Count > 50 && _greenRoomMessages.TryDequeue(out _)) { }
            GreenRoomMessagePosted?.Invoke(msg);
        }

        private bool VerifyGreenRoomAccess(HttpListenerContext context, string? bodyPasscode = null) {
            string expected = _profileManager.CurrentProfile.GreenRoomPasscode?.Trim() ?? "";
            if (string.IsNullOrEmpty(expected)) {
                return true; // No passcode configured, open access
            }

            // 1. Check custom header
            string? headerPass = context.Request.Headers["X-GreenRoom-Passcode"];
            if (!string.IsNullOrWhiteSpace(headerPass) && string.Equals(headerPass.Trim(), expected, StringComparison.Ordinal)) {
                return true;
            }

            // 2. Check query string (?pin= or ?passcode=)
            string? queryPin = context.Request.QueryString["pin"] ?? context.Request.QueryString["passcode"];
            if (!string.IsNullOrWhiteSpace(queryPin) && string.Equals(queryPin.Trim(), expected, StringComparison.Ordinal)) {
                return true;
            }

            // 3. Check JSON body passcode
            if (!string.IsNullOrWhiteSpace(bodyPasscode) && string.Equals(bodyPasscode.Trim(), expected, StringComparison.Ordinal)) {
                return true;
            }

            return false;
        }

        private void HandleGreenRoomAuth(HttpListenerContext context) {
            try {
                using var reader = new StreamReader(context.Request.InputStream, System.Text.Encoding.UTF8);
                string body = reader.ReadToEnd();
                var passMatch = System.Text.RegularExpressions.Regex.Match(body, "\"passcode\"\\s*:\\s*\"(.*?)\"");
                string provided = passMatch.Success ? passMatch.Groups[1].Value : "";
                bool ok = VerifyGreenRoomAccess(context, provided);

                context.Response.ContentType = "application/json";
                context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                if (ok) {
                    context.Response.StatusCode = 200;
                    byte[] res = System.Text.Encoding.UTF8.GetBytes(
                        $"{{\"success\":true,\"hostName\":\"{EscapeJson(_profileManager.CurrentProfile.HostName)}\",\"stationName\":\"{EscapeJson(_profileManager.CurrentProfile.StationName)}\"}}"
                    );
                    context.Response.OutputStream.Write(res, 0, res.Length);
                } else {
                    context.Response.StatusCode = 401;
                    byte[] res = System.Text.Encoding.UTF8.GetBytes("{\"success\":false,\"error\":\"Invalid passcode\"}");
                    context.Response.OutputStream.Write(res, 0, res.Length);
                }
            } catch {
                context.Response.StatusCode = 400;
            } finally {
                context.Response.Close();
            }
        }

        private void HandleGreenRoomGet(HttpListenerContext context) {
            try {
                if (!VerifyGreenRoomAccess(context)) {
                    context.Response.StatusCode = 401;
                    context.Response.ContentType = "application/json";
                    context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                    byte[] err = System.Text.Encoding.UTF8.GetBytes("{\"error\":\"Unauthorized: Invalid or missing Green Room passcode\"}");
                    context.Response.OutputStream.Write(err, 0, err.Length);
                    return;
                }

                var response = context.Response;
                response.ContentType = "application/json; charset=utf-8";
                response.Headers.Add("Access-Control-Allow-Origin", "*");
                var messages = _greenRoomMessages.Select(m =>
                    $"{{\"id\":\"{EscapeJson(m.Id)}\",\"sender\":\"{EscapeJson(m.Sender)}\",\"text\":\"{EscapeJson(m.Text)}\",\"timestamp\":\"{m.Timestamp:o}\",\"isHost\":{(m.IsHost ? "true" : "false")},\"color\":\"{EscapeJson(m.Color)}\"}}"
                );
                string json = $"{{\"messages\":[{string.Join(",", messages)}]}}";
                byte[] buffer = System.Text.Encoding.UTF8.GetBytes(json);
                response.ContentLength64 = buffer.Length;
                response.OutputStream.Write(buffer, 0, buffer.Length);
            } catch {
                context.Response.StatusCode = 500;
            } finally {
                context.Response.Close();
            }
        }

        private void HandleGreenRoomPost(HttpListenerContext context) {
            try {
                using var reader = new StreamReader(context.Request.InputStream, System.Text.Encoding.UTF8);
                string body = reader.ReadToEnd();
                var senderMatch = System.Text.RegularExpressions.Regex.Match(body, "\"sender\"\\s*:\\s*\"(.*?)\"");
                var textMatch = System.Text.RegularExpressions.Regex.Match(body, "\"text\"\\s*:\\s*\"(.*?)\"");
                var isHostMatch = System.Text.RegularExpressions.Regex.Match(body, "\"isHost\"\\s*:\\s*(true|false)");
                var colorMatch = System.Text.RegularExpressions.Regex.Match(body, "\"color\"\\s*:\\s*\"(.*?)\"");
                var passMatch = System.Text.RegularExpressions.Regex.Match(body, "\"passcode\"\\s*:\\s*\"(.*?)\"");

                string? bodyPass = passMatch.Success ? passMatch.Groups[1].Value : null;
                if (!VerifyGreenRoomAccess(context, bodyPass)) {
                    context.Response.StatusCode = 401;
                    context.Response.ContentType = "application/json";
                    context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                    byte[] err = System.Text.Encoding.UTF8.GetBytes("{\"error\":\"Unauthorized: Invalid or missing Green Room passcode\"}");
                    context.Response.OutputStream.Write(err, 0, err.Length);
                    return;
                }

                string sender = senderMatch.Success ? senderMatch.Groups[1].Value : "DJ Guest";
                string text = textMatch.Success ? textMatch.Groups[1].Value : "";
                bool isHost = isHostMatch.Success && isHostMatch.Groups[1].Value == "true";
                string color = colorMatch.Success && !string.IsNullOrWhiteSpace(colorMatch.Groups[1].Value) ? colorMatch.Groups[1].Value : "#00d2ff";

                if (!string.IsNullOrWhiteSpace(text)) {
                    PostGreenRoomMessage(sender, text, isHost, color);
                }

                context.Response.ContentType = "application/json";
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

        private void HandleGreenRoomWebPortal(HttpListenerContext context) {
            try {
                var response = context.Response;
                response.ContentType = "text/html; charset=utf-8";
                response.Headers.Add("Access-Control-Allow-Origin", "*");
                string html = BuildGreenRoomPortalHtml();
                byte[] buffer = System.Text.Encoding.UTF8.GetBytes(html);
                response.ContentLength64 = buffer.Length;
                response.OutputStream.Write(buffer, 0, buffer.Length);
            } catch {
                context.Response.StatusCode = 500;
            } finally {
                context.Response.Close();
            }
        }

        private string BuildGreenRoomPortalHtml() {
            var profile = _profileManager.CurrentProfile;
            string stationName = EscapeHtml(profile.StationName);
            string hostName = EscapeHtml(profile.HostName);
            return $$"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no" />
    <title>{{stationName}} — Backstage DJ Green Room</title>
    <link rel="preconnect" href="https://fonts.googleapis.com" />
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin />
    <link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700;800&family=JetBrains+Mono:wght@500;700&display=swap" rel="stylesheet" />
    <style>
        * { box-sizing: border-box; margin: 0; padding: 0; }
        body {
            background: #07090e;
            color: #f1f5f9;
            font-family: 'Inter', -apple-system, BlinkMacSystemFont, sans-serif;
            min-height: 100vh;
            height: 100vh;
            overflow: hidden;
            display: flex;
            flex-direction: column;
            justify-content: center;
            align-items: center;
            padding: 12px;
        }
        .gr-card {
            width: 100%;
            background: #0f131c;
            border: 1px solid #1e2638;
            border-radius: 14px;
            box-shadow: 0 20px 40px rgba(0, 0, 0, 0.6);
            overflow: hidden;
            display: flex;
            flex-direction: column;
        }
        #loginCard {
            max-width: 480px;
            margin: auto;
        }
        .gr-header {
            background: #141a27;
            border-bottom: 1px solid #202a3d;
            padding: 14px 16px;
            display: flex;
            justify-content: space-between;
            align-items: center;
        }
        .gr-title {
            font-size: 13px;
            font-weight: 800;
            color: #c084fc;
            display: flex;
            align-items: center;
            gap: 8px;
            letter-spacing: 0.5px;
        }
        .gr-pill {
            font-size: 9px;
            font-weight: 800;
            background: rgba(192, 132, 252, 0.15);
            border: 1px solid rgba(192, 132, 252, 0.35);
            color: #c084fc;
            padding: 2px 7px;
            border-radius: 999px;
            letter-spacing: 0.5px;
        }
        .gr-body { padding: 16px; }
        .gr-label {
            font-size: 11px;
            font-weight: 600;
            color: #94a3b8;
            margin-bottom: 6px;
            display: block;
        }
        .gr-input {
            width: 100%;
            background: #090c13;
            border: 1px solid #202738;
            border-radius: 8px;
            color: #fff;
            padding: 10px 12px;
            font-size: 13px;
            font-family: inherit;
            outline: none;
            transition: border-color 0.2s;
            margin-bottom: 12px;
        }
        .gr-input:focus { border-color: #a855f7; }
        .gr-btn-primary {
            width: 100%;
            background: linear-gradient(135deg, #a855f7 0%, #7c3aed 100%);
            border: none;
            color: #fff;
            font-weight: 700;
            font-size: 13px;
            padding: 11px 16px;
            border-radius: 8px;
            cursor: pointer;
            box-shadow: 0 4px 14px rgba(168, 85, 247, 0.35);
            transition: opacity 0.2s, transform 0.1s;
        }
        .gr-btn-primary:active { transform: scale(0.98); opacity: 0.9; }
        #backstagePanel {
            display: none;
            width: 100%;
            max-width: min(1200px, calc(100vw - 24px));
            height: calc(100vh - 24px);
            margin: auto;
        }
        .gr-status-strip {
            background: #090c12;
            border-bottom: 1px solid #1a2233;
            padding: 8px 14px;
            font-size: 11px;
            display: flex;
            justify-content: space-between;
            align-items: center;
        }
        .gr-now-playing {
            color: #38bdf8;
            white-space: nowrap;
            overflow: hidden;
            text-overflow: ellipsis;
            flex: 1;
            min-width: 0;
            margin-right: 12px;
            font-weight: 600;
        }
        .gr-audio-btn {
            background: #1c2436;
            border: 1px solid #2b374f;
            color: #38bdf8;
            font-size: 11px;
            padding: 3px 8px;
            border-radius: 6px;
            cursor: pointer;
            flex-shrink: 0;
        }
        .gr-chat-feed {
            flex: 1;
            overflow-y: auto;
            padding: 12px 14px;
            display: flex;
            flex-direction: column;
            gap: 6px;
            background: #080a10;
        }
        .gr-msg {
            font-size: 12px;
            line-height: 1.4;
            display: flex;
            gap: 6px;
            align-items: baseline;
        }
        .gr-msg-time {
            font-size: 9.5px;
            color: #64748b;
            font-family: 'JetBrains Mono', monospace;
        }
        .gr-msg-sender { font-weight: 700; }
        .gr-msg-text { color: #e2e8f0; word-break: break-word; }
        .gr-chips {
            padding: 8px 12px;
            display: flex;
            gap: 6px;
            overflow-x: auto;
            background: #0c1017;
            border-top: 1px solid #1a2336;
        }
        .gr-chip {
            background: #151c2a;
            border: 1px solid #243048;
            color: #cbd5e1;
            font-size: 10.5px;
            padding: 4px 9px;
            border-radius: 999px;
            white-space: nowrap;
            cursor: pointer;
            user-select: none;
            transition: background 0.15s;
        }
        .gr-chip:hover { background: #222d42; color: #fff; }
        .gr-input-bar {
            padding: 10px 12px;
            background: #0f141f;
            border-top: 1px solid #1e2638;
            display: flex;
            gap: 8px;
        }
        .gr-input-bar input {
            margin-bottom: 0;
            font-size: 12px;
            padding: 9px 12px;
        }
        .gr-input-bar button {
            width: auto;
            padding: 0 16px;
            font-size: 12px;
        }
        @media (min-width: 768px) {
            body { padding: 16px; }
            #backstagePanel {
                height: calc(100vh - 32px);
                max-width: min(1200px, calc(100vw - 32px));
                border-radius: 16px;
            }
            .gr-chips {
                flex-wrap: wrap;
                padding: 10px 16px;
                gap: 8px;
            }
            .gr-chat-feed {
                padding: 16px 20px;
                gap: 8px;
            }
            .gr-msg {
                font-size: 13px;
            }
            .gr-input-bar {
                padding: 12px 16px;
            }
            .gr-input-bar input {
                font-size: 13px;
            }
            .gr-input-bar button {
                font-size: 13px;
            }
        }
    </style>
</head>
<body>
    <div class="gr-card" id="loginCard">
        <div class="gr-header">
            <div class="gr-title">🎙️ SCRIM BACKSTAGE</div>
            <div class="gr-pill">OFF-AIR GREEN ROOM</div>
        </div>
        <div class="gr-body">
            <div style="font-size: 12px; color: #94a3b8; margin-bottom: 14px; line-height: 1.4;">
                Welcome to <strong>{{stationName}}</strong> (Host: {{hostName}}). Enter your DJ moniker and the station's Green Room passcode to join the backstage performer intercom.
            </div>
            <label class="gr-label">Your DJ / Performer Name</label>
            <input type="text" class="gr-input" id="djNameInput" placeholder="e.g. DJ Shadow, MC Nova" autofocus />

            <label class="gr-label">Green Room Passcode</label>
            <input type="password" class="gr-input" id="passcodeInput" placeholder="Enter Backstage PIN" />

            <div id="authError" style="color: #f87171; font-size: 11px; margin-bottom: 10px; display: none;"></div>

            <button class="gr-btn-primary" id="enterBtn">Enter Green Room ⚡</button>
        </div>
    </div>

    <div class="gr-card" id="backstagePanel">
        <div class="gr-header">
            <div class="gr-title">
                <span>🎙️</span>
                <span id="headerStation">{{stationName}}</span>
                <span class="gr-pill" id="headerDjName">DJ</span>
            </div>
            <button class="gr-chip" id="leaveBtn" style="padding: 2px 8px; font-size: 10px;">Leave</button>
        </div>
        <div class="gr-status-strip">
            <div class="gr-now-playing" id="nowPlaying">🎵 Live Broadcast</div>
            <button class="gr-audio-btn" id="audioToggleBtn">▶ Monitor Stream</button>
            <audio id="liveAudio" preload="none"></audio>
        </div>
        <div class="gr-chat-feed" id="chatFeed">
            <div style="color: #64748b; font-size: 11px; text-align: center; margin: auto; font-style: italic;">
                Connected to Green Room. Coordinate track keys, BPMs, cues &amp; hand-offs with the host.
            </div>
        </div>
        <div class="gr-chips">
            <div class="gr-chip" data-cue="⚡ Ready to drop in 30s">⚡ Ready in 30s</div>
            <div class="gr-chip" data-cue="🎛️ Beatmatching next track">🎛️ Beatmatching</div>
            <div class="gr-chip" data-cue="🎹 Key: Am | BPM: 128">🎹 Key &amp; BPM</div>
            <div class="gr-chip" data-cue="🎧 Fade me in!">🎧 Fade me in!</div>
            <div class="gr-chip" data-cue="🔄 Taking back decks">🔄 Taking decks</div>
            <div class="gr-chip" data-cue="🙌 Awesome set!">🙌 Great set!</div>
        </div>
        <div class="gr-input-bar">
            <input type="text" class="gr-input" id="msgInput" placeholder="Private note to DJs... (Enter to send)" autocomplete="off" />
            <button class="gr-btn-primary" id="sendBtn">Send 💬</button>
        </div>
    </div>

    <script>
        (function () {
            const urlParams = new URLSearchParams(window.location.search);
            const queryPin = urlParams.get('pin') || urlParams.get('passcode') || '';
            const queryName = urlParams.get('name') || urlParams.get('dj') || '';
            const autoLogin = urlParams.get('auto') === '1' || urlParams.get('auto') === 'true' || urlParams.has('autologin');
            let isHostUser = urlParams.get('host') === '1' || urlParams.get('host') === 'true';
            const passInput = document.getElementById('passcodeInput');
            const nameInput = document.getElementById('djNameInput');
            const authError = document.getElementById('authError');
            const loginCard = document.getElementById('loginCard');
            const backstagePanel = document.getElementById('backstagePanel');
            const chatFeed = document.getElementById('chatFeed');
            const msgInput = document.getElementById('msgInput');
            const sendBtn = document.getElementById('sendBtn');
            const headerDjName = document.getElementById('headerDjName');
            const nowPlaying = document.getElementById('nowPlaying');
            const audioToggleBtn = document.getElementById('audioToggleBtn');
            const liveAudio = document.getElementById('liveAudio');

            if (queryPin) passInput.value = queryPin;
            if (queryName) {
                nameInput.value = queryName;
            } else {
                nameInput.value = localStorage.getItem('scrim_gr_name') || '';
            }
            if (!queryPin && localStorage.getItem('scrim_gr_pin')) {
                passInput.value = localStorage.getItem('scrim_gr_pin');
            }

            let currentPasscode = '';
            let currentDjName = '';
            let sseSource = null;
            let pollTimer = null;
            const seenMessageIds = new Set();

            async function attemptLogin() {
                const name = nameInput.value.trim();
                const pass = passInput.value.trim();
                if (!name) {
                    showError('Please enter your DJ / Performer name.');
                    return;
                }
                authError.style.display = 'none';

                try {
                    const res = await fetch('/api/greenroom/auth', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({ passcode: pass })
                    });
                    if (res.ok) {
                        const data = await res.json().catch(() => ({}));
                        currentPasscode = pass;
                        currentDjName = name;
                        if (data && data.hostName && (name.toLowerCase().includes(data.hostName.toLowerCase()) || name.toLowerCase().includes('host'))) {
                            isHostUser = true;
                        }
                        localStorage.setItem('scrim_gr_name', name);
                        localStorage.setItem('scrim_gr_pin', pass);
                        enterBackstage();
                    } else {
                        showError('Invalid Green Room Passcode. Check with the host.');
                    }
                } catch (e) {
                    showError('Network error connecting to station.');
                }
            }

            function showError(msg) {
                authError.textContent = msg;
                authError.style.display = 'block';
            }

            function enterBackstage() {
                loginCard.style.display = 'none';
                backstagePanel.style.display = 'flex';
                headerDjName.textContent = currentDjName + (isHostUser ? ' 👑' : '');
                liveAudio.src = '/stream';

                loadHistory();
                connectSse();
                pollTimer = setInterval(loadHistory, 3500);
                pollMetadata();
                setInterval(pollMetadata, 4000);
                msgInput.focus();
            }

            function leaveBackstage() {
                if (sseSource) sseSource.close();
                if (pollTimer) clearInterval(pollTimer);
                liveAudio.pause();
                liveAudio.src = '';
                backstagePanel.style.display = 'none';
                loginCard.style.display = 'flex';
            }

            async function loadHistory() {
                try {
                    const res = await fetch('/api/greenroom/chat', {
                        headers: { 'X-GreenRoom-Passcode': currentPasscode }
                    });
                    if (res.ok) {
                        const data = await res.json();
                        if (data && Array.isArray(data.messages)) {
                            data.messages.forEach(appendMessage);
                        }
                    }
                } catch (e) {}
            }

            async function pollMetadata() {
                try {
                    const res = await fetch('/api/metadata');
                    if (res.ok) {
                        const data = await res.json();
                        if (data && data.title) {
                            nowPlaying.textContent = '🎵 ' + data.title + (data.artist ? ' — ' + data.artist : '');
                        }
                    }
                } catch (e) {}
            }

            function connectSse() {
                try {
                    sseSource = new EventSource('/events');
                    sseSource.onmessage = function (ev) {
                        try {
                            const data = JSON.parse(ev.data);
                            if (data && data.type === 'greenroom_chat') {
                                appendMessage(data);
                            }
                        } catch (e) {}
                    };
                } catch (e) {}
            }

            function appendMessage(msg) {
                if (!msg || !msg.id || seenMessageIds.has(msg.id)) return;
                seenMessageIds.add(msg.id);

                const div = document.createElement('div');
                div.className = 'gr-msg';
                const time = msg.timestamp ? new Date(msg.timestamp).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) : '';
                const senderColor = msg.isHost ? '#c084fc' : (msg.color || '#38bdf8');
                const hostBadge = msg.isHost ? ' <span style="font-size: 8px; background: rgba(192,132,252,0.2); padding: 1px 4px; border-radius: 3px;">HOST</span>' : '';

                div.innerHTML = '<span class="gr-msg-time">' + time + '</span> ' +
                    '<span class="gr-msg-sender" style="color: ' + senderColor + ';">' + escapeHtml(msg.sender) + hostBadge + ':</span> ' +
                    '<span class="gr-msg-text">' + escapeHtml(msg.text) + '</span>';
                chatFeed.appendChild(div);
                chatFeed.scrollTop = chatFeed.scrollHeight;
            }

            function escapeHtml(str) {
                if (!str) return '';
                return str.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
            }

            async function sendMessage(text) {
                const t = (text || msgInput.value).trim();
                if (!t) return;
                msgInput.value = '';

                try {
                    await fetch('/api/greenroom/chat', {
                        method: 'POST',
                        headers: {
                            'Content-Type': 'application/json',
                            'X-GreenRoom-Passcode': currentPasscode
                        },
                        body: JSON.stringify({
                            sender: currentDjName,
                            text: t,
                            passcode: currentPasscode,
                            isHost: isHostUser,
                            color: isHostUser ? '#c084fc' : '#38bdf8'
                        })
                    });
                    loadHistory();
                } catch (e) {}
            }

            document.getElementById('enterBtn').addEventListener('click', attemptLogin);
            passInput.addEventListener('keydown', (e) => { if (e.key === 'Enter') attemptLogin(); });
            nameInput.addEventListener('keydown', (e) => { if (e.key === 'Enter') passInput.focus(); });
            document.getElementById('leaveBtn').addEventListener('click', leaveBackstage);

            sendBtn.addEventListener('click', () => sendMessage());
            msgInput.addEventListener('keydown', (e) => { if (e.key === 'Enter') sendMessage(); });

            document.querySelectorAll('.gr-chip[data-cue]').forEach(chip => {
                chip.addEventListener('click', function () {
                    sendMessage(this.dataset.cue);
                });
            });

            audioToggleBtn.addEventListener('click', function () {
                if (liveAudio.paused) {
                    liveAudio.play().then(() => {
                        audioToggleBtn.textContent = '⏸ Pause Monitor';
                    }).catch(() => {});
                } else {
                    liveAudio.pause();
                    audioToggleBtn.textContent = '▶ Monitor Stream';
                }
            });

            if (nameInput.value && passInput.value) {
                attemptLogin();
            }
        })();
    </script>
</body>
</html>
""";
        }

        private static string EscapeHtml(string? input) {
            if (string.IsNullOrEmpty(input)) return "";
            return System.Net.WebUtility.HtmlEncode(input);
        }

        private string BuildVisualizerReactorsJson() {
            var profile = _profileManager.CurrentProfile;
            var reactors = profile.VisualizerReactors;
            if (reactors == null || reactors.Count == 0) {
                return "[{\"id\":\"bars\",\"label\":\"Bars\",\"emoji\":\"📊\",\"baseMode\":\"bars\",\"isDefault\":true},{\"id\":\"wave\",\"label\":\"Wave\",\"emoji\":\"📈\",\"baseMode\":\"wave\",\"isDefault\":false},{\"id\":\"spectrum\",\"label\":\"Spectrum\",\"emoji\":\"🌈\",\"baseMode\":\"spectrum\",\"isDefault\":false},{\"id\":\"pulse\",\"label\":\"Pulse\",\"emoji\":\"✨\",\"baseMode\":\"pulse\",\"isDefault\":false}]";
            }
            var enabled = reactors.Where(r => r.IsEnabled).Select(r =>
                $"{{\"id\":\"{EscapeJson(r.Id)}\",\"label\":\"{EscapeJson(r.Label)}\",\"emoji\":\"{EscapeJson(r.Emoji)}\",\"baseMode\":\"{EscapeJson(r.BaseMode)}\",\"isDefault\":{(r.IsDefault ? "true" : "false")}}}"
            );
            return $"[{string.Join(",", enabled)}]";
        }

        private void HandleSongRequest(HttpListenerContext context) {
            try {
                context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                var station = ResolveStationPipeline(context);
                bool isRequestsEnabled = station?.Config.EnableSongRequests ?? _profileManager.CurrentProfile.EnableSongRequests;

                if (!isRequestsEnabled) {
                    context.Response.StatusCode = 403;
                    context.Response.ContentType = "application/json";
                    byte[] errBytes = System.Text.Encoding.UTF8.GetBytes("{\"error\":\"Song requests are currently paused by the station host.\",\"enabled\":false}");
                    context.Response.OutputStream.Write(errBytes, 0, errBytes.Length);
                    return;
                }

                using var reader = new StreamReader(context.Request.InputStream, System.Text.Encoding.UTF8);
                string body = reader.ReadToEnd();
                var queryMatch = System.Text.RegularExpressions.Regex.Match(body, "\"query\"\\s*:\\s*\"(.*?)\"");
                var dedicationMatch = System.Text.RegularExpressions.Regex.Match(body, "\"dedication\"\\s*:\\s*\"(.*?)\"");

                string query = queryMatch.Success ? queryMatch.Groups[1].Value : "";
                string dedication = dedicationMatch.Success ? dedicationMatch.Groups[1].Value : "";

                query = System.Text.RegularExpressions.Regex.Unescape(query);
                dedication = System.Text.RegularExpressions.Regex.Unescape(dedication);

                if (!string.IsNullOrWhiteSpace(query)) {
                    var reqCtrl = station?.RequestController ?? _requestController;
                    reqCtrl.SubmitRequest(query, dedication);
                }
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
                response.Headers.Add("Cache-Control", "no-cache, no-store, must-revalidate");
                response.Headers.Add("Pragma", "no-cache");

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

        private void HandlePollVote(HttpListenerContext context) {
            try {
                using var reader = new StreamReader(context.Request.InputStream, System.Text.Encoding.UTF8);
                string body = reader.ReadToEnd();
                var pollIdMatch = System.Text.RegularExpressions.Regex.Match(body, "\"pollId\"\\s*:\\s*\"(.*?)\"");
                var optIdxMatch = System.Text.RegularExpressions.Regex.Match(body, "\"optionIndex\"\\s*:\\s*(\\d+)");
                if (pollIdMatch.Success && optIdxMatch.Success && int.TryParse(optIdxMatch.Groups[1].Value, out int optIdx)) {
                    RecordPollVote(pollIdMatch.Groups[1].Value, optIdx);
                }
                context.Response.StatusCode = 200;
                context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
            } catch {
                context.Response.StatusCode = 500;
            } finally {
                context.Response.Close();
            }
        }

        private void HandleCurrentPoll(HttpListenerContext context) {
            try {
                string json = BuildPollJson(_currentPoll);
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

        private string EscapeJson(string? value) {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Replace("\\", "\\\\")
                        .Replace("\"", "\\\"")
                        .Replace("\r", "\\r")
                        .Replace("\n", "\\n")
                        .Replace("\t", "\\t");
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

    public class PollOption {
        public string Text { get; set; } = "";
        public int Votes { get; set; } = 0;
    }

    public class PollState {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Question { get; set; } = "";
        public System.Collections.Generic.List<PollOption> Options { get; set; } = new();
        public int TotalVotes => Options.Sum(o => o.Votes);
    }
}

