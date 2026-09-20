# Creating Scrim Plugins Without Source Code

This guide provides a complete, step-by-step walkthrough for building, compiling, and deploying custom plugins for **Scrim** using only the compiled application and the .NET SDK. You do **not** need access to the Scrim source code repository or private build tools.

---

## 1. Overview & Architecture

Scrim features a modular plugin architecture that dynamically discovers and executes both compiled **.NET assemblies** (`.dll`) and **Python plugins** (`.py` or packaged folders) at startup. Plugins can:
- **Hook into Real-Time Metadata**: Detect track changes, song durations, album art, and playback states from Spotify, media players, or browser streams.
- **Monitor Live Broadcasts**: Track active listener counts, bitrate, format, transmitter (TX) state, and ON AIR toggles.
- **Interact with Station Profiles**: Read and adapt to station branding, port settings, audio presets, and themes.
- **Moderate & Automate Song Requests**: Inspect, approve, or reject incoming listener song requests and dedications.
- **Inject Custom Dashboard Cards**: Add custom drag-and-drop Blazor cards into the DJ console layout (`IUiExtension`, for .NET plugins).
- **Run Isolated Python Workflows**: Write automations, webhooks, analytics, and bots in pure Python with full access to `pip` packages without compiling C# code.

---

## 2. Prerequisites

