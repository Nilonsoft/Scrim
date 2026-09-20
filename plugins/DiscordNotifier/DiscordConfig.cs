using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Scrim.Plugin.DiscordNotifier {
    public class DiscordConfig {
        [JsonPropertyName("webhookUrl")]
        public string WebhookUrl { get; set; } = string.Empty;

        [JsonPropertyName("announceTracks")]
        public bool AnnounceTracks { get; set; } = true;

        [JsonPropertyName("announceBroadcastStatus")]
        public bool AnnounceBroadcastStatus { get; set; } = true;

        [JsonPropertyName("botUsername")]
        public string BotUsername { get; set; } = "Scrim Studio";

        [JsonPropertyName("avatarUrl")]
        public string? AvatarUrl { get; set; }

        public static DiscordConfig Load(string? pluginDir = null) {
            var config = new DiscordConfig();

            // 1. Try environment variable first
            var envWebhook = Environment.GetEnvironmentVariable("SCRIM_DISCORD_WEBHOOK");
            if (!string.IsNullOrWhiteSpace(envWebhook)) {
                config.WebhookUrl = envWebhook.Trim();
            }

            // Candidate configuration paths in priority order
            var pathsToTry = new System.Collections.Generic.List<string>();

            if (!string.IsNullOrEmpty(pluginDir)) {
                pathsToTry.Add(Path.Combine(pluginDir, "discord_config.json"));
            }

            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            pathsToTry.Add(Path.Combine(baseDir, "Plugins", "discord_config.json"));
            pathsToTry.Add(Path.Combine(baseDir, "discord_config.json"));

            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            pathsToTry.Add(Path.Combine(userProfile, ".scrim", "discord_config.json"));
            pathsToTry.Add(Path.Combine(userProfile, ".scrim", "plugins", "discord_config.json"));

            foreach (var path in pathsToTry) {
                if (File.Exists(path)) {
                    try {
                        var json = File.ReadAllText(path);
                        var loaded = JsonSerializer.Deserialize<DiscordConfig>(json, new JsonSerializerOptions {
                            PropertyNameCaseInsensitive = true
                        });
                        if (loaded != null) {
                            if (!string.IsNullOrWhiteSpace(loaded.WebhookUrl)) {
                                config.WebhookUrl = loaded.WebhookUrl.Trim();
                            }
                            config.AnnounceTracks = loaded.AnnounceTracks;
                            config.AnnounceBroadcastStatus = loaded.AnnounceBroadcastStatus;
                            if (!string.IsNullOrWhiteSpace(loaded.BotUsername)) {
                                config.BotUsername = loaded.BotUsername;
                            }
                            if (!string.IsNullOrWhiteSpace(loaded.AvatarUrl)) {
                                config.AvatarUrl = loaded.AvatarUrl;
                            }
                            break;
                        }
                    } catch {
                        // Continue to next candidate path if parse fails
                    }
                }
            }

            return config;
        }
    }
}
