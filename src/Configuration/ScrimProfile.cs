using System;

namespace Scrim.Configuration {
    public class ScrimProfile {
        public string ProfileName { get; set; } = "Default";
        public int TargetProcessId { get; set; }
        public string TargetProcessName { get; set; } = "";
        public string SelectedMicrophoneId { get; set; } = "";
        public string SelectedMicrophoneName { get; set; } = "";
        public string SelectedVoiceEffect { get; set; } = "normal";
        public string MicControlMode { get; set; } = "PushToTalk";
        public bool MicMonitoring { get; set; } = false;
        public string AudioFormat { get; set; } = "Mp3";
        public int Bitrate { get; set; } = 128;
        public int Port { get; set; } = 4242;
        public bool EnableNetworkAccess { get; set; } = true;
        public bool EnableUpnpPortForwarding { get; set; } = true;
        public CloseToTrayMode CloseMode { get; set; } = CloseToTrayMode.MinimizeToTray;
        public bool BroadcastOnOpen { get; set; } = false;
        public bool MuteAppLocally { get; set; } = false;
        public int AppStreamVolume { get; set; } = 100;
        public string CaptureDeviceId { get; set; } = "";
        public string CaptureDeviceName { get; set; } = "";
        public string CustomPublicUrl { get; set; } = "";
        public bool UseHttps { get; set; } = false;
        public bool UseReverseProxy { get; set; } = false;
        public string StreamMountPoint { get; set; } = "stream";
        public bool RestrictToLocalNetwork { get; set; } = false;
        public bool EnableChat { get; set; } = true;
        public System.Collections.Generic.List<string> NicknameBlacklist { get; set; } = new();
        public System.Collections.Generic.List<BannedChatUser> BannedUsers { get; set; } = new();

        // Song History Configuration
        public bool EnableSongHistory { get; set; } = true;
        public int SongHistoryLimit { get; set; } = 10;

        // Web Player Branding Customization
        public string StationName { get; set; } = "GLOBAL INDIE RADIO";
        public string PageTitle { get; set; } = "Global Indie Radio - Live Broadcast";
        public string ShowTitle { get; set; } = "Late Night Indie Drive";
        public string HostName { get; set; } = "Sarah J.";
        public string StationTagline { get; set; } = "Indie Rock & Modern Anthems";
        public string GenreTag { get; set; } = "Indie Alternative";
        public string AccentColor { get; set; } = "#ef4444";
        public string WebTheme { get; set; } = "dark";
        public bool SyncConsoleThemeWithWeb { get; set; } = true;
        public bool EnableDynamicBackdrop { get; set; } = false;
        public string BroadcasterBio { get; set; } = "";
        public string SocialDiscord { get; set; } = "";
        public string SocialTwitch { get; set; } = "";
        public string SocialTwitter { get; set; } = "";
        public string ScheduleDescription { get; set; } = "";
        public string LogoUrl { get; set; } = "";
        public string BannerUrl { get; set; } = "";
        public string NavLinks { get; set; } = "Discover,Schedule,Shows,About";

        public System.Collections.Generic.List<WebNavLink> CustomNavLinks { get; set; } = new() {
            new WebNavLink { Label = "Discover", Url = "#discover" },
            new WebNavLink { Label = "Schedule", Url = "#schedule" },
            new WebNavLink { Label = "Shows", Url = "#shows" },
            new WebNavLink { Label = "About", Url = "#about" }
        };

        // Audio Visualizer Reaction Modes ("Reactors")
        public string DefaultVisualizerMode { get; set; } = "bars";
        public System.Collections.Generic.List<VisualizerReactorConfig> VisualizerReactors { get; set; } = new() {
            new VisualizerReactorConfig { Id = "bars", Label = "Bars", Emoji = "📊", BaseMode = "bars", IsEnabled = true, IsDefault = true, IsBuiltin = true },
            new VisualizerReactorConfig { Id = "wave", Label = "Wave", Emoji = "📈", BaseMode = "wave", IsEnabled = true, IsDefault = false, IsBuiltin = true },
            new VisualizerReactorConfig { Id = "spectrum", Label = "Spectrum", Emoji = "🌈", BaseMode = "spectrum", IsEnabled = true, IsDefault = false, IsBuiltin = true },
            new VisualizerReactorConfig { Id = "pulse", Label = "Pulse", Emoji = "✨", BaseMode = "pulse", IsEnabled = true, IsDefault = false, IsBuiltin = true }
        };

        // Layout Customization & Column Sizing Proportions
        public string ColumnLayoutMode { get; set; } = "Studio";
        public int CustomLeftWidth { get; set; } = 320;
        public int CustomRightWidth { get; set; } = 340;
        public System.Collections.Generic.List<string> LeftColumnCards { get; set; } = new() { "card-audio-routing", "card-dj-eq", "card-web-branding" };
        public System.Collections.Generic.List<string> CenterColumnCards { get; set; } = new() { "card-vu-talk", "card-voice-fx", "card-queue", "card-chat" };
        public System.Collections.Generic.List<string> RightColumnCards { get; set; } = new() { "card-encoding", "card-media-player", "card-soundboard" };
        public System.Collections.Generic.List<string> CollapsedCards { get; set; } = new();

        // High-DPI UI Scaling Percentage (e.g. 80, 100, 125, 150)
        public int UiScalePercentage { get; set; } = 100;

        // Console Window Bounds & State Persistence
        public double WindowWidth { get; set; } = 1200;
        public double WindowHeight { get; set; } = 800;
        public bool WindowMaximized { get; set; } = false;

        public System.Collections.Generic.List<CustomSoundItem> CustomSounds { get; set; } = new();

