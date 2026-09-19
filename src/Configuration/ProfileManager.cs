using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        public IEnumerable<string> GetAvailableProfiles() {
            var files = Directory.GetFiles(_configDir, "*.json");
            var names = files.Select(Path.GetFileNameWithoutExtension).ToList();
            if (!names.Contains("Default")) {
                names.Insert(0, "Default");
            }
            return names!;
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
