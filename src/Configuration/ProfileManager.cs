using System;
using System.IO;
using System.Text.Json;

namespace Scrim.Configuration {
    public class ProfileManager : IProfileManager {
        private readonly string _configDir;
        
        public ScrimProfile CurrentProfile { get; private set; } = new ScrimProfile();

        public ProfileManager() {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            _configDir = Path.Combine(appData, ".scrim");
            if (!Directory.Exists(_configDir)) {
                Directory.CreateDirectory(_configDir);
            }
        }

        public void SaveProfile(ScrimProfile profile) {
            string file = Path.Combine(_configDir, $"{profile.ProfileName}.json");
            string json = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(file, json);
            CurrentProfile = profile;
        }

        public void LoadProfile(string name) {
            string file = Path.Combine(_configDir, $"{name}.json");
            if (File.Exists(file)) {
                string json = File.ReadAllText(file);
                CurrentProfile = JsonSerializer.Deserialize<ScrimProfile>(json) ?? new ScrimProfile();
            }
        }
    }
}
