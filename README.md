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
- **3-Column Drag & Drop Interface**: Customize your studio layout with 11 reorderable cards (Audio Routing, Network & Internet, Master VU, Voice FX, Song Queue, Live Station Chat, Encoding, Media Player, Soundboard, Website Branding, and Playlist).
- **Integrated Broadcast Soundboard**: Built-in sound effects (Airhorn, Applause, Rimshot, etc.) and custom `.wav` files mix directly into the live broadcast stream with analog VU needle reaction while simultaneously playing in DJ headphones.
- **Proportional Column Layouts**: Toggle between **Studio** (Little-Big-Little) and **Equal** (1fr-1fr-1fr) sizing modes.
- **Synchronized ON AIR & Transmitter (TX)**: Visual status badges and real-time audio transmission indicators keep you informed when sound is flowing.
- **Calibrated Stereo dB VU Meters**: High-frequency analog-style response bars with precise numeric decibel readouts (`-40 dB` to `0.0 dB`).

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
- **Anonymous Live Chat**: Real-time discussion between listeners and broadcaster without signups or accounts; verified `[HOST]` badges and broadcaster pause/clear controls.
- **4 Selectable Visualizers**: Choose between Neon Frequency Spectrum, Dynamic Waveform, Glowing Retro VU Meter, or Pulsing Sound Bars.
- **Listener Song Requests & Dedications**: Remote listeners can submit song requests with free-form dedications (e.g. for a friend, partner, or crew), visible in the live queue and the DJ's moderation card.

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

- 🚀 **[Post-Installation & User Guide](docs/post-install-guide.md)**: Comprehensive end-user handbook—from running the installer, setting up FFmpeg, 1-click virtual audio routing, and going on air to station chat, custom banners, and sharing links.
- 🔒 **[Easy Streaming Guide (Caddy & Free HTTPS)](docs/easy-streaming-guide.md)**: Beginner-friendly guide to streaming without port numbers (`:4242`) using Caddy and free automatic Let's Encrypt SSL certificates.
- 📖 **[User & Feature Guide](docs/user-guide.md)**: Complete walkthrough of all 11 console cards, real-time live chat, virtual audio, mic controls, and web player options.
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

The resulting installer (`bin/ScrimSetup-v1.0.0.msi`) bundles all dependencies, driver payloads, and offline documentation (`docs/` and `README.md`) for 1-click per-user installation (`%LOCALAPPDATA%\Programs\Scrim`) without requiring administrator elevation, complete with desktop and Start Menu shortcuts and in-place upgrade support.

---

## Software Updates & Auto-Update

Scrim includes native integration with the NilonSoft Client Update API (`https://www.nilonsoft.com`):

- **Startup Update Checks**: Every time Scrim is launched, it asynchronously queries the update endpoint in the background without slowing down console startup.
- **Daily Background Checks**: When Scrim is not running, a Windows Scheduled Task (`Scrim Daily Update Check`) executes `Scrim.exe --check-updates-silent` once per day. If an update is detected, it alerts the user; if up to date, it silently exits immediately.
- **In-Place Auto-Update**: When an update is accepted, Scrim downloads the latest `.msi` package, spawns a detached updater script that closes running Scrim instances, executes `msiexec /i <installer> /passive /norestart` in the per-user scope (no UAC elevation required), and automatically restarts Scrim.

---

## License

This project is licensed under the [MIT License](LICENSE).
