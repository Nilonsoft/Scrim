# Scrim Broadcast Console

[![Windows](https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011-blue?logo=windows)](https://microsoft.com)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-purple?logo=dotnet)](https://dotnet.microsoft.com)
[![License](https://img.shields.io/badge/License-MIT-green)](LICENSE)

**Scrim** is a high-performance Windows desktop radio broadcast console and streaming station built with C# and Blazor Hybrid. It intercepts desktop or application audio in real time, overlays an on-air DJ microphone with automatic sidechain ducking, provides granular DSP voice changers, transcodes streams on the fly into MP3, AAC, Opus, or lossless FLAC, and broadcasts via an embedded HTTP streaming server and interactive HTML5 web player.

---

## Key Features

### ⚡ 1-Click Virtual Audio Setup
- **Integrated WHQL Driver**: Bundles the official Microsoft-certified VB-Audio Virtual Cable driver for silent background app capture.
- **Zero-Configuration Routing**: Programmatically sets Windows default playback to the virtual cable via CoreAudio policy (`IPolicyConfig`), automatically streaming all PC audio and music apps into Scrim without manual Windows Mixer setup.
- **Automatic Microphone Virtual Routing**: Loops your live microphone into the virtual capture device (`CABLE Input`) and broadcast stream with clear console status indicators, allowing external recording software and games to capture both voice and music.
- **1-Click Speaker Restoration**: Safely restores your physical speakers/headphones at any time with a single click.

### 🎚️ Modular Studio Console
- **3-Column Drag & Drop Interface**: Customize your studio layout with 12 reorderable cards (Audio Routing, Pro DJ Mixer & EQ, Network & Internet, Master VU, Voice FX, Song Queue, Live Station Chat, Encoding, Media Player, Soundboard, Website Branding, and Playlist).
- **Integrated Broadcast Soundboard**: Built-in sound effects (Airhorn, Applause, Rimshot, etc.) and custom `.wav` files mix directly into the live broadcast stream with analog VU needle reaction while simultaneously playing in DJ headphones.
- **Proportional Column Layouts**: Toggle between **Studio** (Little-Big-Little) and **Equal** (1fr-1fr-1fr) sizing modes.
- **Synchronized ON AIR & Transmitter (TX)**: Visual status badges and real-time audio transmission indicators keep you informed when sound is flowing.
- **Calibrated Stereo dB VU Meters**: High-frequency analog-style response bars with precise numeric decibel readouts (`-40 dB` to `0.0 dB`).

### 🎛️ Professional DJ Stereo Mixer & Full EQ
- **Hardware-Class DJ Console Strip**: Dedicated module mimicking industry-standard DJ software (Pioneer DJM, Traktor, Serato) with real-time audio DSP. Details in [Pro DJ Mixer & EQ Guide](docs/pro-dj-mixer-and-eq.md).
- **Stereo Music Deck & Channel Separation**: Independent Left/Right channel panning (constant-power law), Mid-Side Stereo Width Expander (0% True Mono sum to 200% Super-Wide soundstage), channel trim gain (-12dB to +12dB), and channel mute.
- **Microphone Channel Strip**: Dedicated mic gain, stereo pan, 80Hz rumble high-pass filter to eliminate desk bumps/plosives, and talkover status.
- **3-Band DJ Isolator Mode**: Low (20-300Hz), Mid (300-2.5kHz), and High (2.5k-20kHz) faders (-26dB to +6dB) with instant **LOW KILL**, **MID KILL**, and **HIGH KILL** buttons for drop transitions and acapella/vocal isolation.
- **DJ Sound Color Sweep Filter**: Bi-polar filter sweeping from resonant Low-Pass Filter (cuts highs) to neutral bypass to resonant High-Pass Filter (cuts lows).
- **10-Band ISO Graphic Equalizer**: 10 standard ISO frequency bands (31Hz to 16kHz) with precision vertical sliders.
- **Real-Time SVG Frequency Response Curve**: Live visual curve showing exact filter frequency shapes across 20Hz - 20kHz with gradient fill.
- **Analog Soft-Clip Limiter**: Transparent hyperbolic tangent (`tanh`) saturation preventing harsh digital clipping even under extreme EQ boosts.
- **Pro DJ Presets**: Instant 1-click presets for *Flat / Bypass*, *Club / EDM Bass*, *Radio DJ Voice*, *Rock & Live*, *Warm Vinyl & Acoustic*, *Hip-Hop Thump*, *Lofi Lounge*, and *Treble Sparkle*.

### 🎙️ Live Microphone & Voice FX Engine
- **Push-to-Talk & Push-to-Mute**: Flexible hardware-style microphone switching.
- **Headphone Sidetone (Hear Myself)**: Ultra-low latency (25ms) local monitoring so you can hear your voice in real time.
- **Off-Air Mic Check**: Test microphone levels, proximity, and voice changers in your headphones without broadcasting to listeners.
- **DSP Voice Changers**: Real-time granular pitch shifting and vocal formant filtering with presets for **Anime Girl**, **Woman**, **Man**, **Robot**, **Radio**, and **Alien**, plus Lua/JSON/DLL plugin expansion.
- **Sidechain Ducking**: Automatically attenuates background music by **-14 dB** with broadcast-standard attack (20ms) and release (250ms) curves when speaking.

### 📻 Zero-Latency Live Transcoding
- **Multi-Format Support**: Broadcast in universal **MP3**, high-efficiency **AAC**, speech-optimized **Opus**, or bit-perfect lossless **FLAC**.
- **On-The-Fly Hot-Swapping**: Switch audio codecs and bitrates (`64k` to `320k`) instantly while live on the air without dropping listener connections.

### 🌐 Network & Internet Broadcasting
- **Local Wi-Fi / LAN Sharing**: Share direct playback links for devices on your home network.
- **Automatic UPnP Port Forwarding**: Effortlessly broadcast over the internet without manual router port configuration.
- **Configurable Stream Mount Path**: Customize the audio stream endpoint (e.g., `/live`, `/radio`, `/station`) directly in the console with automatic `/stream` fallback compatibility and live copy shortcuts.
- **Local Network Only Restriction (Private Stream)**: Restrict broadcasts exclusively to home Wi-Fi/LAN listeners. When enabled, remote internet visitors see a clean "🔒 Private Stream" banner with complete lockout of audio, metadata, and chat.
- **Reverse Proxy & HTTPS Support**: Checkboxes for **"Use HTTPS"** (generates `https://` links) and **"Using Reverse Proxy"** (strips port numbers for Caddy, Nginx, or Cloudflare Tunnel setups) for seamless copy/paste and one-click opening.
- **Custom Domain / Host Override**: Enter custom domain names or DDNS hostnames (e.g. `radio.mydomain.com`) that propagate across all share links.
- **Direct Media Player Endpoints**: Native stream endpoints (`/<mount>` and `/stream`) and `.m3u` / `.pls` playlists for direct listening in VLC, Winamp, foobar2000, and mobile streaming apps.

### 🎨 Modern Responsive Web Player & Live Chat
- **Dark Glassmorphism Interface**: Real-time track metadata and album art synchronized via Windows System Media Transport Controls (SMTC).
- **Dynamic Ambient Backdrop**: Real-time canvas palette extraction dynamically glows the background with album art colors; toggleable in the Scrim console so station owners can prioritize their custom themes.
- **Party / TV Full-Screen Mode**: Cinema-ready TV viewing experience with massive album artwork, enlarged visualizers, clean typography, and auto-hiding controls on mouse idle.
- **Live Synced & Plain Lyrics Drawer**: Powered by the open LRCLIB API with automatic line-by-line sync and smooth scrolling tracking current audio playback.
- **Station Schedule & Broadcaster Bio**: Sleek modal displaying host biography, live broadcast schedule, and Discord, Twitch, and Twitter/X social links.
- **DJ Live Polls & Track Battles**: Broadcasters can launch live questions or 1-click track battles directly from the console, displaying live percentage bars and collecting instant listener votes.
- **Anonymous Live Chat**: Real-time discussion between listeners and broadcaster without signups or accounts; verified `[HOST]` badges and broadcaster pause/clear controls.
- **4 Selectable Visualizers**: Choose between Neon Frequency Spectrum, Dynamic Waveform, Glowing Retro VU Meter, or Pulsing Sound Bars.
- **Listener Song Requests & Dedications**: Remote listeners can submit song requests with free-form dedications (e.g. for a friend, partner, or crew), visible in the live queue and the DJ's moderation card.

### 📻 SAM Broadcaster PRO Modular Suite
- **Modular On-Demand Workflow**: 7 professional modular cards inspired by SAM Broadcaster PRO. Left off by default to maintain an uncluttered studio, broadcasters can add, reposition, or close each card individually via **+ Add / Manage Cards**.
- **Dual Decks & Crossfader (`card-dual-deck`)**: Deck A and Deck B with individual CUE points, pitch sliders (-8% to +8%), and equal-loudness crossfader with 1-click Auto-DJ crossfade.
- **Automated Show Clock & Scheduler (`card-event-scheduler`)**: Automated sweeps, hourly station IDs, chat announcements, theme changes, and polls on hourly, daily, or minute intervals.
- **Voice-Tracking Transition Recorder (`card-voice-tracking`)**: Record DJ voice clips with automated music bed ducking over track intros/outros before air.
- **Multi-Encoder & Relay Rack (`card-multi-encoder`)**: Broadcast multiple simultaneous stream formats (MP3 128k, AAC+ 64k mobile, FLAC lossless) or relay to remote Icecast/SHOUTcast servers.
- **Live Listener Connections & IP Inspector (`card-listener-inspector`)**: Real-time listener connection table with IP addresses, mount points, session duration, bandwidth transferred, player types, and 1-click disconnect/kick.
- **Dead-Air Auto-Recovery & Alarm (`card-silence-recovery`)**: Master bus silence watchdog with configurable threshold, live countdown timer, and automated backup jingles/alarms.
- **Music Rotation Rules & Category Bins (`card-rotation-rules`)**: Categorical crates (Heavy, Medium, Gold Classics, Sweepers), artist separation limits, and 1-hour clockwheel queue generator.

### 🎵 Local Music Playback & Playlist Management Console
- **Console-Native Playback**: Play your local music library directly from Scrim without needing third-party players (Spotify, VLC, Winamp).
- **Pro DJ Audio Pipeline Integration**: Local music flows into Scrim's Pro DJ audio engine — the 10-Band ISO EQ, DJ Color sweep filter, stereo pan/width, and microphone auto-ducking (-14dB talkover) apply identically to local files.
- **Comprehensive Format Support**: Plays `.mp3` (with ID3 tags), `.wav`, `.flac`, `.m4a`, `.aac`, `.wma`, and `.aiff` natively.
- **Interactive Console Player**: Now Playing strip with animated visualizer, interactive seek scrubber, transport controls (Play/Pause, Stop, Prev, Next), shuffle, repeat (Off / All / 1), and local monitor volume.
- **Playlist Workstation**: Add files, scan folders, filter with real-time search, move tracks up/down, export/import `.m3u` and `.json` playlists, and 1-click cue to Deck A or Deck B. See [User Guide](docs/user-guide.md#14-local-music-playback--playlist-management-console).

### 🎛️ Stream Relaying & Multi-DJ Party Hub
- **External & Sister-Station Relay Ingest**: Ingest live MP3, AAC, Ogg, and FLAC streams from remote radio stations or co-hosts via background FFmpeg decoders with auto-reconnect.
- **Scrim-to-Scrim Peer Auto-Detection**: Auto-detects peer Scrim broadcasts, pulling remote DJ names, bios, and avatars into the console and web player.
- **Pre-Fade Listen (PFL) Headphone Cueing**: Dedicated `🎧 CUE` button to preview and beatmatch remote streams in headphones over secondary WASAPI audio devices before going live.
- **Automated B2B DJ Crossfader**: 1-click `⚡ Hand-Off to Guest` and `⚡ Take Back Decks` executing smooth 8-second S-curve volume transitions between host decks and guest streams.
- **Green Room Talkback Intercom**: Dedicated `🎙️ Talkback (Off-Air)` isolates host microphone from the master broadcast for private off-air coordination with guest performers.
- **Stream Health & Auto-Fallback**: Live buffer depth telemetry (`(1.2s buffer)`) and instant auto-fallback restoring host volume to 100% if a guest stream drops.
- **Interactive Web Player Stage Presence**: "Now on the Decks" dual-avatar badge with glowing pulse rings, DJ hand-off toast banners, guest DJ profile popover modal, and real-time `🎉 PARTY` celebration pyro/confetti bursts. See [v1.0.11 Feature Guide](docs/v1.0.11-features-guide.md#1-stream-relaying--multi-dj-party-hub).

### 🔌 Extensible C# & Python Plugin System
- **Dual Runtime Support**: Dynamically discover and execute compiled .NET class libraries (`.dll`) and pure Python plugins (`.py` or packaged folders with `plugin.json`).
- **Crash-Resilient Worker Bridge**: Python plugins run in isolated child processes communicating over bidirectional JSON-RPC, protecting live radio broadcasts from script crashes or hangs.
- **Dedicated Virtual Environments**: Each Python plugin can run in its own `.venv` to leverage third-party `pip` packages (Discord webhooks, Twitch bots, databases, analytics).
- **Interactive Console Management**: Toggle plugins ON/OFF dynamically from the Scrim Dashboard with real-time runtime badges, version tags, and integrated logging.

---

## Prerequisites

- **Windows 10 / 11 (64-bit)**
- **FFmpeg**: Required for on-the-fly audio transcoding (MP3, AAC, FLAC, Opus). Scrim automatically detects FFmpeg on installation and launch; if missing, an in-app guidance modal provides 1-click `winget` installation, package manager commands, and manual setup instructions.
  ```powershell
  winget install ffmpeg
  ```
- **Python 3.12 (Recommended / Optional)**: Required if you wish to run Python plugins (`.py` scripts or packages). **Python 3.12** is strongly preferred for full compatibility with PyTorch, AMD ROCm acceleration, and AI/machine learning audio plugins. Scrim automatically detects `python.exe`, `python3.exe`, or `py.exe` on your system `PATH`, or a dedicated `.venv` virtual environment inside the plugin's folder:
  ```powershell
  winget install Python.Python.3.12
  ```

---

## Quick Start

1. **Clone and Build**:
   ```powershell
   git clone https://github.com/Nilonsoft/Scrim.git
   cd Scrim
   dotnet build src/Scrim.csproj
   ```
2. **Run Scrim**:
   ```powershell
   dotnet run --project src/Scrim.csproj
   ```
3. **Set Up Virtual Audio**: Click **`[ ⚡ Setup Virtual Device ]`** in the **Audio Routing** card to automatically configure audio routing.
4. **Go On Air**: Click **`🔴 ON AIR`** to start broadcasting!

---

## Documentation

Comprehensive guides, specifications, and architecture documents are located in the [`docs/`](docs/) directory:

- 🌟 **[Release & Feature Guide (v1.0.11)](docs/v1.0.11-features-guide.md)**: Details on Stream Relaying & Multi-DJ Party Hub, Backstage DJ / Green Room Private Chat, Audio Visualizer Reactor Mode Customization, Dedicated Admin Screen, High-DPI UI Scaling, Collapsible Cards, Venue Profile Import/Export, and Overnight Auto-Recovery.
- 📋 **[Release Notes (v1.0.9)](docs/patch-notes-v1.0.9.md)**: What's new in v1.0.9 (Local Music Playback & Playlist Management Console).
- 🚀 **[Post-Installation & User Guide](docs/post-install-guide.md)**: Comprehensive end-user handbook—from running the installer, setting up FFmpeg, 1-click virtual audio routing, and going on air to station chat, custom banners, and sharing links.
- 📻 **[SAM Broadcaster PRO Modular Suite Guide](docs/sam-pro-features-guide.md)**: Detailed breakdown of the 7 modular cards (Dual Decks, Event Scheduler, Voice Tracking, Multi-Encoder Rack, Listener IP Inspector, Dead-Air Recovery, and Music Rotation Rules).
- 🔒 **[Easy Streaming Guide (Caddy & Free HTTPS)](docs/easy-streaming-guide.md)**: Beginner-friendly guide to streaming without port numbers (`:4242`) using Caddy and free automatic Let's Encrypt SSL certificates.
- 🎛️ **[Pro DJ Stereo Mixer & Full EQ Guide](docs/pro-dj-mixer-and-eq.md)**: Hardware-grade channel strip routing (Trim, Pan, Mid-Side Stereo Width Expander 0% to 200%), 3-Band Isolator with Low/Mid/High Kills, DJ Sound Color Filter (HPF/LPF), 10-Band ISO Graphic EQ, real-time SVG response curves, and analog soft-clipping limiter.
- 📖 **[User & Feature Guide](docs/user-guide.md)**: Complete walkthrough of all console cards, real-time live chat, virtual audio, mic controls, and web player options.
- 🎨 **[Web Player Themes & Custom Theme Guide](docs/web-themes.md)**: Guide to built-in themes (Dark, Goth, Pink, Flowers, Light, Rock & Roll) and creating user JSON themes in `~/.scrim/themes/`.
- 🌐 **[Network & Outside Broadcasting Guide](docs/network-broadcasting.md)**: Details on local LAN sharing, UPnP port forwarding, custom domain overrides, and media player endpoints.
- 📐 **[Project Specification & Architecture](docs/spec.md)**: Deep technical dive into the WASAPI loopback capture, mixing pipelines, and transcoding engine.
- 🔌 **[Plugin Development Guide](docs/plugin-development.md)**: Complete guide to authoring custom Scrim plugins in **C# (.NET assemblies)** and **Python** (via isolated subprocess worker bridge and `scrim.py` SDK), with details on the ten built-in reference plugins: **Discord Notifier**, **OBS Overlay**, **Show Logger**, **Lyrics Ticker**, **Twitch Bot**, **Stream Deck Companion**, **Last.fm Scrobbler**, **Jingle Sweeper**, **Stream Archiver**, and **MIDI Controller Surface**.

---

## Building the Installer

Scrim includes automated WiX Toolset v5 scripts to generate a standalone Windows MSI installer package:

```powershell
pwsh -NoProfile -File ./build-installer.ps1
```

The resulting installer (`bin/ScrimSetup-v1.0.9.msi`) bundles all dependencies, driver payloads, and offline documentation (`docs/` and `README.md`) for 1-click per-user installation (`%LOCALAPPDATA%\Programs\Scrim`) without requiring administrator elevation, complete with desktop and Start Menu shortcuts and in-place upgrade support.

---

## Software Updates & Auto-Update

Scrim includes native integration with the NilonSoft Client Update API (`https://www.nilonsoft.com`):

- **Startup Update Checks**: Every time Scrim is launched, it asynchronously queries the update endpoint in the background without slowing down console startup.
- **Daily Background Checks**: When Scrim is not running, a Windows Scheduled Task (`Scrim Daily Update Check`) executes `Scrim.exe --check-updates-silent` once per day. If an update is detected, it alerts the user; if up to date, it silently exits immediately.
- **In-Place Auto-Update**: When an update is accepted, Scrim downloads the latest `.msi` package, spawns a detached updater script that closes running Scrim instances, executes `msiexec /i <installer> /passive /norestart` in the per-user scope (no UAC elevation required), and automatically restarts Scrim.

---

## License

This project is licensed under the [MIT License](LICENSE).
