using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Scrim.Plugin.MidiController {
    public class MidiConfig {
        [JsonPropertyName("deviceName")]
        public string DeviceName { get; set; } = string.Empty;

        [JsonPropertyName("volumeCc")]
        public int VolumeCc { get; set; } = 7;

        [JsonPropertyName("micMuteNote")]
        public int MicMuteNote { get; set; } = 36;

        [JsonPropertyName("broadcastToggleNote")]
        public int BroadcastToggleNote { get; set; } = 37;

        [JsonPropertyName("skipTrackNote")]
        public int SkipTrackNote { get; set; } = 38;

        [JsonPropertyName("playPauseNote")]
        public int PlayPauseNote { get; set; } = 39;

        public static MidiConfig Load(string? pluginDir = null) {
            var config = new MidiConfig();

            var pathsToTry = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrEmpty(pluginDir)) {
                pathsToTry.Add(Path.Combine(pluginDir, "midi_config.json"));
            }
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            pathsToTry.Add(Path.Combine(baseDir, "Plugins", "midi_config.json"));
            pathsToTry.Add(Path.Combine(baseDir, "midi_config.json"));

            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            pathsToTry.Add(Path.Combine(userProfile, ".scrim", "midi_config.json"));
            pathsToTry.Add(Path.Combine(userProfile, ".scrim", "plugins", "midi_config.json"));

            foreach (var path in pathsToTry) {
                if (File.Exists(path)) {
                    try {
                        var json = File.ReadAllText(path);
                        var loaded = JsonSerializer.Deserialize<MidiConfig>(json, new JsonSerializerOptions {
                            PropertyNameCaseInsensitive = true
                        });
                        if (loaded != null) {
                            if (!string.IsNullOrWhiteSpace(loaded.DeviceName)) config.DeviceName = loaded.DeviceName.Trim();
                            if (loaded.VolumeCc >= 0) config.VolumeCc = loaded.VolumeCc;
                            if (loaded.MicMuteNote >= 0) config.MicMuteNote = loaded.MicMuteNote;
                            if (loaded.BroadcastToggleNote >= 0) config.BroadcastToggleNote = loaded.BroadcastToggleNote;
                            if (loaded.SkipTrackNote >= 0) config.SkipTrackNote = loaded.SkipTrackNote;
                            if (loaded.PlayPauseNote >= 0) config.PlayPauseNote = loaded.PlayPauseNote;
                            break;
                        }
                    } catch { }
                }
            }

            return config;
        }
    }
}
