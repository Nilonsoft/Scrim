using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Scrim.Metadata;
using Scrim.Plugins;

namespace Scrim.Plugin.DiscordNotifier {
    public class DiscordNotifierPlugin : IScrimPlugin {
        public string Name => "Discord Now Playing Notifier";
        public string Version => "1.0.0";
        public string Author => "Scrim Studio";

        private IScrimHost? _host;
        private DiscordConfig _config = new();
        private static readonly HttpClient _http = new() {
            Timeout = TimeSpan.FromSeconds(5)
        };
        private string _lastTrack = string.Empty;

        public void Initialize(IScrimHost host) {
            _host = host;

            string? pluginDir = null;
            try {
                pluginDir = Path.GetDirectoryName(typeof(DiscordNotifierPlugin).Assembly.Location);
            } catch { }

            _config = DiscordConfig.Load(pluginDir);

            _host.Metadata.MetadataChanged += OnMetadataChanged;
            _host.Broadcast.BroadcastingStateChanged += OnBroadcastingStateChanged;

            if (string.IsNullOrWhiteSpace(_config.WebhookUrl)) {
                Console.WriteLine($"[{Name}] Initialized. Webhook URL is not set. Configure Plugins/discord_config.json or set SCRIM_DISCORD_WEBHOOK to enable.");
            } else {
                Console.WriteLine($"[{Name}] Initialized successfully. Webhook configured for Discord announcements.");
            }
        }

        private void OnMetadataChanged(object? sender, MediaMetadata meta) {
            if (!_config.AnnounceTracks || string.IsNullOrWhiteSpace(_config.WebhookUrl)) {
                return;
            }

            if (string.IsNullOrWhiteSpace(meta.Title) || meta.Title.StartsWith("Awaiting", StringComparison.OrdinalIgnoreCase)) {
                return;
            }

            string trackKey = $"{meta.Title} - {meta.Artist}";
            if (string.Equals(trackKey, _lastTrack, StringComparison.Ordinal)) {
                return;
            }
            _lastTrack = trackKey;

            Console.WriteLine($"[{Name}] Now Playing: {meta.Title} by {meta.Artist}");

            Task.Run(() => SendTrackNotificationAsync(meta));
        }

        private void OnBroadcastingStateChanged(object? sender, bool isLive) {
            if (!_config.AnnounceBroadcastStatus || string.IsNullOrWhiteSpace(_config.WebhookUrl)) {
                return;
            }

            Console.WriteLine($"[{Name}] Broadcast state: {(isLive ? "ON AIR 🔴" : "OFF AIR ⚪")}");

            Task.Run(() => SendBroadcastStatusNotificationAsync(isLive));
        }

        private async Task SendTrackNotificationAsync(MediaMetadata meta) {
            try {
                var stationName = _host?.ProfileManager.CurrentProfile.StationName;
                if (string.IsNullOrWhiteSpace(stationName)) {
                    stationName = "Scrim Live Station";
                }

                int listenerCount = _host?.Broadcast.ActiveClientCount ?? 0;
                string durationStr = meta.Duration > TimeSpan.Zero ? meta.Duration.ToString(@"mm\:ss") : "--:--";

                var payload = new {
                    username = _config.BotUsername,
                    avatar_url = _config.AvatarUrl,
                    embeds = new[] {
                        new {
                            title = string.IsNullOrWhiteSpace(meta.Title) ? "Unknown Track" : meta.Title,
                            description = $"**Artist:** {meta.Artist}\n**Album:** {meta.Album}",
                            color = 0x5865F2,
                            fields = new[] {
                                new { name = "Duration", value = durationStr, inline = true },
                                new { name = "Station", value = stationName, inline = true },
                                new { name = "Listeners", value = listenerCount.ToString(), inline = true }
                            },
                            thumbnail = (!string.IsNullOrWhiteSpace(meta.AlbumArtUrl) && meta.AlbumArtUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                                ? new { url = meta.AlbumArtUrl }
                                : null,
                            footer = new { text = "Scrim Studio • Now Playing" },
                            timestamp = DateTime.UtcNow.ToString("o")
                        }
                    }
                };

                string json = JsonSerializer.Serialize(payload);
                using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                var response = await _http.PostAsync(_config.WebhookUrl, content);
                if (!response.IsSuccessStatusCode) {
                    Console.WriteLine($"[{Name}] Discord webhook returned status: {(int)response.StatusCode} {response.ReasonPhrase}");
                }
            } catch (Exception ex) {
                Console.WriteLine($"[{Name}] Failed to send Discord notification: {ex.Message}");
            }
        }

        private async Task SendBroadcastStatusNotificationAsync(bool isLive) {
            try {
                var stationName = _host?.ProfileManager.CurrentProfile.StationName;
                if (string.IsNullOrWhiteSpace(stationName)) {
                    stationName = "Scrim Live Station";
                }

                var payload = new {
                    username = _config.BotUsername,
                    avatar_url = _config.AvatarUrl,
                    embeds = new[] {
                        new {
                            title = isLive ? "🔴 Station is ON AIR" : "⚪ Station is OFF AIR",
                            description = isLive 
                                ? $"**{stationName}** is now broadcasting live! Tune in now."
                                : $"**{stationName}** has ended the live stream.",
                            color = isLive ? 0x22C55E : 0x64748B,
                            footer = new { text = "Scrim Studio Broadcast Monitor" },
                            timestamp = DateTime.UtcNow.ToString("o")
                        }
                    }
                };

                string json = JsonSerializer.Serialize(payload);
                using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                await _http.PostAsync(_config.WebhookUrl, content);
            } catch (Exception ex) {
                Console.WriteLine($"[{Name}] Failed to send broadcast status notification: {ex.Message}");
            }
        }

        public void Shutdown() {
            if (_host != null) {
                _host.Metadata.MetadataChanged -= OnMetadataChanged;
                _host.Broadcast.BroadcastingStateChanged -= OnBroadcastingStateChanged;
            }
            Console.WriteLine($"[{Name}] Shutdown complete.");
        }
    }
}
