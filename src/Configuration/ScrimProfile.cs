using System;

namespace Scrim.Configuration {
    public class ScrimProfile {
        public string ProfileName { get; set; } = "Default";
        public int TargetProcessId { get; set; }
        public string AudioFormat { get; set; } = "Mp3";
        public int Bitrate { get; set; } = 128;
        public int Port { get; set; } = 4242;
        public bool CloseToTray { get; set; } = true;
    }

    public interface IProfileManager {
        ScrimProfile CurrentProfile { get; }
        System.Collections.Generic.IEnumerable<string> GetAvailableProfiles();
        void SaveProfile(ScrimProfile profile);
        void LoadProfile(string name);
    }
}
