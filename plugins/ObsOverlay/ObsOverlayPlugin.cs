using System;
using System.IO;
using System.Text.Json;
using Scrim.Metadata;
using Scrim.Plugins;

namespace Scrim.Plugin.ObsOverlay {
    public class ObsOverlayPlugin : IScrimPlugin {
        public string Name => "OBS Studio Overlay";
        public string Version => "1.0.0";
        public string Author => "Scrim Studio";

        private IScrimHost? _host;
        private string _obsDir = string.Empty;
        private string _lastTrack = string.Empty;

        public void Initialize(IScrimHost host) {
            _host = host;

            try {
                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                _obsDir = Path.Combine(userProfile, ".scrim", "obs");
                if (!Directory.Exists(_obsDir)) {
                    Directory.CreateDirectory(_obsDir);
                }

                EnsureOverlayHtml();
                WriteInitialFiles();

                _host.Metadata.MetadataChanged += OnMetadataChanged;
                _host.Broadcast.BroadcastingStateChanged += OnBroadcastingStateChanged;

                Console.WriteLine($"[{Name}] Initialized. OBS assets and overlay ready at {_obsDir}");
            } catch (Exception ex) {
                Console.WriteLine($"[{Name}] Error during initialization: {ex.Message}");
            }
        }

        private void WriteInitialFiles() {
            WriteTextFile("status.txt", "OFF AIR ⚪");
            WriteTextFile("nowplaying.txt", "Scrim Radio - Ready");
            WriteTextFile("title.txt", "Ready");
            WriteTextFile("artist.txt", "Scrim Studio");
            WriteTextFile("album.txt", "");
            WriteTextFile("station.txt", _host?.ProfileManager.CurrentProfile.StationName ?? "Scrim Studio");
            WriteTextFile("listeners.txt", "0");
            WriteOverlayJson("Ready", "Scrim Studio", "", false, false);
        }

        private void OnMetadataChanged(object? sender, MediaMetadata meta) {
            if (string.IsNullOrWhiteSpace(meta.Title)) {
                return;
            }

            string trackKey = $"{meta.Title} - {meta.Artist}";
            if (string.Equals(trackKey, _lastTrack, StringComparison.Ordinal)) {
                return;
            }
            _lastTrack = trackKey;

            bool isLive = _host?.Broadcast.IsBroadcasting ?? false;
            int listeners = _host?.Broadcast.ActiveClientCount ?? 0;
            string station = _host?.ProfileManager.CurrentProfile.StationName ?? "Scrim Studio";

            WriteTextFile("nowplaying.txt", trackKey);
            WriteTextFile("title.txt", meta.Title);
            WriteTextFile("artist.txt", meta.Artist);
            WriteTextFile("album.txt", meta.Album);
            WriteTextFile("station.txt", station);
            WriteTextFile("listeners.txt", listeners.ToString());

            bool hasArt = false;
            if (meta.AlbumArt != null && meta.AlbumArt.Length > 0) {
                try {
                    File.WriteAllBytes(Path.Combine(_obsDir, "cover.jpg"), meta.AlbumArt);
                    hasArt = true;
                } catch { }
            }

            WriteOverlayJson(meta.Title, meta.Artist, meta.Album, isLive, hasArt);
        }

        private void OnBroadcastingStateChanged(object? sender, bool isLive) {
            WriteTextFile("status.txt", isLive ? "ON AIR 🔴" : "OFF AIR ⚪");
            int listeners = _host?.Broadcast.ActiveClientCount ?? 0;
            WriteTextFile("listeners.txt", listeners.ToString());

            var meta = _host?.Metadata.CurrentMetadata;
            string title = meta?.Title ?? (isLive ? "Live Broadcast" : "Ready");
            string artist = meta?.Artist ?? (_host?.ProfileManager.CurrentProfile.StationName ?? "Scrim Studio");
            string album = meta?.Album ?? "";
            bool hasArt = File.Exists(Path.Combine(_obsDir, "cover.jpg"));

            WriteOverlayJson(title, artist, album, isLive, hasArt);
        }

        private void WriteOverlayJson(string title, string artist, string album, bool isLive, bool hasArt) {
            try {
                var data = new {
                    title,
                    artist,
                    album,
                    station = _host?.ProfileManager.CurrentProfile.StationName ?? "Scrim Studio",
                    listeners = _host?.Broadcast.ActiveClientCount ?? 0,
                    isLive,
                    hasArt,
                    updated = DateTime.UtcNow.ToString("o")
                };

                string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(Path.Combine(_obsDir, "overlay_data.json"), json);
            } catch { }
        }

        private void WriteTextFile(string filename, string content) {
            try {
                File.WriteAllText(Path.Combine(_obsDir, filename), content);
            } catch { }
        }

