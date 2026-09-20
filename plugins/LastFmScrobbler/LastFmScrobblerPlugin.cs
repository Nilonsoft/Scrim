using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Scrim.Metadata;
using Scrim.Plugins;

namespace Scrim.Plugin.LastFmScrobbler {
    public class LastFmScrobblerPlugin : IScrimPlugin {
        public string Name => "Last.fm & ListenBrainz Scrobbler";
        public string Version => "1.0.0";
        public string Author => "Scrim Studio";

        private IScrimHost? _host;
        private LastFmConfig _config = new();
        private static readonly HttpClient _http = new() {
            Timeout = TimeSpan.FromSeconds(8)
        };

        private string _lastTrackKey = string.Empty;
        private DateTime _trackStartTime = DateTime.UtcNow;
        private bool _hasScrobbled = false;
        private MediaMetadata? _currentTrack;
        private Timer? _scrobbleTimer;
        private readonly object _lock = new();

        public void Initialize(IScrimHost host) {
            _host = host;

            string? pluginDir = null;
            try {
                pluginDir = Path.GetDirectoryName(typeof(LastFmScrobblerPlugin).Assembly.Location);
            } catch { }

            _config = LastFmConfig.Load(pluginDir);

            bool hasLastFm = !string.IsNullOrWhiteSpace(_config.ApiKey) && !string.IsNullOrWhiteSpace(_config.SessionKey);
            bool hasListenBrainz = !string.IsNullOrWhiteSpace(_config.ListenBrainzToken);

            if (!hasLastFm && !hasListenBrainz) {
                Console.WriteLine($"[{Name}] Initialized. Credentials not set. Configure Plugins/lastfm_config.json or set SCRIM_LISTENBRAINZ_TOKEN.");
            } else {
                Console.WriteLine($"[{Name}] Initialized successfully with {(hasLastFm ? "Last.fm " : "")}{(hasListenBrainz ? "ListenBrainz" : "")}!");
            }

            _host.Metadata.MetadataChanged += OnMetadataChanged;
            _scrobbleTimer = new Timer(CheckScrobbleThreshold, null, 1000, 1000);
        }

        private void OnMetadataChanged(object? sender, MediaMetadata meta) {
            if (string.IsNullOrWhiteSpace(meta.Title) || meta.Title.StartsWith("Awaiting", StringComparison.OrdinalIgnoreCase)) {
                return;
            }

            string key = $"{meta.Title} - {meta.Artist}";
            lock (_lock) {
                if (string.Equals(key, _lastTrackKey, StringComparison.Ordinal)) {
                    return;
                }
                _lastTrackKey = key;
                _currentTrack = meta;
                _trackStartTime = DateTime.UtcNow;
                _hasScrobbled = false;
            }

            Task.Run(() => SendNowPlayingAsync(meta));
        }

        private void CheckScrobbleThreshold(object? state) {
            MediaMetadata? track;
            DateTime startTime;
            bool alreadyDone;

            lock (_lock) {
                track = _currentTrack;
                startTime = _trackStartTime;
                alreadyDone = _hasScrobbled;
            }

            if (track == null || alreadyDone || string.IsNullOrWhiteSpace(track.Title)) {
                return;
            }

            var elapsed = DateTime.UtcNow - startTime;
            double totalSec = track.Duration.TotalSeconds;

            bool reachedMinSeconds = elapsed.TotalSeconds >= _config.MinPlaySeconds;
            bool reachedPercentage = totalSec > 0 && (elapsed.TotalSeconds / totalSec) >= (_config.MinPlayPercentage / 100.0);
            bool reachedMaxCap = elapsed.TotalSeconds >= 240; // 4 minutes max threshold standard

            if (reachedMinSeconds && (reachedPercentage || reachedMaxCap || totalSec <= 0)) {
                lock (_lock) {
                    _hasScrobbled = true;
                }
                Task.Run(() => SubmitScrobbleAsync(track, startTime));
            }
        }

        private async Task SendNowPlayingAsync(MediaMetadata meta) {
            if (!string.IsNullOrWhiteSpace(_config.ListenBrainzToken)) {
                await SubmitListenBrainzAsync(meta, "playing_now", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            }
        }

        private async Task SubmitScrobbleAsync(MediaMetadata meta, DateTime startTime) {
            long timestamp = new DateTimeOffset(startTime).ToUnixTimeSeconds();
            Console.WriteLine($"[{Name}] Scrobbling: {meta.Title} by {meta.Artist}");

            if (!string.IsNullOrWhiteSpace(_config.ListenBrainzToken)) {
                await SubmitListenBrainzAsync(meta, "single", timestamp);
            }

            if (!string.IsNullOrWhiteSpace(_config.ApiKey) && !string.IsNullOrWhiteSpace(_config.SessionKey) && !string.IsNullOrWhiteSpace(_config.ApiSecret)) {
                await SubmitLastFmScrobbleAsync(meta, timestamp);
            }
        }

        private async Task SubmitListenBrainzAsync(MediaMetadata meta, string listenType, long timestamp) {
            try {
                var payload = new {
                    listen_type = listenType,
                    payload = new[] {
                        new {
                            listened_at = listenType == "single" ? (long?)timestamp : null,
                            track_metadata = new {
                                artist_name = meta.Artist,
                                track_name = meta.Title,
                                release_name = meta.Album
                            }
                        }
                    }
                };

                string json = JsonSerializer.Serialize(payload);
                using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.listenbrainz.org/1/submit-listens");
                req.Headers.Add("Authorization", $"Token {_config.ListenBrainzToken}");
                req.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                await _http.SendAsync(req);
            } catch { }
        }

        private async Task SubmitLastFmScrobbleAsync(MediaMetadata meta, long timestamp) {
            try {
                var parameters = new System.Collections.Generic.Dictionary<string, string> {
                    ["method"] = "track.scrobble",
                    ["api_key"] = _config.ApiKey,
                    ["sk"] = _config.SessionKey,
                    ["artist"] = meta.Artist,
                    ["track"] = meta.Title,
                    ["timestamp"] = timestamp.ToString()
                };
                if (!string.IsNullOrWhiteSpace(meta.Album)) {
                    parameters["album"] = meta.Album;
                }

                // Compute Last.fm MD5 api_sig: sort keys, concatenate name+value, append secret
                var sortedKeys = new System.Collections.Generic.List<string>(parameters.Keys);
                sortedKeys.Sort(StringComparer.Ordinal);
                var sb = new StringBuilder();
                foreach (var k in sortedKeys) {
                    sb.Append(k).Append(parameters[k]);
                }
                sb.Append(_config.ApiSecret);

                using var md5 = MD5.Create();
                byte[] hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(sb.ToString()));
                string apiSig = Convert.ToHexStringLower(hash);

                parameters["api_sig"] = apiSig;
                parameters["format"] = "json";

                using var content = new FormUrlEncodedContent(parameters);
                await _http.PostAsync("https://ws.audioscrobbler.com/2.0/", content);
            } catch { }
        }

        public void Shutdown() {
            _scrobbleTimer?.Dispose();
            if (_host != null) {
                _host.Metadata.MetadataChanged -= OnMetadataChanged;
            }
            Console.WriteLine($"[{Name}] Shutdown complete.");
        }
    }
}
