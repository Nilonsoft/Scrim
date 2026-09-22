using System;
using System.Text.Json.Serialization;

namespace Scrim.Audio {
    public enum RelayStreamMode {
        ReencodedMix,
        RawPassthrough
    }

    public class RelayStreamConfig {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "Remote DJ Stream";
        public string StreamUrl { get; set; } = "";
        public RelayStreamMode Mode { get; set; } = RelayStreamMode.ReencodedMix;
        public bool IsEnabled { get; set; } = true;
        public float Volume { get; set; } = 1.0f;
        public int FadeInSeconds { get; set; } = 3;
        public string CustomAvatarUrl { get; set; } = "";
        public bool IsMuted { get; set; } = false;
        public bool PassthroughDjBranding { get; set; } = true;
        public bool PassthroughTrackMetadata { get; set; } = true;
        public string RemoteGreenRoomPasscode { get; set; } = "";

        // Pre-Fade Listen (PFL) Headphone Cueing
        [JsonIgnore]
        public bool IsCueActive { get; set; } = false;

        // Auto-detected metadata from remote Scrim
        [JsonIgnore]
        public bool IsConnected { get; set; } = false;
        [JsonIgnore]
        public bool IsScrimOrigin { get; set; } = false;
        [JsonIgnore]
        public string OriginDjName { get; set; } = "";
        [JsonIgnore]
        public string OriginAvatarUrl { get; set; } = "";
        [JsonIgnore]
        public string OriginBio { get; set; } = "";
        [JsonIgnore]
        public string OriginDiscord { get; set; } = "";
        [JsonIgnore]
        public string OriginTwitch { get; set; } = "";
        [JsonIgnore]
        public string OriginTwitter { get; set; } = "";

        // Remote track playback info
        [JsonIgnore]
        public string CurrentTrackTitle { get; set; } = "";
        [JsonIgnore]
        public string CurrentTrackArtist { get; set; } = "";

        // Health & Diagnostics Telemetry
        [JsonIgnore]
        public string StatusText { get; set; } = "Standby";
        [JsonIgnore]
        public string HealthStatus { get; set; } = "128 kbps • Stable";
        [JsonIgnore]
        public float CurrentGain { get; set; } = 0.0f;
        [JsonIgnore]
        public double BufferLatencySec { get; set; } = 0.0;

        [JsonIgnore]
        public string EffectiveDjName => IsScrimOrigin && !string.IsNullOrWhiteSpace(OriginDjName) 
            ? OriginDjName 
            : (!string.IsNullOrWhiteSpace(Name) ? Name : "Guest DJ");

        [JsonIgnore]
        public string EffectiveAvatarUrl {
            get {
                if (IsScrimOrigin && !string.IsNullOrWhiteSpace(OriginAvatarUrl)) {
                    return OriginAvatarUrl;
                }
                if (!string.IsNullOrWhiteSpace(CustomAvatarUrl)) {
                    return CustomAvatarUrl;
                }
                return "/icon.svg";
            }
        }
    }
}