        // Pro DJ Stereo Mixer & Multi-Band Equalizer Configuration
        public DjEqualizerConfig DjEq { get; set; } = new();

        // SAM Broadcaster PRO Modular Configurations
        public DualDeckConfig DualDeck { get; set; } = new();
        public EventSchedulerConfig Scheduler { get; set; } = new();
        public DeadAirConfig DeadAir { get; set; } = new();
        public RotationRulesConfig RotationRules { get; set; } = new();
        public MultiEncoderConfig MultiEncoder { get; set; } = new();
    }

    public class DualDeckConfig {
        public float CrossfaderPosition { get; set; } = -1.0f;
        public string Curve { get; set; } = "ConstantPower";
        public int AutoCrossfadeSeconds { get; set; } = 4;
        public float DeckAVolume { get; set; } = 1.0f;
        public float DeckBVolume { get; set; } = 1.0f;
        public float DeckAPitch { get; set; } = 0.0f;
        public float DeckBPitch { get; set; } = 0.0f;
    }

    public class EventSchedulerConfig {
        public bool Enabled { get; set; } = true;
        public System.Collections.Generic.List<Scrim.Audio.ScheduledEvent> Events { get; set; } = new();
    }

    public class DeadAirConfig {
        public bool Enabled { get; set; } = false;
        public int SilenceSeconds { get; set; } = 10;
        public bool AutoPlayBackup { get; set; } = true;
        public string FallbackSound { get; set; } = "Airhorn";
        public bool PlayAudibleAlarm { get; set; } = true;
    }

    public class RotationRulesConfig {
        public bool Enabled { get; set; } = true;
        public int ArtistSeparationMinutes { get; set; } = 60;
        public int TrackSeparationMinutes { get; set; } = 180;
        public string ClockWheelTemplate { get; set; } = "2 A-List -> 1 Jingle -> 1 Gold -> 1 Request";
        public System.Collections.Generic.List<string> HeavyRotationTracks { get; set; } = new();
        public System.Collections.Generic.List<string> RecentsTracks { get; set; } = new();
        public System.Collections.Generic.List<string> ClassicGoldTracks { get; set; } = new();
        public System.Collections.Generic.List<string> JingleTracks { get; set; } = new();
    }

    public class MultiEncoderConfig {
        public bool MasterMp3Enabled { get; set; } = true;
        public bool MobileAacEnabled { get; set; } = false;
        public int MobileAacBitrate { get; set; } = 64;
        public bool FlacStreamEnabled { get; set; } = false;
        public bool IcecastRelayEnabled { get; set; } = false;
        public string IcecastServerUrl { get; set; } = "http://icecast.example.com:8000/live";
        public string IcecastPassword { get; set; } = "";
        public string IcecastMount { get; set; } = "/live";
    }

    public class DjEqualizerConfig {
        public bool Enabled { get; set; } = true;
        public bool Is10BandMode { get; set; } = false;
        public string ActivePreset { get; set; } = "Flat / Bypass";
        public bool EqBypass { get; set; } = false;

        // Music Deck
        public float MusicTrimDb { get; set; } = 0.0f;
        public float MusicPan { get; set; } = 0.0f;
        public float MusicStereoWidth { get; set; } = 1.0f;
        public bool MusicMuted { get; set; } = false;

        // Microphone
        public float MicGainDb { get; set; } = 0.0f;
        public float MicPan { get; set; } = 0.0f;
        public bool MicLowCut { get; set; } = true;

        // 3-Band Isolator
        public float LowGainDb { get; set; } = 0.0f;
        public float MidGainDb { get; set; } = 0.0f;
        public float HighGainDb { get; set; } = 0.0f;
        public bool LowKill { get; set; } = false;
        public bool MidKill { get; set; } = false;
        public bool HighKill { get; set; } = false;

        // DJ Sound Color Sweep Filter (-100 to +100)
        public float ColorFilterKnob { get; set; } = 0.0f;

        // 10-Band Graphic EQ (dB per ISO band)
        public float[] Bands10Db { get; set; } = new float[10];

        // Master Bus
        public float MasterGainDb { get; set; } = 0.0f;
        public float MasterBalance { get; set; } = 0.0f;
        public bool SoftLimiterEnabled { get; set; } = true;
    }

    public class VisualizerReactorConfig {
        public string Id { get; set; } = "";
        public string Label { get; set; } = "";
        public string Emoji { get; set; } = "";
        public string BaseMode { get; set; } = "bars"; // bars, wave, spectrum, pulse
        public bool IsEnabled { get; set; } = true;
        public bool IsDefault { get; set; } = false;
        public bool IsBuiltin { get; set; } = true;
    }

    public class CustomSoundItem {
        public string Name { get; set; } = "";
        public string FilePath { get; set; } = "";
    }

    public class WebNavLink {
        public string Label { get; set; } = "";
        public string Url { get; set; } = "";
    }

    public class BannedChatUser {
        public string UserId { get; set; } = "";
        public string Nickname { get; set; } = "";
        public DateTime BannedAt { get; set; } = DateTime.UtcNow;
    }

    public enum CloseToTrayMode {
        Ask,
        MinimizeToTray,
        Close
    }

    public interface IProfileManager {
        ScrimProfile CurrentProfile { get; }
        System.Collections.Generic.IEnumerable<string> GetAvailableProfiles();
        void SaveProfile(ScrimProfile profile);
        void LoadProfile(string name);
        bool ExportProfile(string profileName, string targetFilePath);
        bool ImportProfile(string sourceFilePath, out string importedName);
        bool DeleteProfile(string name);
        bool DuplicateProfile(string sourceName, string newName);
        string? ResolveAssetPath(string? inputPath);
    }
}
