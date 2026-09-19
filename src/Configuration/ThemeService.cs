using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Scrim.Configuration {
    public interface IThemeService {
        string ThemesDirectory { get; }
        IReadOnlyList<WebThemeDefinition> GetAvailableThemes();
        WebThemeDefinition? GetTheme(string id);
        void RefreshThemes();
        void OpenThemesDirectory();
    }

    public class ThemeService : IThemeService {
        private readonly string _themesDir;
        private readonly List<WebThemeDefinition> _builtInThemes = new();
        private List<WebThemeDefinition> _cachedCustomThemes = new();

        public string ThemesDirectory => _themesDir;

        public ThemeService() {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            _themesDir = Path.Combine(userProfile, ".scrim", "themes");

            InitializeBuiltInThemes();
            EnsureThemesDirectoryAndExample();
            RefreshThemes();
        }

        private void InitializeBuiltInThemes() {
            _builtInThemes.Add(new WebThemeDefinition {
                Id = "dark",
                Name = "Dark Studio",
                Icon = "🌙",
                Description = "Obsidian carbon broadcast studio with electric cyan accents",
                AccentColor = "#00d2ff",
                IsCustom = false
            });

            _builtInThemes.Add(new WebThemeDefinition {
                Id = "goth",
                Name = "Goth",
                Icon = "🦇",
                Description = "Pitch-black void, blood crimson, obsidian shadows, and gothic silver borders",
                AccentColor = "#e11d48",
                IsCustom = false
            });

            _builtInThemes.Add(new WebThemeDefinition {
                Id = "pink",
                Name = "Pink",
                Icon = "💖",
                Description = "Cyber bubblegum magenta, hot pink glow, and energetic neon highlights",
                AccentColor = "#ec4899",
                IsCustom = false
            });

            _builtInThemes.Add(new WebThemeDefinition {
                Id = "flowers",
                Name = "Flowers",
                Icon = "🌸",
                Description = "Botanical forest night, emerald foliage, and delicate floral rose touches",
                AccentColor = "#10b981",
                IsCustom = false
            });

            _builtInThemes.Add(new WebThemeDefinition {
                Id = "light",
                Name = "Light",
                Icon = "☀️",
                Description = "Crisp daytime studio with clean white surfaces and azure accents",
                AccentColor = "#0284c7",
                IsCustom = false
            });

            _builtInThemes.Add(new WebThemeDefinition {
                Id = "rock",
                Name = "Rock & Roll",
                Icon = "🎸",
                Description = "Vintage amplifier gold, high-voltage flame orange, and distressed copper",
                AccentColor = "#ea580c",
                IsCustom = false
            });

            _builtInThemes.Add(new WebThemeDefinition {
                Id = "synthwave",
                Name = "Synthwave 80s",
                Icon = "🌆",
                Description = "Retro-futuristic neon magenta and cyan grid with twilight sunset glow",
                AccentColor = "#f43f5e",
                IsCustom = false
            });

            _builtInThemes.Add(new WebThemeDefinition {
                Id = "cyberpunk",
                Name = "Cyberpunk",
                Icon = "⚡",
                Description = "High-voltage neon yellow, toxic cyan, and high-tech circuit hex grid",
                AccentColor = "#facc15",
                IsCustom = false
            });

            _builtInThemes.Add(new WebThemeDefinition {
                Id = "space",
                Name = "Midnight Nebula",
                Icon = "🌌",
                Description = "Deep cosmic void with starlight particles and glowing violet nebula dust",
                AccentColor = "#8b5cf6",
                IsCustom = false
            });

            _builtInThemes.Add(new WebThemeDefinition {
                Id = "lofi",
                Name = "Lo-Fi Cafe",
                Icon = "☕",
                Description = "Warm cozy coffee house amber, vintage vinyl texture, and lantern glow",
                AccentColor = "#f97316",
                IsCustom = false
            });

            _builtInThemes.Add(new WebThemeDefinition {
                Id = "ocean",
                Name = "Ocean Deep",
                Icon = "🌊",
                Description = "Abyssal navy depths with bioluminescent marine mint and aqua shimmer",
                AccentColor = "#06b6d4",
                IsCustom = false
            });
        }

        private void EnsureThemesDirectoryAndExample() {
            try {
                if (!Directory.Exists(_themesDir)) {
                    Directory.CreateDirectory(_themesDir);
                }

                string exampleFile = Path.Combine(_themesDir, "example-synthwave.json");
                if (!File.Exists(exampleFile)) {
                    var exampleTheme = new WebThemeDefinition {
                        Id = "synthwave",
                        Name = "Synthwave 80s",
                        Icon = "🌆",
                        Description = "Retro-futuristic neon violet and sunset orange grid aesthetic",
                        AccentColor = "#f43f5e",
                        IsCustom = true,
                        BgDark = "#0f0728",
                        CardBg = "#190d3d",
                        CardBorder = "#3b1a7d",
                        CardBorderHover = "#5c28c4",
                        HeaderBg = "#120930",
                        HeaderBorder = "#2c1460",
                        InputBg = "#140a33",
                        TextBright = "#fdf4ff",
                        TextMuted = "#f0abfc",
                        TextDim = "#c084fc",
                        AccentGlow = "0 0 24px rgba(244, 63, 94, 0.65)",
                        AmberColor = "#e879f9",
                        PlayCream = "#fae8ff"
                    };

                    string json = JsonSerializer.Serialize(exampleTheme, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(exampleFile, json);
                }
            } catch { }
        }

        public void RefreshThemes() {
            var customList = new List<WebThemeDefinition>();

            try {
                if (Directory.Exists(_themesDir)) {
                    foreach (var file in Directory.GetFiles(_themesDir, "*.json")) {
                        try {
                            string json = File.ReadAllText(file);
                            var theme = JsonSerializer.Deserialize<WebThemeDefinition>(json, new JsonSerializerOptions {
                                PropertyNameCaseInsensitive = true
                            });

                            if (theme != null && !string.IsNullOrWhiteSpace(theme.Id) && !string.IsNullOrWhiteSpace(theme.Name)) {
                                theme.IsCustom = true;
                                if (string.IsNullOrWhiteSpace(theme.Icon)) {
                                    theme.Icon = "🎨";
                                }
                                customList.Add(theme);
                            }
                        } catch { }
                    }
                }
            } catch { }

            _cachedCustomThemes = customList;
        }

        public IReadOnlyList<WebThemeDefinition> GetAvailableThemes() {
            var all = new List<WebThemeDefinition>(_builtInThemes);
            all.AddRange(_cachedCustomThemes);
            return all;
        }

        public WebThemeDefinition? GetTheme(string id) {
            if (string.IsNullOrWhiteSpace(id)) return _builtInThemes[0];
            return GetAvailableThemes().FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        public void OpenThemesDirectory() {
            try {
                if (!Directory.Exists(_themesDir)) {
                    Directory.CreateDirectory(_themesDir);
                }
                Process.Start(new ProcessStartInfo {
                    FileName = "explorer.exe",
                    Arguments = _themesDir,
                    UseShellExecute = true
                });
            } catch { }
        }
    }
}
