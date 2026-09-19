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
                BgDark = "#0d0f12",
                CardBg = "#16181d",
                CardBorder = "#232730",
                CardBorderHover = "#313744",
                HeaderBg = "#101216",
                HeaderBorder = "#1c1f26",
                InputBg = "#101216",
                TextBright = "#ffffff",
                TextMuted = "#8c93a4",
                TextDim = "#5c6374",
                AccentGlow = "0 0 20px rgba(0, 210, 255, 0.55)",
                IsCustom = false
            });

            _builtInThemes.Add(new WebThemeDefinition {
                Id = "goth",
                Name = "Goth",
                Icon = "🦇",
                Description = "Pitch-black void, blood crimson, obsidian shadows, and gothic silver borders",
                AccentColor = "#e11d48",
                BgDark = "#050608",
                CardBg = "#0b0d11",
                CardBorder = "#1d212b",
                CardBorderHover = "#2e3442",
                HeaderBg = "#08090d",
                HeaderBorder = "#171a22",
                InputBg = "#08090d",
                TextBright = "#f8fafc",
                TextMuted = "#94a3b8",
                TextDim = "#64748b",
                AccentGlow = "0 0 24px rgba(225, 29, 72, 0.65)",
                IsCustom = false
            });

            _builtInThemes.Add(new WebThemeDefinition {
                Id = "pink",
                Name = "Pink",
                Icon = "💖",
                Description = "Cyber bubblegum magenta, hot pink glow, and energetic neon highlights",
                AccentColor = "#ec4899",
                BgDark = "#130a17",
                CardBg = "#1e0f23",
                CardBorder = "#3b1c44",
                CardBorderHover = "#582966",
                HeaderBg = "#170c1b",
                HeaderBorder = "#34183d",
                InputBg = "#160b1b",
                TextBright = "#ffffff",
                TextMuted = "#f472b6",
                TextDim = "#a855f7",
                AccentGlow = "0 0 22px rgba(236, 72, 153, 0.65)",
                IsCustom = false
            });

            _builtInThemes.Add(new WebThemeDefinition {
                Id = "flowers",
                Name = "Flowers",
                Icon = "🌸",
                Description = "Botanical forest night, emerald foliage, and delicate floral rose touches",
                AccentColor = "#10b981",
                BgDark = "#09130e",
                CardBg = "#111f18",
                CardBorder = "#213a2e",
                CardBorderHover = "#2f5442",
                HeaderBg = "#0c1812",
                HeaderBorder = "#1d3228",
                InputBg = "#0e1914",
                TextBright = "#f0fdf4",
                TextMuted = "#86efac",
                TextDim = "#4ade80",
                AccentGlow = "0 0 22px rgba(16, 185, 129, 0.6)",
                IsCustom = false
            });

            _builtInThemes.Add(new WebThemeDefinition {
                Id = "light",
                Name = "Light",
                Icon = "☀️",
                Description = "Crisp daytime studio with clean white surfaces and azure accents",
                AccentColor = "#0284c7",
                BgDark = "#f8fafc",
                CardBg = "#ffffff",
                CardBorder = "#e2e8f0",
                CardBorderHover = "#cbd5e1",
                HeaderBg = "#ffffff",
                HeaderBorder = "#e2e8f0",
                InputBg = "#f1f5f9",
                TextBright = "#0f172a",
                TextMuted = "#475569",
                TextDim = "#94a3b8",
                AccentGlow = "0 0 20px rgba(2, 132, 199, 0.35)",
                IsCustom = false
            });

            _builtInThemes.Add(new WebThemeDefinition {
                Id = "rock",
                Name = "Rock & Roll",
                Icon = "🎸",
                Description = "Vintage amplifier gold, high-voltage flame orange, and distressed copper",
                AccentColor = "#ea580c",
                BgDark = "#120d09",
                CardBg = "#1c140e",
                CardBorder = "#3b281b",
                CardBorderHover = "#583b27",
                HeaderBg = "#16100b",
                HeaderBorder = "#2e1d13",
                InputBg = "#150f09",
                TextBright = "#fef3c7",
                TextMuted = "#fbbf24",
                TextDim = "#d97706",
                AccentGlow = "0 0 22px rgba(234, 88, 12, 0.65)",
                IsCustom = false
            });

            _builtInThemes.Add(new WebThemeDefinition {
                Id = "synthwave",
                Name = "Synthwave 80s",
                Icon = "🌆",
                Description = "Retro-futuristic neon magenta and cyan grid with twilight sunset glow",
                AccentColor = "#f43f5e",
                BgDark = "#0d041a",
                CardBg = "#170b2e",
                CardBorder = "#3a1766",
                CardBorderHover = "#5d23a6",
                HeaderBg = "#120724",
                HeaderBorder = "#2a0f4d",
                InputBg = "#140827",
                TextBright = "#ffffff",
                TextMuted = "#f0abfc",
                TextDim = "#c084fc",
                AccentGlow = "0 0 24px rgba(244, 63, 94, 0.7)",
                IsCustom = false
            });

            _builtInThemes.Add(new WebThemeDefinition {
                Id = "cyberpunk",
                Name = "Cyberpunk",
                Icon = "⚡",
                Description = "High-voltage neon yellow, toxic cyan, and high-tech circuit hex grid",
                AccentColor = "#facc15",
                BgDark = "#07090e",
                CardBg = "#0e121a",
                CardBorder = "#1e2636",
                CardBorderHover = "#334155",
                HeaderBg = "#0a0d13",
                HeaderBorder = "#19202c",
                InputBg = "#0b0e15",
                TextBright = "#fef08a",
                TextMuted = "#cbd5e1",
                TextDim = "#64748b",
                AccentGlow = "0 0 22px rgba(250, 204, 21, 0.75)",
                IsCustom = false
            });

            _builtInThemes.Add(new WebThemeDefinition {
                Id = "space",
                Name = "Midnight Nebula",
                Icon = "🌌",
                Description = "Deep cosmic void with starlight particles and glowing violet nebula dust",
                AccentColor = "#8b5cf6",
                BgDark = "#040714",
                CardBg = "#0b1026",
                CardBorder = "#1c2752",
                CardBorderHover = "#2f4082",
                HeaderBg = "#070c1e",
                HeaderBorder = "#172044",
                InputBg = "#090e22",
                TextBright = "#f8fafc",
                TextMuted = "#a5b4fc",
                TextDim = "#6366f1",
                AccentGlow = "0 0 24px rgba(139, 92, 246, 0.7)",
                IsCustom = false
            });

            _builtInThemes.Add(new WebThemeDefinition {
                Id = "lofi",
                Name = "Lo-Fi Cafe",
                Icon = "☕",
                Description = "Warm cozy coffee house amber, vintage vinyl texture, and lantern glow",
                AccentColor = "#f97316",
                BgDark = "#140e0a",
                CardBg = "#201712",
                CardBorder = "#3e2b20",
                CardBorderHover = "#593e2e",
                HeaderBg = "#19120d",
                HeaderBorder = "#322319",
                InputBg = "#1a130e",
                TextBright = "#fffbeb",
                TextMuted = "#fed7aa",
                TextDim = "#d97706",
                AccentGlow = "0 0 22px rgba(249, 115, 22, 0.65)",
                IsCustom = false
            });

            _builtInThemes.Add(new WebThemeDefinition {
                Id = "ocean",
                Name = "Ocean Deep",
                Icon = "🌊",
                Description = "Abyssal navy depths with bioluminescent marine mint and aqua shimmer",
                AccentColor = "#06b6d4",
                BgDark = "#030c17",
                CardBg = "#07172b",
                CardBorder = "#113259",
                CardBorderHover = "#194a82",
                HeaderBg = "#051121",
                HeaderBorder = "#0d2745",
                InputBg = "#061424",
                TextBright = "#f0fdfa",
                TextMuted = "#67e8f9",
                TextDim = "#06b6d4",
                AccentGlow = "0 0 24px rgba(6, 182, 212, 0.7)",
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
