using System;

namespace Scrim.Configuration {
    public class ScrimProfile {
        public string ProfileName { get; set; } = "Default";
        public int TargetProcessId { get; set; }
        public string AudioFormat { get; set; } = "Mp3";
        public int Bitrate { get; set; } = 128;
        public int Port { get; set; } = 4242;
        public CloseToTrayMode CloseMode { get; set; } = CloseToTrayMode.Ask;

        // Web Player Branding Customization
        public string StationName { get; set; } = "GLOBAL INDIE RADIO";
        public string PageTitle { get; set; } = "Global Indie Radio - Live Broadcast";
        public string ShowTitle { get; set; } = "Late Night Indie Drive";
        public string HostName { get; set; } = "Sarah J.";
        public string StationTagline { get; set; } = "Indie Rock & Modern Anthems";
        public string GenreTag { get; set; } = "Indie Alternative";
        public string AccentColor { get; set; } = "#ef4444";
        public string LogoUrl { get; set; } = "";
        public string NavLinks { get; set; } = "Discover,Schedule,Shows,About";
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
