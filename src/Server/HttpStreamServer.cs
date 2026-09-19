using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
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
        private HttpListener? _listener;
        private CancellationTokenSource? _cts;

        public HttpStreamServer(BroadcastHub hub, IMetadataService metadataService, SongRequestController requestController, IProfileManager profileManager, INetworkDiscoveryService networkDiscovery) {
            _hub = hub;
            _metadataService = metadataService;
            _requestController = requestController;
            _profileManager = profileManager;
            _networkDiscovery = networkDiscovery;
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
            if (_listener != null && _listener.IsListening) return;

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

                    foreach (var ip in _networkDiscovery.GetAllLocalIps()) {
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

                    // If network / wildcard prefixes failed (e.g. Access is Denied without admin rights),
                    // fall back IMMEDIATELY to a clean loopback-only listener on the SAME targetPort!
                    if (addedNetworkPrefixes) {
                        var loopbackListener = new HttpListener();
                        loopbackListener.Prefixes.Add($"http://localhost:{targetPort}/");
                        loopbackListener.Prefixes.Add($"http://127.0.0.1:{targetPort}/");
                        try {
                            loopbackListener.Start();
                            _listener = loopbackListener;
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
                        }
                    }

                    // Only advance port if even loopback failed (truly occupied by another application)
                    targetPort++;
                }
            }

            if (_listener == null || !_listener.IsListening) return;

            _cts = new CancellationTokenSource();
            Task.Run(() => AcceptLoop(_cts.Token));
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

                if (path == "/stream" || path == "/stream.mp3" || path == "/live" || path == "/listen" || (path == "/" && isMediaPlayer)) {
                    _ = HandleStreamClient(context, token);
                } else if (path == "/listen.m3u" || path == "/playlist.m3u") {
                    HandleM3uRequest(context);
                } else if (path == "/listen.pls") {
                    HandlePlsRequest(context);
                } else if (path == "/api/events") {
                    _ = HandleSseClient(context, token);
                } else if (path == "/api/network") {
                    HandleNetworkRequest(context);
                } else if (path == "/api/albumart") {
                    HandleAlbumArtRequest(context);
                } else if (path == "/api/metadata") {
                    HandleMetadataRequest(context);
                } else if (path == "/api/branding") {
                    HandleBrandingRequest(context);
                } else if (path == "/api/requests" && context.Request.HttpMethod == "POST") {
                    HandleSongRequest(context);
                } else if (path == "/api/status") {
                    HandleStatusRequest(context);
                } else {
                    // Serve Web Player assets: /, /index.html, /player.css, /player.js, /assets/*, etc.
                    Scrim.Web.EmbeddedWebPlayer.ServeAsync(context);
                }
            }
        }

        private void HandleM3uRequest(HttpListenerContext context) {
            var response = context.Response;
            response.ContentType = "audio/x-mpegurl; charset=utf-8";
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            string host = context.Request.Url?.Host ?? "localhost";
            int port = context.Request.Url?.Port ?? _profileManager.CurrentProfile.Port;
            string m3uContent = $"#EXTM3U\r\n#EXTINF:-1,{_profileManager.CurrentProfile.StationName ?? "Scrim Broadcast"}\r\nhttp://{host}:{port}/stream\r\n";
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(m3uContent);
            response.ContentLength64 = bytes.Length;
            response.OutputStream.Write(bytes, 0, bytes.Length);
            response.Close();
        }

        private void HandlePlsRequest(HttpListenerContext context) {
            var response = context.Response;
            response.ContentType = "audio/x-scpls; charset=utf-8";
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            string host = context.Request.Url?.Host ?? "localhost";
            int port = context.Request.Url?.Port ?? _profileManager.CurrentProfile.Port;
            string plsContent = $"[playlist]\r\nNumberOfEntries=1\r\nFile1=http://{host}:{port}/stream\r\nTitle1={_profileManager.CurrentProfile.StationName ?? "Scrim Broadcast"}\r\nLength1=-1\r\nVersion=2\r\n";
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
            string json = $"{{\"isLive\":{(isLive ? "true" : "false")},\"listeners\":{_hub.ActiveClientCount}}}";
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

            try {
                using var writer = new StreamWriter(response.OutputStream);
                while (!token.IsCancellationRequested) {
                    await Task.Delay(2000, token);
                    
                    var meta = _metadataService.CurrentMetadata;
                    bool hasArt = (meta.AlbumArt != null && meta.AlbumArt.Length > 0) || !string.IsNullOrEmpty(meta.AlbumArtUrl);
                    string artUrl = hasArt ? (string.IsNullOrEmpty(meta.AlbumArtUrl) ? "/api/albumart" : meta.AlbumArtUrl) : "";
                    string metaJson = $"{{\"type\":\"metadata\",\"title\":\"{EscapeJson(meta.Title)}\",\"artist\":\"{EscapeJson(meta.Artist)}\",\"album\":\"{EscapeJson(meta.Album)}\",\"hasArt\":{(hasArt ? "true" : "false")},\"albumArtUrl\":\"{EscapeJson(artUrl)}\"}}";
                    await writer.WriteAsync($"data: {metaJson}\n\n");

                    string statsJson = $"{{\"type\":\"stats\",\"listeners\":{_hub.ActiveClientCount},\"isLive\":{(_hub.IsBroadcasting ? "true" : "false")}}}";
                    await writer.WriteAsync($"data: {statsJson}\n\n");

                    var requests = _requestController.GetLiveQueue().Select(r => $"{{\"query\":\"{EscapeJson(r.Query)}\",\"status\":\"{EscapeJson(r.Status)}\"}}");
                    string queueJson = $"{{\"type\":\"queue\",\"requests\":[{string.Join(",", requests)}]}}";
                    await writer.WriteAsync($"data: {queueJson}\n\n");

                    var profile = _profileManager.CurrentProfile;
                    var navLinksArray = string.Join(",", profile.CustomNavLinks.Select(l => $"{{\"label\":\"{EscapeJson(l.Label)}\",\"url\":\"{EscapeJson(l.Url)}\"}}"));
                    string brandingJson = $"{{\"type\":\"branding\",\"stationName\":\"{EscapeJson(profile.StationName)}\",\"pageTitle\":\"{EscapeJson(profile.PageTitle)}\",\"showTitle\":\"{EscapeJson(profile.ShowTitle)}\",\"hostName\":\"{EscapeJson(profile.HostName)}\",\"genreTag\":\"{EscapeJson(profile.GenreTag)}\",\"tagline\":\"{EscapeJson(profile.StationTagline)}\",\"accentColor\":\"{EscapeJson(profile.AccentColor)}\",\"logoUrl\":\"{EscapeJson(profile.LogoUrl)}\",\"navLinks\":\"{EscapeJson(profile.NavLinks)}\",\"customNavLinks\":[{navLinksArray}]}}";
                    await writer.WriteAsync($"data: {brandingJson}\n\n");

                    await writer.FlushAsync();
                }
            } catch {
            } finally {
                response.Close();
            }
        }

        private void HandleBrandingRequest(HttpListenerContext context) {
            try {
                var profile = _profileManager.CurrentProfile;
                var navLinksArray = string.Join(",", profile.CustomNavLinks.Select(l => $"{{\"label\":\"{EscapeJson(l.Label)}\",\"url\":\"{EscapeJson(l.Url)}\"}}"));
                string json = $"{{\"stationName\":\"{EscapeJson(profile.StationName)}\",\"pageTitle\":\"{EscapeJson(profile.PageTitle)}\",\"showTitle\":\"{EscapeJson(profile.ShowTitle)}\",\"hostName\":\"{EscapeJson(profile.HostName)}\",\"genreTag\":\"{EscapeJson(profile.GenreTag)}\",\"tagline\":\"{EscapeJson(profile.StationTagline)}\",\"accentColor\":\"{EscapeJson(profile.AccentColor)}\",\"logoUrl\":\"{EscapeJson(profile.LogoUrl)}\",\"navLinks\":\"{EscapeJson(profile.NavLinks)}\",\"customNavLinks\":[{navLinksArray}]}}";
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
                using var reader = new StreamReader(context.Request.InputStream);
                string body = reader.ReadToEnd();
                // Simple JSON extraction: {"query":"Artist - Song"}
                var match = System.Text.RegularExpressions.Regex.Match(body, "\"query\"\\s*:\\s*\"(.*?)\"");
                if (match.Success) {
                    _requestController.SubmitRequest(match.Groups[1].Value);
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
                int port = _profileManager.CurrentProfile.Port;
                var response = context.Response;
                response.ContentType = "application/json";
                response.Headers.Add("Access-Control-Allow-Origin", "*");
                string json = $"{{\"localUrl\":\"{_networkDiscovery.GetLocalShareUrl(port)}\",\"publicUrl\":\"{_networkDiscovery.GetPublicShareUrl(port)}\",\"primaryLocalIp\":\"{_networkDiscovery.PrimaryLocalIp}\",\"publicIp\":\"{_networkDiscovery.PublicIp}\",\"upnpStatus\":\"{EscapeJson(_networkDiscovery.UpnpStatus)}\",\"isUpnpMapped\":{(_networkDiscovery.IsUpnpMapped ? "true" : "false")}}}";
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
                string json = $"{{\"title\":\"{EscapeJson(meta.Title)}\",\"artist\":\"{EscapeJson(meta.Artist)}\",\"album\":\"{EscapeJson(meta.Album)}\",\"hasArt\":{(hasArt ? "true" : "false")},\"albumArtUrl\":\"{EscapeJson(artUrl)}\"}}";

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
