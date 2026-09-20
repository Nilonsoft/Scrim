using System;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Scrim.Plugins;

namespace Scrim.Plugin.StreamDeckCompanion {
    public class StreamDeckCompanionPlugin : IScrimPlugin {
        public string Name => "Stream Deck & Hardware Macro Companion";
        public string Version => "1.0.0";
        public string Author => "Scrim Studio";

        private IScrimHost? _host;
        private HttpListener? _listener;
        private CancellationTokenSource? _cts;
        private int _port = 5055;

        public void Initialize(IScrimHost host) {
            _host = host;

            try {
                LoadConfig();

                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://127.0.0.1:{_port}/api/");
                _listener.Start();

                _cts = new CancellationTokenSource();
                Task.Run(() => ListenAsync(_cts.Token));

                Console.WriteLine($"[{Name}] Initialized. Hardware control REST API listening on http://127.0.0.1:{_port}/api/");
            } catch (Exception ex) {
                Console.WriteLine($"[{Name}] Initialization warning: {ex.Message}");
            }
        }

        private void LoadConfig() {
            try {
                string? pluginDir = Path.GetDirectoryName(typeof(StreamDeckCompanionPlugin).Assembly.Location);
                string cfgPath = Path.Combine(pluginDir ?? "", "streamdeck_config.json");
                if (!File.Exists(cfgPath)) {
                    cfgPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins", "streamdeck_config.json");
                }

                if (File.Exists(cfgPath)) {
                    string json = File.ReadAllText(cfgPath);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("port", out var p) && p.TryGetInt32(out int portNum)) {
                        _port = portNum;
                    }
                }
            } catch { }
        }

        private async Task ListenAsync(CancellationToken token) {
            while (_listener != null && _listener.IsListening && !token.IsCancellationRequested) {
                try {
                    var context = await _listener.GetContextAsync();
                    _ = Task.Run(() => HandleRequestAsync(context));
                } catch (HttpListenerException) {
                    break;
                } catch (OperationCanceledException) {
                    break;
                } catch { }
            }
        }

        private async Task HandleRequestAsync(HttpListenerContext context) {
            var req = context.Request;
            var res = context.Response;

            // CORS headers for web dashboards & widgets
            res.Headers.Add("Access-Control-Allow-Origin", "*");
            res.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            res.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

            if (req.HttpMethod == "OPTIONS") {
                res.StatusCode = 204;
                res.Close();
                return;
            }

            string path = req.Url?.AbsolutePath.TrimEnd('/').ToLowerInvariant() ?? "";

            try {
                if (req.HttpMethod == "GET" && path == "/api/status") {
                    var meta = _host?.Metadata.CurrentMetadata;
                    var status = new {
                        isLive = _host?.Broadcast.IsBroadcasting ?? false,
                        station = _host?.ProfileManager.CurrentProfile.StationName ?? "Scrim Studio",
                        listeners = _host?.Broadcast.ActiveClientCount ?? 0,
                        title = meta?.Title ?? "",
                        artist = meta?.Artist ?? "",
                        album = meta?.Album ?? "",
                        duration = meta?.Duration.ToString(@"mm\:ss") ?? "00:00",
                        position = meta?.Position.ToString(@"mm\:ss") ?? "00:00",
                        isPlaying = meta?.IsPlaying ?? false,
                        micMuted = _host?.AudioMixer.IsToggleMuted ?? false,
                        micLive = _host?.AudioMixer.IsMicLive ?? false
                    };
                    await SendJsonAsync(res, 200, status);
                } else if (req.HttpMethod == "POST" && path == "/api/player/playpause") {
                    if (_host != null) await _host.Metadata.TogglePlayPauseAsync();
                    await SendJsonAsync(res, 200, new { ok = true, action = "playpause" });
                } else if (req.HttpMethod == "POST" && path == "/api/player/skip") {
                    if (_host != null) await _host.Metadata.SkipNextAsync();
                    await SendJsonAsync(res, 200, new { ok = true, action = "skip" });
                } else if (req.HttpMethod == "POST" && path == "/api/player/previous") {
                    if (_host != null) await _host.Metadata.SkipPreviousAsync();
                    await SendJsonAsync(res, 200, new { ok = true, action = "previous" });
                } else if (req.HttpMethod == "POST" && path == "/api/mic/toggle") {
                    if (_host != null) {
                        _host.AudioMixer.IsToggleMuted = !_host.AudioMixer.IsToggleMuted;
                    }
                    await SendJsonAsync(res, 200, new { ok = true, micMuted = _host?.AudioMixer.IsToggleMuted ?? false });
                } else if (req.HttpMethod == "POST" && path == "/api/sfx/play") {
                    string? fileParam = req.QueryString["file"];
                    if (!string.IsNullOrWhiteSpace(fileParam)) {
                        string targetPath = fileParam;
                        if (!File.Exists(targetPath)) {
                            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                            targetPath = Path.Combine(userProfile, ".scrim", "jingles", fileParam);
                        }
                        if (File.Exists(targetPath) && _host != null) {
                            _host.AudioMixer.PlayAudioFile(targetPath);
                            await SendJsonAsync(res, 200, new { ok = true, played = Path.GetFileName(targetPath) });
                            return;
                        }
                    }
                    await SendJsonAsync(res, 404, new { ok = false, error = "File not found" });
                } else {
                    await SendJsonAsync(res, 404, new { error = "Endpoint not found" });
                }
            } catch (Exception ex) {
                try {
                    await SendJsonAsync(res, 500, new { error = ex.Message });
                } catch { }
            }
        }

        private static async Task SendJsonAsync(HttpListenerResponse res, int statusCode, object data) {
            res.StatusCode = statusCode;
            res.ContentType = "application/json; charset=utf-8";
            string json = JsonSerializer.Serialize(data);
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(json);
            res.ContentLength64 = bytes.Length;
            await res.OutputStream.WriteAsync(bytes);
            res.Close();
        }

        public void Shutdown() {
            _cts?.Cancel();
            try {
                _listener?.Stop();
                _listener?.Close();
            } catch { }
            Console.WriteLine($"[{Name}] Shutdown complete.");
        }
    }
}
