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
            LoadActiveOrInitialProfile();
        }

        private void LoadActiveOrInitialProfile() {
            string activeFile = Path.Combine(_configDir, "active_profile.txt");
            string profileToLoad = "Default";

            if (File.Exists(activeFile)) {
                try {
                    string activeName = File.ReadAllText(activeFile).Trim();
                    if (!string.IsNullOrEmpty(activeName) && File.Exists(Path.Combine(_configDir, $"{activeName}.json"))) {
                        profileToLoad = activeName;
                    }
                } catch { }
            }

            string targetJson = Path.Combine(_configDir, $"{profileToLoad}.json");
            if (File.Exists(targetJson)) {
                try {
                    string json = File.ReadAllText(targetJson);
                    CurrentProfile = JsonSerializer.Deserialize<ScrimProfile>(json) ?? new ScrimProfile();
                } catch {
                    CurrentProfile = new ScrimProfile();
                }
            } else {
                CurrentProfile = new ScrimProfile();
                SaveProfile(CurrentProfile);
            }
        }

        public void SaveProfile(ScrimProfile profile) {
            string file = Path.Combine(_configDir, $"{profile.ProfileName}.json");
            string json = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(file, json);
            CurrentProfile = profile;

            try {
                File.WriteAllText(Path.Combine(_configDir, "active_profile.txt"), profile.ProfileName);
            } catch { }
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
                try {
                    File.WriteAllText(Path.Combine(_configDir, "active_profile.txt"), name);
                } catch { }
            }
        }
    }
}
