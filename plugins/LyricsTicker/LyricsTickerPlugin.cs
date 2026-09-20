using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Scrim.Metadata;
using Scrim.Plugins;

namespace Scrim.Plugin.LyricsTicker {
    public class LyricsTickerPlugin : IScrimPlugin {
        public string Name => "Synchronized Lyrics Ticker";
        public string Version => "1.0.0";
        public string Author => "Scrim Studio";

        private IScrimHost? _host;
        private string _obsDir = string.Empty;
        private static readonly HttpClient _http = new() {
            Timeout = TimeSpan.FromSeconds(5)
        };

        private record LyricLine(TimeSpan Timestamp, string Text);
        private readonly List<LyricLine> _currentLyrics = new();
        private readonly object _lock = new();
        private string _lastTrack = string.Empty;
        private string _currentLyricText = string.Empty;
        private DateTime _trackStartTime = DateTime.UtcNow;
        private Timer? _tickerTimer;

        public void Initialize(IScrimHost host) {
            _host = host;

            try {
                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                _obsDir = Path.Combine(userProfile, ".scrim", "obs");
                if (!Directory.Exists(_obsDir)) {
                    Directory.CreateDirectory(_obsDir);
                }

                WriteLyricFiles(string.Empty);

                _host.Metadata.MetadataChanged += OnMetadataChanged;
                _tickerTimer = new Timer(OnTick, null, 250, 250);

                Console.WriteLine($"[{Name}] Initialized. Lyrics will sync to {_obsDir}\\lyrics.txt");
            } catch (Exception ex) {
                Console.WriteLine($"[{Name}] Initialization error: {ex.Message}");
            }
        }

        private void OnMetadataChanged(object? sender, MediaMetadata meta) {
            if (string.IsNullOrWhiteSpace(meta.Title) || meta.Title.StartsWith("Awaiting", StringComparison.OrdinalIgnoreCase)) {
                ClearLyrics();
                return;
            }

            string trackKey = $"{meta.Title} - {meta.Artist}";
            if (string.Equals(trackKey, _lastTrack, StringComparison.Ordinal)) {
                return;
            }
            _lastTrack = trackKey;
            _trackStartTime = DateTime.UtcNow;

            ClearLyrics();

            Task.Run(() => FetchLyricsAsync(meta.Title, meta.Artist));
        }

        private async Task FetchLyricsAsync(string title, string artist) {
            try {
                string url = $"https://lrclib.net/api/get?artist_name={Uri.EscapeDataString(artist)}&track_name={Uri.EscapeDataString(title)}";
                var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Add("User-Agent", "Scrim-Studio/1.0.0");

                var res = await _http.SendAsync(req);
                if (res.IsSuccessStatusCode) {
                    var json = await res.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("syncedLyrics", out var syncedElement) && syncedElement.ValueKind == JsonValueKind.String) {
                        string? lrc = syncedElement.GetString();
                        if (!string.IsNullOrWhiteSpace(lrc)) {
                            ParseLrc(lrc);
                            Console.WriteLine($"[{Name}] Found synced lyrics for: {title} by {artist}");
                        }
                    }
                }
            } catch {
                // Silently ignore network or parsing failures
            }
        }

        private void ParseLrc(string lrc) {
            var lines = new List<LyricLine>();
            var regex = new Regex(@"\[(\d{2}):(\d{2})\.(\d{2,3})\](.*)");

            using var reader = new StringReader(lrc);
            string? line;
            while ((line = reader.ReadLine()) != null) {
                var match = regex.Match(line);
                if (match.Success) {
                    if (int.TryParse(match.Groups[1].Value, out int min) &&
                        int.TryParse(match.Groups[2].Value, out int sec)) {
                        string msStr = match.Groups[3].Value.PadRight(3, '0');
                        int.TryParse(msStr, out int ms);
                        var time = new TimeSpan(0, 0, min, sec, ms);
                        string text = match.Groups[4].Value.Trim();
                        lines.Add(new LyricLine(time, text));
                    }
                }
            }

            lines.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));

            lock (_lock) {
                _currentLyrics.Clear();
                _currentLyrics.AddRange(lines);
            }
        }

        private void OnTick(object? state) {
            var meta = _host?.Metadata.CurrentMetadata;
            if (meta == null || string.IsNullOrWhiteSpace(meta.Title)) {
                return;
            }

            TimeSpan currentPos = meta.Position;
            if (currentPos <= TimeSpan.Zero) {
                currentPos = DateTime.UtcNow - _trackStartTime;
            }

            string activeText = string.Empty;

            lock (_lock) {
                if (_currentLyrics.Count > 0) {
                    for (int i = _currentLyrics.Count - 1; i >= 0; i--) {
                        if (currentPos >= _currentLyrics[i].Timestamp) {
                            activeText = _currentLyrics[i].Text;
                            break;
                        }
                    }
                }
            }

            if (activeText != _currentLyricText) {
                _currentLyricText = activeText;
                WriteLyricFiles(activeText);
            }
        }

        private void ClearLyrics() {
            lock (_lock) {
                _currentLyrics.Clear();
            }
            _currentLyricText = string.Empty;
            WriteLyricFiles(string.Empty);
        }

        private void WriteLyricFiles(string text) {
            try {
                if (string.IsNullOrEmpty(_obsDir)) return;
                File.WriteAllText(Path.Combine(_obsDir, "lyrics.txt"), text);

                var payload = new {
                    text,
                    updated = DateTime.UtcNow.ToString("o")
                };
                string json = JsonSerializer.Serialize(payload);
                File.WriteAllText(Path.Combine(_obsDir, "lyrics.json"), json);
            } catch { }
        }

        public void Shutdown() {
            _tickerTimer?.Dispose();
            if (_host != null) {
                _host.Metadata.MetadataChanged -= OnMetadataChanged;
            }
            WriteLyricFiles(string.Empty);
            Console.WriteLine($"[{Name}] Shutdown complete.");
        }
    }
}