To build a Scrim plugin, you need:
1. **.NET 10 SDK** (or compatible modern .NET SDK) installed on your system. Download from [dotnet.microsoft.com](https://dotnet.microsoft.com/download).
2. **Scrim Installed on Windows**: Install via `ScrimSetup-v1.0.0.msi`.
3. Your preferred editor or IDE (Visual Studio, VS Code, JetBrains Rider, or the command line).

---

## 3. Where Scrim Finds Your Plugins

Scrim checks two locations for plugin DLLs on startup:

| Location | Path | Use Case |
|---|---|---|
| **User Directory (Recommended)** | `%USERPROFILE%\.scrim\plugins\` | No administrator privileges needed. Drop your `.dll` here and Scrim loads it automatically. |
| **Application Directory** | `%LOCALAPPDATA%\Programs\Scrim\Plugins\` or `<InstallDir>\Plugins\` | For system-wide installations or bundled extensions. |

> [!TIP]
> If `%USERPROFILE%\.scrim\plugins\` does not exist on your computer yet, Scrim will create it automatically when launched, or you can create it yourself in PowerShell:
> ```powershell
> New-Item -ItemType Directory -Path "$HOME\.scrim\plugins" -Force
> ```

---

## 4. Where to Find `Scrim.dll`

To build a plugin without the source code, your project references `Scrim.dll` directly from your local installation. 

Default installation paths for `Scrim.dll`:
- **Standard Per-User Install (Default)**:
  `%LOCALAPPDATA%\Programs\Scrim\Scrim.dll` (e.g. `C:\Users\<username>\AppData\Local\Programs\Scrim\Scrim.dll`)
- **System-Wide Install**:
  `C:\Program Files\Scrim\Scrim.dll`
- **Portable / Custom Folder**:
  The directory containing `Scrim.exe`.

---

## 5. Step-by-Step Plugin Creation

### Step 1: Scaffold a New Project

Open a terminal (PowerShell or Command Prompt) and create a new class library:

```powershell
dotnet new classlib -n Scrim.Plugin.DiscordNotifier -f net10.0-windows10.0.19041.0
cd Scrim.Plugin.DiscordNotifier
```

### Step 2: Configure Your `.csproj` File

Replace the contents of `Scrim.Plugin.DiscordNotifier.csproj` with the following configuration. This references `Scrim.dll` from your local install and ensures `Scrim.dll` is marked as `Private="false"` (so Scrim's own assemblies are not duplicated in your plugin output):

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
  </PropertyGroup>

  <ItemGroup>
    <!-- Reference the installed Scrim.dll -->
    <Reference Include="Scrim">
      <!-- Checks the default Scrim installation directory -->
      <HintPath Condition="Exists('$(LocalAppData)\Programs\Scrim\Scrim.dll')">$(LocalAppData)\Programs\Scrim\Scrim.dll</HintPath>
      <!-- Fallback check for Program Files -->
      <HintPath Condition="!Exists('$(LocalAppData)\Programs\Scrim\Scrim.dll') And Exists('$(ProgramFiles)\Scrim\Scrim.dll')">$(ProgramFiles)\Scrim\Scrim.dll</HintPath>
      <!-- Private=false prevents copying Scrim.dll into your output folder -->
      <Private>false</Private>
    </Reference>
  </ItemGroup>

</Project>
```

> [!NOTE]
> If you copied `Scrim.dll` directly into a local `lib/` folder in your project repository, you can simply use:
> ```xml
> <Reference Include="Scrim">
>   <HintPath>lib\Scrim.dll</HintPath>
>   <Private>false</Private>
> </Reference>
> ```

---

## 6. Implementing the Plugin Interface

A Scrim plugin is any public class in your assembly that implements the `IScrimPlugin` interface.

### The `IScrimPlugin` Interface

```csharp
namespace Scrim.Plugins {
    public interface IScrimPlugin {
        string Name { get; }
        string Version { get; }
        string Author { get; }
        void Initialize(IScrimHost host);
        void Shutdown();
    }
}
```

### The `IScrimHost` Services API

When Scrim starts, it instantiates your plugin and calls `Initialize(IScrimHost host)`. The `host` gives you direct access to the live console engine:

```csharp
namespace Scrim.Plugins {
    public interface IScrimHost {
        BroadcastHub Broadcast { get; }
        IMetadataService Metadata { get; }
        IProfileManager ProfileManager { get; }
        SongRequestController SongRequests { get; }
    }
}
```

| Service | What You Can Access |
|---|---|
| **`host.Metadata`** | `CurrentMetadata`: Track title, artist, album, duration, elapsed position, album art bytes/URL.<br>`MetadataChanged`: Event triggered whenever a track changes or time advances. |
| **`host.Broadcast`** | `IsBroadcasting`: Whether the station is actively live.<br>`ActiveClientCount`: Current number of connected listeners.<br>`BroadcastingStateChanged`: Event triggered when broadcasting starts or stops. |
| **`host.ProfileManager`** | `CurrentProfile`: Access station name, port, theme, bitrate, audio format, and custom branding settings. |
| **`host.SongRequests`** | `GetLiveQueue()`: List of pending song requests and dedications submitted by web listeners.<br>Methods to approve or reject requests programmatically. |

---

## 7. Complete Working Example: Now Playing Webhook Plugin

Scrim ships with this exact reference plugin included out of the box in `plugins/DiscordNotifier/` (and compiled to `Plugins/DiscordNotifier.dll`). Broadcasters can enable it simply by configuring `Plugins/discord_config.json` or setting the `SCRIM_DISCORD_WEBHOOK` environment variable.

The source code below demonstrates how this plugin listens for song transitions and prints track announcements or posts them to a webhook:

```csharp
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Scrim.Metadata;
using Scrim.Plugins;

namespace Scrim.Plugin.DiscordNotifier {
    public class DiscordNotifierPlugin : IScrimPlugin {
        public string Name => "Discord Now Playing Notifier";
        public string Version => "1.0.0";
        public string Author => "Station DJ";

        private IScrimHost? _host;
        private static readonly HttpClient _http = new();
        private string _lastTrack = string.Empty;

        // Replace with your actual Discord Webhook URL if desired
        private const string WebhookUrl = "";

        public void Initialize(IScrimHost host) {
            _host = host;

            // Subscribe to live track changes from Spotify/media players
            _host.Metadata.MetadataChanged += OnMetadataChanged;
            _host.Broadcast.BroadcastingStateChanged += OnBroadcastingStateChanged;

            Console.WriteLine($"[{Name}] Initialized successfully!");
        }

        private void OnMetadataChanged(object? sender, MediaMetadata meta) {
            if (string.IsNullOrWhiteSpace(meta.Title) || meta.Title.StartsWith("Awaiting", StringComparison.OrdinalIgnoreCase)) {
                return;
            }

            string trackKey = $"{meta.Title} - {meta.Artist}";
            if (trackKey == _lastTrack) {
                return;
            }
            _lastTrack = trackKey;

            Console.WriteLine($"[{Name}] Now Playing: {meta.Title} by {meta.Artist} (Duration: {meta.Duration:mm\\:ss})");

            if (!string.IsNullOrEmpty(WebhookUrl)) {
                Task.Run(() => SendDiscordNotificationAsync(meta.Title, meta.Artist, meta.Album, meta.AlbumArtUrl));
            }
        }

        private void OnBroadcastingStateChanged(object? sender, bool isLive) {
            Console.WriteLine($"[{Name}] Broadcast state changed: {(isLive ? "ON AIR 🔴" : "OFF AIR ⚪")}");
        }

        private async Task SendDiscordNotificationAsync(string title, string artist, string album, string? artUrl) {
            try {
                var json = $$"""
                {
                    "content": "🎶 **Now Playing on Stream:** {{title}} by **{{artist}}**",
                    "embeds": [{
                        "title": "{{title}}",
                        "description": "Artist: {{artist}}\nAlbum: {{album}}",
                        "color": 3447003
                    }]
                }
                """;

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                await _http.PostAsync(WebhookUrl, content);
            } catch (Exception ex) {
                Console.WriteLine($"[{Name}] Failed to send webhook: {ex.Message}");
            }
        }

        public void Shutdown() {
            if (_host != null) {
                _host.Metadata.MetadataChanged -= OnMetadataChanged;
                _host.Broadcast.BroadcastingStateChanged -= OnBroadcastingStateChanged;
            }
            Console.WriteLine($"[{Name}] Shutdown complete.");
        }
    }
}
```

---

## 8. Creating Custom UI Cards (`IUiExtension`)

If you want your plugin to add a custom draggable card into the Scrim Studio dashboard, implement the `IUiExtension` interface in addition to `IScrimPlugin`:

```csharp
using System;
using Scrim.Plugins;

namespace Scrim.Plugin.DiscordNotifier {
    public class DiscordNotifierPlugin : IScrimPlugin, IUiExtension {
        public string Name => "Discord Notifier";
        public string Version => "1.0.0";
        public string Author => "Station DJ";

        // IUiExtension implementation
        public string CardTitle => "Discord Webhook";
        public Type ComponentType => typeof(MyPluginCardComponent);

        public void Initialize(IScrimHost host) {
            // Setup logic
        }

        public void Shutdown() {
            // Teardown logic
        }
    }
}
```

Where `MyPluginCardComponent` is a standard Blazor component (`.razor` file).

---

## 9. Building and Deploying Your Plugin

### Step 1: Build in Release Mode
From your plugin project directory, run:

```powershell
dotnet build -c Release
```

### Step 2: Copy to Your Scrim Plugins Folder
Copy the output `.dll` file from your `bin/Release` folder into `%USERPROFILE%\.scrim\plugins\`:

```powershell
$dest = "$HOME\.scrim\plugins"
if (!(Test-Path $dest)) { New-Item -ItemType Directory -Path $dest -Force }
Copy-Item "bin\Release\net10.0-windows10.0.19041.0\Scrim.Plugin.DiscordNotifier.dll" -Destination $dest -Force
```

### Step 3: Launch Scrim
Start Scrim. The plugin loader will automatically:
1. Scan `%USERPROFILE%\.scrim\plugins\`.
2. Inspect and load `Scrim.Plugin.DiscordNotifier.dll`.
3. Instantiate your plugin and call `Initialize(host)`.

---

## 10. Troubleshooting & Tips

- **Target Framework Mismatch**: Make sure your project targets `net10.0-windows10.0.19041.0`. Targeting an older incompatible runtime may prevent the assembly from loading.
- **Checking Logs**: If a plugin fails to load due to a missing dependency or exception, check Scrim's log files located at:
  `%USERPROFILE%\.scrim\logs\`
- **Third-Party Dependencies**: If your plugin uses external NuGet packages (e.g. `Newtonsoft.Json` or specialized audio packages), ensure those dependent DLLs are also placed in `%USERPROFILE%\.scrim\plugins\` alongside your plugin DLL. Setting `<CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>` in your `.csproj` ensures these files are collected in your build output folder.

---

## 11. Built-in Reference Plugins Suite

Scrim ships with ten production-ready reference plugins in the `plugins/` directory and packages them directly into the default installer under `[INSTALLFOLDER]\Plugins\`:

| Plugin | Assembly | Purpose & Outputs |
|---|---|---|
| **Discord Notifier** | `DiscordNotifier.dll` | Sends rich embeds to Discord channels on track transitions and broadcast status updates (ON/OFF AIR). Configured via `discord_config.json` or `SCRIM_DISCORD_WEBHOOK`. |
| **OBS Studio Overlay** | `ObsOverlay.dll` | Exports live text files (`nowplaying.txt`, `artist.txt`, `title.txt`, `listeners.txt`, `status.txt`) and album art (`cover.jpg`) to `%USERPROFILE%\.scrim\obs\`. Also generates `overlay.html` for direct transparent OBS browser sources. |
| **Broadcast Session Logger** | `BroadcastLogger.dll` | Automatically records each live show into `%USERPROFILE%\.scrim\shows\Show_YYYY-MM-DD_HHmmss.md` with full track playlists, durations, and listener stats. Appends to `playlist_history.csv` for CSV export. |
| **Synchronized Lyrics Ticker** | `LyricsTicker.dll` | Queries the open LRCLIB API for time-synced lyrics and writes the current singing line in real-time to `%USERPROFILE%\.scrim\obs\lyrics.txt` and `lyrics.json` for OBS subtitles. |
| **Twitch Chat Bot & Song Requests** | `TwitchBot.dll` | Connects to Twitch chat via IRC WebSocket, responding to `!song`/`!nowplaying` and adding viewer `!request <song>` queries to the DJ queue. Configured via `twitch_config.json`. |
| **Stream Deck Hardware Companion** | `StreamDeckCompanion.dll` | Hosts a local REST API on `http://127.0.0.1:5055/api/` for Stream Deck, Touch Portal, and macro keys (play/pause, skip, mic mute, live status). Configured via `streamdeck_config.json`. |
| **Last.fm & ListenBrainz Scrobbler** | `LastFmScrobbler.dll` | Automatically scrobbles played tracks to Last.fm and ListenBrainz when songs pass 50% completion or 4 minutes. Configured via `lastfm_config.json`. |
| **Station Jingle & Sweeper Player** | `JingleSweeper.dll` | Plays station IDs and audio drops from `%USERPROFILE%\.scrim\jingles\*.wav` at periodic intervals (e.g. every 15m) into the audio mixer with ducking. Configured via `jingle_config.json`. |
| **Live Stream Audio Archiver** | `StreamArchiver.dll` | Captures live broadcast stream frames while ON AIR and writes continuous MP3/AAC audio files directly into `%USERPROFILE%\.scrim\recordings\`. Configured via `archiver_config.json`. |
| **MIDI Controller Surface** | `MidiController.dll` | Maps USB/MIDI hardware faders (CC 7 volume) and pads (notes 36-39 for mic mute, broadcast toggle, skip, play/pause) to live Scrim controls. Configured via `midi_config.json`. |

---

## 12. Building Plugins in Python

Scrim supports plugins written entirely in **Python** without compiling C# code or requiring the .NET SDK. Python plugins run as isolated child processes communicating with Scrim over bidirectional JSON-RPC via standard I/O (`stdin`/`stdout`).

### Why Write Plugins in Python?
- **Crash Isolation**: Errors, exceptions, or hangs in Python scripts cannot terminate the Scrim desktop application or interrupt the live audio broadcast.
- **Full `pip` Ecosystem**: Use any standard library or third-party package (e.g. `requests`, `websockets`, `discord.py`, `pydantic`, `pandas`) without library version conflicts.
- **Virtual Environment Support**: Each plugin can bundle its own `.venv` or `venv` directory containing its required packages.
- **First-Class UI Integration**: Python plugins appear in the Scrim Dashboard with a blue **Python** badge, full ON/OFF toggles, version info, and error reporting.

---

### Python Plugin Structure

Scrim discovers Python plugins in `%USERPROFILE%\.scrim\plugins\` (or `<InstallDir>\Plugins\`) in two formats:

#### Format A: Packaged Plugin Folder (Recommended)
```
%USERPROFILE%\.scrim\plugins\
  └── MyWebhookNotifier\
      ├── plugin.json          # Plugin metadata & entry script
      ├── main.py              # Main Python script
      ├── requirements.txt     # Optional pip dependencies
      └── .venv\               # Optional dedicated virtual environment
```

#### Format B: Standalone Python Script
A single `.py` file placed directly in `%USERPROFILE%\.scrim\plugins\my_quick_script.py`. Scrim will automatically derive the plugin name from the file name and run it using system Python.

---

### The `plugin.json` Manifest

When using a folder structure, create a `plugin.json` file in the root of your plugin directory:

```json
{
  "id": "my-webhook-notifier",
  "name": "Discord Webhook Notifier",
  "version": "1.0.0",
  "author": "DJ Alex",
  "entry": "main.py",
  "description": "Sends rich Discord embeds whenever tracks change or the broadcast goes live."
}
```

| Field | Type | Description |
|---|---|---|
| `id` | `string` | Unique identifier for state persistence (enabled/disabled). |
| `name` | `string` | Human-readable name displayed in the Scrim Dashboard. |
| `version` | `string` | Version string (e.g. `1.0.0`). |
| `author` | `string` | Author name or studio handle. |
| `entry` | `string` | Script entrypoint (defaults to `main.py`). |
| `description` | `string` | Optional summary of the plugin's functionality. |

---

### Using the `scrim.py` SDK

Scrim includes a lightweight Python helper module named `scrim.py` (packaged directly in Scrim's `Plugins/` directory and available on `PYTHONPATH`).

To build a plugin, subclass `ScrimPlugin` and implement any of the lifecycle hooks:

```python
from scrim import ScrimPlugin, TrackInfo

class MyNotifierPlugin(ScrimPlugin):
    def on_init(self, station_info: dict):
        """Called when Scrim initializes your plugin."""
        self.log(f"Initialized for station: {self.station_name} on port {self.port}")

    def on_metadata_changed(self, track: TrackInfo):
        """Triggered whenever a new song or track starts playing."""
        self.log(f"Now playing: {track.artist} - {track.title} ({track.album})")

    def on_broadcast_state_changed(self, is_live: bool):
        """Triggered when the DJ toggles ON AIR / OFFLINE."""
        status = "ON AIR" if is_live else "OFFLINE"
        self.log(f"Transmitter status changed to: {status}")

    def on_listener_count_changed(self, count: int):
        """Triggered when active listener count updates."""
        self.log(f"Current listeners: {count}")

    def on_shutdown(self):
        """Called when the plugin is turned OFF or Scrim exits."""
        self.log("Shutting down cleanly...")

if __name__ == "__main__":
    MyNotifierPlugin().run()
```

---

### Python SDK Hook Reference

| Method / Hook | Parameters | Description |
|---|---|---|
| `on_init(station_info)` | `station_info: dict` | Contains `station_name` and `port`. |
| `on_metadata_changed(track)` | `track: TrackInfo` | Attributes: `track.title`, `track.artist`, `track.album`, `track.duration_seconds`. |
| `on_broadcast_state_changed(is_live)` | `is_live: bool` | `True` when broadcasting live, `False` when offline. |
| `on_listener_count_changed(count)` | `count: int` | Total connected HTTP audio stream listeners. |
| `on_song_requested(request)` | `request: dict` | Listener song request details. |
| `on_shutdown()` | None | Clean up network sessions, open files, or timers. |
| `self.log(message)` | `message: str` | Sends a log string back to Scrim's console and crash logs. |
| `self.update_metadata(artist, title, album)` | `artist: str, title: str, album: Optional[str]` | Requests Scrim to update active broadcast metadata. |

---

### Complete Example: Python Discord Webhook Notifier

Here is a complete, production-ready Python plugin that posts now-playing tracks to a Discord channel:

**`plugin.json`**:
```json
{
  "id": "python-discord-webhook",
  "name": "Python Discord Announcer",
  "version": "1.0.0",
  "author": "Radio Host",
  "entry": "main.py"
}
```

**`main.py`**:
```python
import urllib.request
import json
from scrim import ScrimPlugin, TrackInfo

# Set your Discord incoming webhook URL here
WEBHOOK_URL = "https://discord.com/api/webhooks/YOUR_WEBHOOK_URL_HERE"

class DiscordAnnouncer(ScrimPlugin):
    def send_discord_embed(self, title: str, description: str, color: int = 0x8b5cf6):
        if "YOUR_WEBHOOK_URL_HERE" in WEBHOOK_URL:
            return

        payload = {
            "embeds": [{
                "title": title,
                "description": description,
                "color": color,
                "footer": {"text": f"Broadcast via {self.station_name}"}
            }]
        }

        try:
            req = urllib.request.Request(
                WEBHOOK_URL,
                data=json.dumps(payload).encode("utf-8"),
                headers={"Content-Type": "application/json", "User-Agent": "ScrimPythonPlugin/1.0"}
            )
            with urllib.request.urlopen(req, timeout=5) as resp:
                pass
        except Exception as ex:
            self.log(f"Failed to send Discord webhook: {ex}")

    def on_broadcast_state_changed(self, is_live: bool):
        if is_live:
            self.send_discord_embed(
                title="🔴 Station is NOW LIVE!",
                description=f"Tune in live now at http://localhost:{self.port}/stream",
                color=0xef4444
            )
        else:
            self.send_discord_embed(
                title="⚪ Station is OFFLINE",
                description="Thanks for listening!",
                color=0x64748b
            )

    def on_metadata_changed(self, track: TrackInfo):
        if not track.title and not track.artist:
            return
        
        self.send_discord_embed(
            title="🎵 Now Playing",
            description=f"**{track.artist}** — {track.title}\n*Album: {track.album or 'Single'}*",
            color=0x8b5cf6
        )

if __name__ == "__main__":
    DiscordAnnouncer().run()
```

---

### Managing Dependencies & Virtual Environments

If your plugin requires third-party packages (e.g. `requests`, `websockets`):

1. Open PowerShell and navigate into your plugin folder:
   ```powershell
   cd "$HOME\.scrim\plugins\MyWebhookNotifier"
   ```
2. Create a virtual environment named `.venv`:
   ```powershell
   python -m venv .venv
   ```
3. Install your dependencies into that virtual environment:
   ```powershell
   .\.venv\Scripts\pip install requests
   ```
4. Scrim will automatically detect `.venv\Scripts\python.exe` and execute your plugin using its dedicated environment and dependencies!


