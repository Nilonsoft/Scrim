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
        private HttpListener? _listener;
        private CancellationTokenSource? _cts;

        public HttpStreamServer(BroadcastHub hub, IMetadataService metadataService, SongRequestController requestController, IProfileManager profileManager) {
            _hub = hub;
            _metadataService = metadataService;
            _requestController = requestController;
            _profileManager = profileManager;
        }

        public void Start(int port) {
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{port}/");
            _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            _listener.Start();
            
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
                } else if (context.Request.Url!.AbsolutePath == "/api/branding") {
                    HandleBrandingRequest(context);
                } else if (context.Request.Url!.AbsolutePath == "/api/requests" && context.Request.HttpMethod == "POST") {
                    HandleSongRequest(context);
                } else {
                    context.Response.StatusCode = 404;
                    context.Response.Close();
                }
            }
        }

        private async Task HandleStreamClient(HttpListenerContext context, CancellationToken token) {
            var response = context.Response;
            response.ContentType = "audio/mpeg";
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
                    string metaJson = $"{{\"type\":\"metadata\",\"title\":\"{EscapeJson(meta.Title)}\",\"artist\":\"{EscapeJson(meta.Artist)}\"}}";
                    await writer.WriteAsync($"data: {metaJson}\n\n");

                    string statsJson = $"{{\"type\":\"stats\",\"listeners\":{_hub.ActiveClientCount}}}";
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

        private string EscapeJson(string? value) {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        // ServeWebPlayer removed in favor of EmbeddedWebPlayer

        public void Stop() {
            _cts?.Cancel();
            _listener?.Stop();
        }

        public void Dispose() {
            Stop();
            _listener?.Close();
        }
    }
}
