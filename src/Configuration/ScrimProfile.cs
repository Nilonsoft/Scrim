using System;

namespace Scrim.Configuration {
    public class ScrimProfile {
        public string ProfileName { get; set; } = "Default";
        public int TargetProcessId { get; set; }
        public string TargetProcessName { get; set; } = "";
        public string SelectedMicrophoneId { get; set; } = "";
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
        public string LogoUrl { get; set; } = "";
        public string NavLinks { get; set; } = "Discover,Schedule,Shows,About";

        public System.Collections.Generic.List<WebNavLink> CustomNavLinks { get; set; } = new() {
            new WebNavLink { Label = "Discover", Url = "#discover" },
            new WebNavLink { Label = "Schedule", Url = "#schedule" },
            new WebNavLink { Label = "Shows", Url = "#shows" },
            new WebNavLink { Label = "About", Url = "#about" }
        };

        // Layout Customization & Column Sizing Proportions
        public string ColumnLayoutMode { get; set; } = "Studio";
        public int CustomLeftWidth { get; set; } = 320;
        public int CustomRightWidth { get; set; } = 340;
        public System.Collections.Generic.List<string> LeftColumnCards { get; set; } = new() { "card-audio-routing", "card-web-branding" };
        public System.Collections.Generic.List<string> CenterColumnCards { get; set; } = new() { "card-vu-talk", "card-voice-fx", "card-queue", "card-chat" };
        public System.Collections.Generic.List<string> RightColumnCards { get; set; } = new() { "card-encoding", "card-media-player", "card-soundboard" };

        public System.Collections.Generic.List<CustomSoundItem> CustomSounds { get; set; } = new();
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
    }
}
