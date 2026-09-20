using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Scrim.Plugin.LastFmScrobbler {
    public class LastFmConfig {
        [JsonPropertyName("apiKey")]
        public string ApiKey { get; set; } = string.Empty;

        [JsonPropertyName("apiSecret")]
        public string ApiSecret { get; set; } = string.Empty;

        [JsonPropertyName("sessionKey")]
        public string SessionKey { get; set; } = string.Empty;

        [JsonPropertyName("listenBrainzToken")]
        public string ListenBrainzToken { get; set; } = string.Empty;

        [JsonPropertyName("minPlayPercentage")]
        public int MinPlayPercentage { get; set; } = 50;

        [JsonPropertyName("minPlaySeconds")]
        public int MinPlaySeconds { get; set; } = 30;

        public static LastFmConfig Load(string? pluginDir = null) {
            var config = new LastFmConfig();

            var envKey = Environment.GetEnvironmentVariable("SCRIM_LASTFM_APIKEY");
            if (!string.IsNullOrWhiteSpace(envKey)) config.ApiKey = envKey.Trim();

            var envSecret = Environment.GetEnvironmentVariable("SCRIM_LASTFM_SECRET");
            if (!string.IsNullOrWhiteSpace(envSecret)) config.ApiSecret = envSecret.Trim();

            var envSession = Environment.GetEnvironmentVariable("SCRIM_LASTFM_SESSION");
            if (!string.IsNullOrWhiteSpace(envSession)) config.SessionKey = envSession.Trim();

            var envLb = Environment.GetEnvironmentVariable("SCRIM_LISTENBRAINZ_TOKEN");
            if (!string.IsNullOrWhiteSpace(envLb)) config.ListenBrainzToken = envLb.Trim();

            var pathsToTry = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrEmpty(pluginDir)) {
                pathsToTry.Add(Path.Combine(pluginDir, "lastfm_config.json"));
            }
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            pathsToTry.Add(Path.Combine(baseDir, "Plugins", "lastfm_config.json"));
            pathsToTry.Add(Path.Combine(baseDir, "lastfm_config.json"));

            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            pathsToTry.Add(Path.Combine(userProfile, ".scrim", "lastfm_config.json"));
            pathsToTry.Add(Path.Combine(userProfile, ".scrim", "plugins", "lastfm_config.json"));

            foreach (var path in pathsToTry) {
                if (File.Exists(path)) {
                    try {
                        var json = File.ReadAllText(path);
                        var loaded = JsonSerializer.Deserialize<LastFmConfig>(json, new JsonSerializerOptions {
                            PropertyNameCaseInsensitive = true
                        });
                        if (loaded != null) {
                            if (!string.IsNullOrWhiteSpace(loaded.ApiKey)) config.ApiKey = loaded.ApiKey.Trim();
                            if (!string.IsNullOrWhiteSpace(loaded.ApiSecret)) config.ApiSecret = loaded.ApiSecret.Trim();
                            if (!string.IsNullOrWhiteSpace(loaded.SessionKey)) config.SessionKey = loaded.SessionKey.Trim();
                            if (!string.IsNullOrWhiteSpace(loaded.ListenBrainzToken)) config.ListenBrainzToken = loaded.ListenBrainzToken.Trim();
                            if (loaded.MinPlayPercentage > 0) config.MinPlayPercentage = loaded.MinPlayPercentage;
                            if (loaded.MinPlaySeconds > 0) config.MinPlaySeconds = loaded.MinPlaySeconds;
                            break;
                        }
                    } catch { }
                }
            }

            return config;
        }
    }
}
