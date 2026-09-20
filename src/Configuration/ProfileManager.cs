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
            string assetsDir = Path.Combine(_configDir, "assets");
            if (!Directory.Exists(assetsDir)) {
                try {
                    Directory.CreateDirectory(assetsDir);
                } catch { }
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
            if (!Directory.Exists(_configDir)) {
                return new[] { "Default" };
            }
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

        public string? ResolveAssetPath(string? inputPath) {
            if (string.IsNullOrWhiteSpace(inputPath)) return null;

            string trimmed = inputPath.Trim();
            if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) {
                return null;
            }

            string assetsDir = Path.Combine(_configDir, "assets");
            if (!Directory.Exists(assetsDir)) {
                try {
                    Directory.CreateDirectory(assetsDir);
                } catch { }
            }

            string[] extensions = { "", ".jpg", ".jpeg", ".png", ".webp", ".gif", ".svg" };

            // 1. Direct path check (if absolute or relative to working directory)
            foreach (var ext in extensions) {
                string candidate = trimmed + ext;
                if (File.Exists(candidate)) {
                    return Path.GetFullPath(candidate);
                }
            }

            // Normalize path separators and remove leading .\ or ./ or / or \
            string clean = trimmed.Replace('/', '\\');
            if (clean.StartsWith(".\\")) {
                clean = clean.Substring(2);
            }
            clean = clean.TrimStart('\\');

            string subName = clean;
            if (clean.StartsWith("assets\\", StringComparison.OrdinalIgnoreCase)) {
                subName = clean.Substring(7).TrimStart('\\');
            }

            // 2. Check in ~/.scrim/assets/ (with priority: subName then clean)
            foreach (var ext in extensions) {
                string candidate = Path.Combine(assetsDir, subName + ext);
                if (File.Exists(candidate)) {
                    return Path.GetFullPath(candidate);
                }
            }
            foreach (var ext in extensions) {
                string candidate = Path.Combine(assetsDir, clean + ext);
                if (File.Exists(candidate)) {
                    return Path.GetFullPath(candidate);
                }
            }

            // 3. Check in ~/.scrim/
            foreach (var ext in extensions) {
                string candidate = Path.Combine(_configDir, clean + ext);
                if (File.Exists(candidate)) {
                    return Path.GetFullPath(candidate);
                }
            }

            // 4. Check in App BaseDirectory Web/Assets/
            string appWebAssets = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Web", "Assets");
            if (Directory.Exists(appWebAssets)) {
                foreach (var ext in extensions) {
                    string candidate = Path.Combine(appWebAssets, subName + ext);
                    if (File.Exists(candidate)) {
                        return Path.GetFullPath(candidate);
                    }
                }
            }

            return null;
        }
    }
}