        private void EnsureOverlayHtml() {
            try {
                string htmlPath = Path.Combine(_obsDir, "overlay.html");
                string html = """
                <!DOCTYPE html>
                <html lang="en">
                <head>
                    <meta charset="UTF-8">
                    <title>Scrim OBS Overlay</title>
                    <style>
                        * { box-sizing: border-box; margin: 0; padding: 0; font-family: 'Segoe UI', system-ui, sans-serif; }
                        body { background: transparent; overflow: hidden; display: flex; align-items: flex-start; padding: 20px; }
                        .card {
                            display: flex; align-items: center; gap: 16px;
                            background: rgba(15, 23, 42, 0.88);
                            backdrop-filter: blur(12px);
                            border: 1px solid rgba(255, 255, 255, 0.12);
                            border-radius: 16px;
                            padding: 14px 20px;
                            box-shadow: 0 10px 30px rgba(0, 0, 0, 0.5);
                            color: #f8fafc;
                            max-width: 520px;
                            transition: all 0.3s ease;
                        }
                        .art-box {
                            width: 68px; height: 68px; border-radius: 12px; overflow: hidden;
                            flex-shrink: 0; background: #1e293b; display: flex; align-items: center; justify-content: center;
                            box-shadow: 0 4px 12px rgba(0, 0, 0, 0.4);
                        }
                        .art-box img { width: 100%; height: 100%; object-fit: cover; }
                        .info { display: flex; flex-direction: column; gap: 4px; overflow: hidden; }
                        .title { font-size: 17px; font-weight: 700; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
                        .artist { font-size: 13px; color: #94a3b8; font-weight: 500; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
                        .meta-bar { display: flex; align-items: center; gap: 10px; margin-top: 2px; }
                        .badge { font-size: 10px; font-weight: 700; text-transform: uppercase; padding: 2px 8px; border-radius: 9999px; letter-spacing: 0.5px; }
                        .badge-live { background: #ef4444; color: white; animation: pulse 2s infinite; }
                        .badge-off { background: #475569; color: #cbd5e1; }
                        .listeners { font-size: 11px; color: #64748b; font-weight: 600; }
                        @keyframes pulse { 0%, 100% { opacity: 1; } 50% { opacity: 0.6; } }
                    </style>
                </head>
                <body>
                    <div class="card" id="overlayCard">
                        <div class="art-box">
                            <img id="coverImg" src="cover.jpg" onerror="this.src='data:image/svg+xml;utf8,<svg xmlns=\'http://www.w3.org/2000/svg\' width=\'68\' height=\'68\' viewBox=\'0 0 24 24\' fill=\'none\' stroke=\'%2364748b\' stroke-width=\'2\'><path d=\'M9 18V5l12-2v13\'/><circle cx=\'6\' cy=\'18\' r=\'3\'/><circle cx=\'18\' cy=\'16\' r=\'3\'/></svg>'">
                        </div>
                        <div class="info">
                            <div class="title" id="trackTitle">Scrim Radio</div>
                            <div class="artist" id="trackArtist">Station DJ</div>
                            <div class="meta-bar">
                                <span class="badge badge-off" id="liveBadge">OFF AIR</span>
                                <span class="listeners" id="listenerCount">0 listeners</span>
                            </div>
                        </div>
                    </div>
                    <script>
                        async function update() {
                            try {
                                const res = await fetch('overlay_data.json?_t=' + Date.now());
                                if (res.ok) {
                                    const data = await res.json();
                                    document.getElementById('trackTitle').textContent = data.title || 'Live Stream';
                                    document.getElementById('trackArtist').textContent = data.artist || data.station || '';
                                    const badge = document.getElementById('liveBadge');
                                    if (data.isLive) {
                                        badge.className = 'badge badge-live';
                                        badge.textContent = 'ON AIR';
                                    } else {
                                        badge.className = 'badge badge-off';
                                        badge.textContent = 'OFF AIR';
                                    }
                                    document.getElementById('listenerCount').textContent = `${data.listeners || 0} listeners`;
                                    if (data.hasArt) {
                                        document.getElementById('coverImg').src = 'cover.jpg?_t=' + Date.now();
                                    }
                                }
                            } catch (e) {}
                        }
                        setInterval(update, 1000);
                        update();
                    </script>
                </body>
                </html>
                """;

                File.WriteAllText(htmlPath, html);
            } catch { }
        }

        public void Shutdown() {
            if (_host != null) {
                _host.Metadata.MetadataChanged -= OnMetadataChanged;
                _host.Broadcast.BroadcastingStateChanged -= OnBroadcastingStateChanged;
            }
            WriteTextFile("status.txt", "OFF AIR ⚪");
            Console.WriteLine($"[{Name}] Shutdown complete.");
        }
    }
}
