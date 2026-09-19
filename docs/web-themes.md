# Scrim Web Player Themes & Custom Theme Creation Guide

Scrim features a powerful, dynamic theming engine for the live broadcast web player. Broadcasters can choose from built-in curated themes or create their own custom themes using simple JSON files. Custom themes are automatically detected and appear directly inside the Scrim Console for one-click application.

---

## Table of Contents
1. [Built-In Themes](#built-in-themes)
2. [Where Custom Themes Live](#where-custom-themes-live)
3. [Creating a Custom Theme](#creating-a-custom-theme)
4. [JSON Schema & Available Properties](#json-schema--available-properties)
5. [Complete Theme Examples](#complete-theme-examples)
6. [Applying Themes in Scrim Console](#applying-themes-in-scrim-console)
7. [CSS Custom Properties Reference](#css-custom-properties-reference)

---

## Built-In Themes

Scrim includes 11 professionally curated broadcast themes out of the box with custom background artwork and ambient gradient patterns:

| Theme | Key | Icon | Description |
| :--- | :---: | :---: | :--- |
| **Dark Studio** | `dark` | 🌙 | Modern obsidian carbon broadcast studio with electric cyan accents and studio spotlight aura. |
| **Goth** | `goth` | 🦇 | Pitch-black void (`#050608`), blood crimson accents (`#e11d48`), cathedral vignette shadows, and silver borders. |
| **Pink** | `pink` | 💖 | Cyber bubblegum neon magenta (`#130a17`), hot pink glows (`#ec4899`), and dual atmospheric gradients. |
| **Flowers** | `flowers` | 🌸 | Serene botanical night aesthetic (`#09130e`), emerald foliage, and blossom gold dust highlights. |
| **Light** | `light` | ☀️ | Crisp daytime broadcast studio (`#f8fafc`), clean white cards, dark typography (`#0f172a`), and sky blue light. |
| **Rock & Roll** | `rock` | 🎸 | Vintage amplifier aesthetic (`#120d09`), high-voltage flame orange (`#ea580c`), and incandescent amber flare. |
| **Synthwave 80s** | `synthwave` | 🌆 | Retro-futuristic neon magenta and cyan grid with twilight sunset glow. |
| **Cyberpunk** | `cyberpunk` | ⚡ | High-voltage neon yellow, toxic cyan, and high-tech circuit hex matrix grid. |
| **Midnight Nebula** | `space` | 🌌 | Deep cosmic void with starlight particles and glowing violet nebula dust clouds. |
| **Lo-Fi Cafe** | `lofi` | ☕ | Warm cozy coffee house amber, vintage vinyl micro-groove texture, and lantern glow. |
| **Ocean Deep** | `ocean` | 🌊 | Abyssal navy depths with bioluminescent marine mint and aqua shimmer caustics. |

---

## Where Custom Themes Live

Custom themes are stored in your Scrim user configuration directory:
```
%USERPROFILE%\.scrim\themes\
```
(For example: `C:\Users\YourUsername\.scrim\themes\`)

> [!TIP]
> You can open this folder directly from the Scrim Console by navigating to the **Website Branding & Links** card and clicking **`📁 Open Themes Folder`**.

---

## Creating a Custom Theme

1. Click **`📁 Open Themes Folder`** in the Scrim Console (or navigate to `~/.scrim/themes/`).
2. Create a new `.json` file (e.g. `cyberpunk.json` or `coffee-house.json`).
3. Fill in the theme details (name, icon, background, cards, and accent colors).
4. Save the file.
5. In the Scrim Console, click **`⟳ Refresh`** next to **Website Theme**.
6. Your new theme immediately appears as a selectable option marked with `(User)`!
7. Click the theme button to apply it to your web player in real time.

---

## JSON Schema & Available Properties

A theme JSON file supports top-level color shorthand properties as well as arbitrary CSS custom properties:

```json
{
  "id": "theme-unique-id",
  "name": "Display Name in Console",
  "icon": "🎨",
  "description": "Short description shown on hover",
  "accentColor": "#00d2ff",
  "bgDark": "#0d0f12",
  "cardBg": "#16181d",
  "cardBorder": "#232730",
  "cardBorderHover": "#313744",
  "headerBg": "#101216",
  "headerBorder": "#1c1f26",
  "inputBg": "#101216",
  "textBright": "#ffffff",
  "textMuted": "#8c93a4",
  "textDim": "#5c6374",
  "accentGlow": "0 0 20px rgba(0, 210, 255, 0.55)",
  "amberColor": "#f59e0b",
  "playCream": "#f4f1e6",
  "cssVariables": {
    "--custom-rule": "value"
  }
}
```

### Property Descriptions

- **`id`** *(Required)*: Unique lowercase identifier (e.g., `synthwave`, `emerald-forest`).
- **`name`** *(Required)*: The title shown on the button in the Scrim Console.
- **`icon`** *(Optional)*: An emoji or glyph shown next to the name (defaults to `🎨`).
- **`description`** *(Optional)*: Tooltip description displayed when hovering the option in the console.
- **`accentColor`** *(Optional)*: Hex color for the primary highlight (visualizer, scrubber, on-air pill, etc.).
- **`bgDark`** *(Optional)*: Main page background color.
- **`cardBg`** *(Optional)*: Background surface color for player and sidebar cards.
- **`cardBorder`** *(Optional)*: Border outline color for cards.
- **`headerBg`** *(Optional)*: Background color of the top broadcast navigation bar.
- **`textBright`** *(Optional)*: Main heading and primary readable text color.
- **`textMuted`** *(Optional)*: Subtitle and secondary metadata text color.
- **`cssVariables`** *(Optional)*: Key-value dictionary of any additional CSS variables to inject directly into `:root`.

---

## Complete Theme Examples

### 1. Synthwave 80s (`synthwave.json`)
```json
{
  "id": "synthwave",
  "name": "Synthwave 80s",
  "icon": "🌆",
  "description": "Retro-futuristic neon violet and sunset orange grid aesthetic",
  "accentColor": "#f43f5e",
  "bgDark": "#0f0728",
  "cardBg": "#190d3d",
  "cardBorder": "#3b1a7d",
  "cardBorderHover": "#5c28c4",
  "headerBg": "#120930",
  "headerBorder": "#2c1460",
  "inputBg": "#140a33",
  "textBright": "#fdf4ff",
  "textMuted": "#f0abfc",
  "textDim": "#c084fc",
  "accentGlow": "0 0 24px rgba(244, 63, 94, 0.65)",
  "amberColor": "#e879f9",
  "playCream": "#fae8ff"
}
```

### 2. Cyberpunk Neon (`cyberpunk.json`)
```json
{
  "id": "cyberpunk",
  "name": "Cyberpunk Neon",
  "icon": "⚡",
  "description": "High-contrast midnight black, electric yellow, and cyan lasers",
  "accentColor": "#facc15",
  "bgDark": "#040406",
  "cardBg": "#0a0a0f",
  "cardBorder": "#262638",
  "cardBorderHover": "#facc15",
  "headerBg": "#07070b",
  "headerBorder": "#1e1e2d",
  "inputBg": "#08080d",
  "textBright": "#ffffff",
  "textMuted": "#00f0ff",
  "textDim": "#71717a",
  "accentGlow": "0 0 22px rgba(250, 204, 21, 0.65)",
  "amberColor": "#00f0ff",
  "playCream": "#fef08a"
}
```

### 3. Coffee House Lo-Fi (`coffee-house.json`)
```json
{
  "id": "coffee-house",
  "name": "Coffee House",
  "icon": "☕",
  "description": "Warm mocha tones, roasted walnut surfaces, and caramel foam highlights",
  "accentColor": "#d97706",
  "bgDark": "#18120e",
  "cardBg": "#241b15",
  "cardBorder": "#423226",
  "cardBorderHover": "#5e4736",
  "headerBg": "#1d1611",
  "headerBorder": "#36291f",
  "inputBg": "#1a130f",
  "textBright": "#fef3c7",
  "textMuted": "#d6c2ab",
  "textDim": "#a89078",
  "accentGlow": "0 0 20px rgba(217, 119, 6, 0.45)",
  "amberColor": "#f59e0b",
  "playCream": "#fef9c3"
}
```

---

## Applying Themes in Scrim Console

1. In Scrim, scroll to the **Website Branding & Links** card.
2. Under **Website Theme**, you will see your themes alongside the built-in themes.
3. User-created themes will have `(User)` appended to their name.
4. Click any theme button to activate it:
   - Scrim immediately saves the theme in your active profile.
   - Connected listeners and web players adapt within seconds over Server-Sent Events (SSE) without needing a page refresh.

---

## CSS Custom Properties Reference

The web player uses the following CSS Custom Properties on `:root`:

| CSS Property | Default (Dark) | Description |
| :--- | :--- | :--- |
| `--bg-dark` | `#0d0f12` | Full browser page background |
| `--card-bg` | `#16181d` | Surface background for the main card and sidebars |
| `--card-border` | `#232730` | Border color for player containers |
| `--card-border-hover` | `#313744` | Highlight border on hover |
| `--header-bg` | `#101216` | Top navigation header background |
| `--header-border` | `#1c1f26` | Bottom border of top navigation header |
| `--input-bg` | `#101216` | Input background for the song request box |
| `--text-bright` | `#ffffff` | Primary text (song title, station brand) |
| `--text-muted` | `#8c93a4` | Secondary text (artist, descriptions, links) |
| `--text-dim` | `#5c6374` | Timestamps, durations, subtle markers |
| `--accent-color` | `#00d2ff` | Visualizer bars/wave, timeline fill, buttons |
| `--accent-glow` | `rgba(...)` | Box-shadow glow for active accents |
| `--on-air-red` | `#ef4444` | ON AIR badge and live broadcasting indicators |
| `--play-cream` | `#f4f1e6` | Background color of the large center play button |
