using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Scrim.Configuration {
    public class WebThemeDefinition {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("icon")]
        public string Icon { get; set; } = "🎨";

        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        [JsonPropertyName("accentColor")]
        public string AccentColor { get; set; } = "#00d2ff";

        [JsonPropertyName("isCustom")]
        public bool IsCustom { get; set; } = false;

        [JsonPropertyName("bgDark")]
        public string? BgDark { get; set; }

        [JsonPropertyName("cardBg")]
        public string? CardBg { get; set; }

        [JsonPropertyName("cardBorder")]
        public string? CardBorder { get; set; }

        [JsonPropertyName("cardBorderHover")]
        public string? CardBorderHover { get; set; }

        [JsonPropertyName("headerBg")]
        public string? HeaderBg { get; set; }

        [JsonPropertyName("headerBorder")]
        public string? HeaderBorder { get; set; }

        [JsonPropertyName("inputBg")]
        public string? InputBg { get; set; }

        [JsonPropertyName("textBright")]
        public string? TextBright { get; set; }

        [JsonPropertyName("textMuted")]
        public string? TextMuted { get; set; }

        [JsonPropertyName("textDim")]
        public string? TextDim { get; set; }

        [JsonPropertyName("accentGlow")]
        public string? AccentGlow { get; set; }

        [JsonPropertyName("amberColor")]
        public string? AmberColor { get; set; }

        [JsonPropertyName("playCream")]
        public string? PlayCream { get; set; }

        [JsonPropertyName("cssVariables")]
        public Dictionary<string, string> CssVariables { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, string> GetResolvedCssVariables() {
            var vars = new Dictionary<string, string>(CssVariables, StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(BgDark)) vars["--bg-dark"] = BgDark;
            if (!string.IsNullOrEmpty(CardBg)) vars["--card-bg"] = CardBg;
            if (!string.IsNullOrEmpty(CardBorder)) vars["--card-border"] = CardBorder;
            if (!string.IsNullOrEmpty(CardBorderHover)) vars["--card-border-hover"] = CardBorderHover;
            if (!string.IsNullOrEmpty(HeaderBg)) vars["--header-bg"] = HeaderBg;
            if (!string.IsNullOrEmpty(HeaderBorder)) vars["--header-border"] = HeaderBorder;
            if (!string.IsNullOrEmpty(InputBg)) vars["--input-bg"] = InputBg;
            if (!string.IsNullOrEmpty(TextBright)) vars["--text-bright"] = TextBright;
            if (!string.IsNullOrEmpty(TextMuted)) vars["--text-muted"] = TextMuted;
            if (!string.IsNullOrEmpty(TextDim)) vars["--text-dim"] = TextDim;
            if (!string.IsNullOrEmpty(AccentColor)) vars["--accent-color"] = AccentColor;
            if (!string.IsNullOrEmpty(AccentGlow)) vars["--accent-glow"] = AccentGlow;
            if (!string.IsNullOrEmpty(AmberColor)) vars["--amber-color"] = AmberColor;
            if (!string.IsNullOrEmpty(PlayCream)) vars["--play-cream"] = PlayCream;
            return vars;
        }
    }
}
