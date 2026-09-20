using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Scrim.Plugin.TwitchBot {
    public class TwitchConfig {
        [JsonPropertyName("channel")]
        public string Channel { get; set; } = string.Empty;

        [JsonPropertyName("botUsername")]
        public string BotUsername { get; set; } = string.Empty;

        [JsonPropertyName("oauthToken")]
        public string OauthToken { get; set; } = string.Empty;

        [JsonPropertyName("enableSongRequests")]
        public bool EnableSongRequests { get; set; } = true;

        [JsonPropertyName("commandPrefix")]
        public string CommandPrefix { get; set; } = "!";

        public static TwitchConfig Load(string? pluginDir = null) {
            var config = new TwitchConfig();

            var envToken = Environment.GetEnvironmentVariable("SCRIM_TWITCH_OAUTH");
            if (!string.IsNullOrWhiteSpace(envToken)) {
                config.OauthToken = envToken.Trim();
            }
            var envChannel = Environment.GetEnvironmentVariable("SCRIM_TWITCH_CHANNEL");
            if (!string.IsNullOrWhiteSpace(envChannel)) {
                config.Channel = envChannel.Trim();
            }

            var pathsToTry = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrEmpty(pluginDir)) {
                pathsToTry.Add(Path.Combine(pluginDir, "twitch_config.json"));
            }
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            pathsToTry.Add(Path.Combine(baseDir, "Plugins", "twitch_config.json"));
            pathsToTry.Add(Path.Combine(baseDir, "twitch_config.json"));

            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            pathsToTry.Add(Path.Combine(userProfile, ".scrim", "twitch_config.json"));
            pathsToTry.Add(Path.Combine(userProfile, ".scrim", "plugins", "twitch_config.json"));

            foreach (var path in pathsToTry) {
                if (File.Exists(path)) {
                    try {
                        var json = File.ReadAllText(path);
                        var loaded = JsonSerializer.Deserialize<TwitchConfig>(json, new JsonSerializerOptions {
                            PropertyNameCaseInsensitive = true
                        });
                        if (loaded != null) {
                            if (!string.IsNullOrWhiteSpace(loaded.Channel)) config.Channel = loaded.Channel.Trim();
                            if (!string.IsNullOrWhiteSpace(loaded.BotUsername)) config.BotUsername = loaded.BotUsername.Trim();
                            if (!string.IsNullOrWhiteSpace(loaded.OauthToken)) config.OauthToken = loaded.OauthToken.Trim();
                            config.EnableSongRequests = loaded.EnableSongRequests;
                            if (!string.IsNullOrWhiteSpace(loaded.CommandPrefix)) config.CommandPrefix = loaded.CommandPrefix.Trim();
                            break;
                        }
                    } catch { }
                }
            }

            return config;
        }
    }
}
