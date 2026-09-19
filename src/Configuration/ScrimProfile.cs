using System;

namespace Scrim.Configuration {
    public class ScrimProfile {
        public string ProfileName { get; set; } = "Default";
        public int TargetProcessId { get; set; }
        public string AudioFormat { get; set; } = "Mp3";
        public int Bitrate { get; set; } = 128;
    }

    public interface IProfileManager {
        ScrimProfile CurrentProfile { get; }
        void SaveProfile(ScrimProfile profile);
        void LoadProfile(string name);
    }
}
