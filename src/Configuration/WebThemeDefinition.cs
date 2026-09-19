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

        public Dictionary<string, string> GetConsoleCssVariables(string? customAccent = null) {
            var vars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string bg = BgDark ?? "#121316";
            string card = CardBg ?? "#191b20";
            string border = CardBorder ?? "#262931";
            string borderHover = CardBorderHover ?? "#363b46";
            string text = TextBright ?? "#ffffff";
            string muted = TextMuted ?? "#8e95a5";
            string dim = TextDim ?? "#5f6675";
            string accent = !string.IsNullOrWhiteSpace(customAccent) ? customAccent : (!string.IsNullOrWhiteSpace(AccentColor) ? AccentColor : "#00d2ff");
            string glow = AccentGlow ?? $"0 0 20px {accent}66";
            string header = HeaderBg ?? bg;
            string headerBorder = HeaderBorder ?? border;
            string input = InputBg ?? card;

            vars["--bg-main"] = bg;
            vars["--card-bg"] = card;
            vars["--card-bg-gradient"] = $"linear-gradient(180deg, {card} 0%, {bg} 100%)";
            vars["--card-border"] = border;
            vars["--card-border-hover"] = borderHover;
            vars["--text-main"] = text;
            vars["--text-muted"] = muted;
            vars["--text-sub"] = dim;
            vars["--pill-bg"] = input;
            vars["--pill-border"] = border;
            vars["--accent-blue"] = accent;
            vars["--accent-color"] = accent;
            vars["--accent-glow"] = glow;
            vars["--topbar-bg"] = header;
            vars["--topbar-border"] = headerBorder;

            foreach (var kv in CssVariables) {
                vars[kv.Key] = kv.Value;
            }

            return vars;
        }
    }
}
