using System;
using System.IO;
using System.Net;
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

        public void Start(int port) {
            if (_listener != null && _listener.IsListening) return;

            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{port}/");
            _listener.Prefixes.Add($"http://127.0.0.1:{port}/");

            var profile = _profileManager.CurrentProfile;
            if (profile.EnableNetworkAccess) {
                // Bind to all active local IPv4 addresses on the network so other LAN devices can connect
                foreach (var ip in _networkDiscovery.GetAllLocalIps()) {
                    try {
                        _listener.Prefixes.Add($"http://{ip}:{port}/");
                    } catch { }
                }

                // Attempt wildcard registration if URL reservation / permissions permit
                try {
                    _listener.Prefixes.Add($"http://+:{port}/");
                } catch { }

                // Trigger UPnP automatic router port forwarding if enabled for outside-network listeners
                if (profile.EnableUpnpPortForwarding) {
                    Task.Run(() => _networkDiscovery.TryMapUpnpPort(port));
                }
            }

            try {
                _listener.Start();
            } catch (Exception) {
                // If wildcard prefix failed, fall back to localhost and local IPs only
                if (_listener.Prefixes.Contains($"http://+:{port}/")) {
                    _listener.Prefixes.Remove($"http://+:{port}/");
                    try { _listener.Start(); } catch { }
                }
            }
            
            _cts = new CancellationTokenSource();
            
            Task.Run(() => AcceptLoop(_cts.Token));
        }

        private async Task AcceptLoop(CancellationToken token) {
            while (!token.IsCancellationRequested) {
                var context = await _listener!.GetContextAsync();
                
                if (context.Request.Url!.AbsolutePath == "/stream") {
                    _ = HandleStreamClient(context, token);
                } else if (context.Request.Url!.AbsolutePath == "/" || context.Request.Url!.AbsolutePath.StartsWith("/assets/")) {
                    Scrim.Web.EmbeddedWebPlayer.ServeAsync(context);
                } else if (context.Request.Url!.AbsolutePath == "/api/events") {
                    _ = HandleSseClient(context, token);
                } else if (context.Request.Url!.AbsolutePath == "/api/network") {
                    HandleNetworkRequest(context);
                } else if (context.Request.Url!.AbsolutePath == "/api/albumart") {
                    HandleAlbumArtRequest(context);
                } else if (context.Request.Url!.AbsolutePath == "/api/metadata") {
                    HandleMetadataRequest(context);
                } else if (context.Request.Url!.AbsolutePath == "/api/branding") {
                    HandleBrandingRequest(context);
                } else if (context.Request.Url!.AbsolutePath == "/api/requests" && context.Request.HttpMethod == "POST") {
                    HandleSongRequest(context);
                } else if (context.Request.Url!.AbsolutePath == "/api/status") {
                    HandleStatusRequest(context);
                } else {
                    context.Response.StatusCode = 404;
                    context.Response.Close();
                }
            }
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
