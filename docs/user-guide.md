# Scrim Broadcast Station — User & Setup Guide

Welcome to **Scrim**! Scrim turns your Windows PC into a personal live radio broadcast station. You can capture and stream music from any app (Spotify, Chrome, media players, or games), talk on air with your microphone, use real-time voice effects, play soundboard clips, and broadcast high-quality live audio to your friends, community, or local network.

---

## ⚡ Quick Start in 3 Easy Steps

1. **Step 1: Check FFmpeg**
   - Scrim uses FFmpeg for high-quality audio streaming (MP3, AAC, FLAC, Opus).
   - If Scrim displays an alert that FFmpeg is missing, open PowerShell and run:
     ```powershell
     winget install ffmpeg
     ```
   - Restart Scrim once installed and it will detect FFmpeg automatically.

2. **Step 2: 1-Click Audio Routing**
   - In the **Audio Routing** card, click **`[ ⚡ Setup Virtual Device ]`**.
   - If prompted by Windows, accept to install the included virtual audio driver.
   - All PC audio (Spotify, games, browser) will automatically route into Scrim cleanly without echo!

3. **Step 3: Go On Air!**
   - Click the glowing **`🔴 ON AIR`** button at the top of the console.
   - Click **`Share Stream`** in the top bar to copy your station link and share it with listeners!

---

## Table of Contents

1. [Quick Start Guide (Get On Air in 60 Seconds)](#1-quick-start-guide)
2. [Audio Routing & Virtual Audio Setup](#2-audio-routing--virtual-audio-setup)
3. [Master Broadcast & Transmitter (TX) Meters](#3-master-broadcast--transmitter-tx-meters)
4. [DJ Microphone, Sidetone & Voice Changers](#4-dj-microphone-sidetone--voice-changers)
5. [Live Encoding & Zero-Latency Hot-Swapping](#5-live-encoding--zero-latency-hot-swapping)
6. [Network & Internet Broadcasting](#6-network--internet-broadcasting)
7. [Responsive Web Player & Visualizers](#7-responsive-web-player--visualizers)
8. [Real-Time Anonymous Web Chat & Broadcaster Controls](#8-real-time-anonymous-web-chat--broadcaster-controls)
9. [Interactive Song Requests Queue & Dedications](#9-interactive-song-requests-queue--dedications)
10. [Studio Soundboard & Custom Sounds](#10-studio-soundboard--custom-sounds)
11. [Persistent Per-Song Reactions & Session Song History](#11-persistent-per-song-reactions--session-song-history)
12. [Console Customization & Profile Persistence](#12-console-customization--profile-persistence)
13. [Website Branding, Custom Banners & Header Images](#13-website-branding-custom-banners--header-images)

---

## 1. Quick Start Guide

1. **Launch Scrim**: Open the application via the Start Menu or Desktop shortcut.
2. **Audio Setup (1-Click)**:
   - In the **Audio Routing** card, click **`[ ⚡ Setup Virtual Device ]`**.
   - If prompted by Windows UAC, accept to install the included WHQL-certified VB-Audio Cable driver.
   - Scrim automatically sets up the virtual audio device, sets Windows audio to route through it, and prepares the capture engine.
3. **Start Broadcasting**:
   - Click the glowing **`🔴 ON AIR`** button or **`[ ⚡ START BROADCAST ]`** in the top bar or Encoding card.
   - Both indicators will light up synchronized in glowing red (`ON AIR`) and emerald green (`TX`).
4. **Share Your Station**:
   - Click **`Share Stream`** in the top bar to get your local network or internet link.
   - Open the link on your phone, tablet, or another computer to listen to the live stream!

---

## 2. Audio Routing & Virtual Audio Setup

### ⚡ 1-Click Virtual Audio Setup
Traditional broadcasting software requires configuring complex third-party audio cables and digging through Windows menus. Scrim automates the entire process into a single click:
- **Automatic Driver Deployment**: Checks for the Microsoft WHQL-certified VB-Audio Virtual Cable driver. If missing, silently installs it from the bundled installer (`VBCABLE_Setup_x64.exe`).
- **Console Capture Binding**: Instantly connects Scrim's loopback capture engine to the virtual cable (`CABLE Input`).
- **Windows CoreAudio Handover**: Programmatically routes Windows default playback audio to the virtual cable via `IPolicyConfig`. All PC audio, browser sound, Spotify, games, and media players automatically feed directly into Scrim without manual routing.
- **Automatic Microphone Virtual Routing**: When a virtual capture device (e.g. `CABLE Input`) is selected, Scrim automatically loops your active microphone through the virtual device via low-latency WASAPI output. Any external recording software, streaming app, or game capturing the virtual device receives your live voice and music combined, with clear visual routing status in the console.
- **1-Click Speaker Restoration**: Whenever you want to switch back to your regular physical speakers or headphones (e.g. *Sound BlasterX G6*), simply click **`[ ↩ Restore Speakers ]`**.

### Application Window Selection & Volume
- **Application Window Dropdown**: Choose between `System Audio (All Apps / Master Desktop Mix)` or isolate a specific window (e.g. Spotify, Chrome, Discord).
- **Independent Stream Level**: Adjust the **App Volume on Stream** slider (0% to 150%) to calibrate how loud the application sounds on the broadcast stream without modifying your Windows desktop volume.
- **Instant Stream Mute**: Click `Mute` on the app volume slider to instantly silence application music on the broadcast stream without stopping the music on your PC.

### Local Speaker Audio Control
- **Play Locally & Stream**: Listen to your music through your PC speakers while simultaneously broadcasting.
- **Stream Only (Muted Locally)**: Silences the application on your physical PC speakers while Scrim captures and streams it cleanly. Eliminates room echo and acoustic feedback when speaking into your microphone.

---

## 3. Master Broadcast & Transmitter (TX) Meters

### Synchronized On-Air & Transmitter Badges
- **`🔴 ON AIR`**: Main broadcast toggle located in the top navigation bar. Pulsing red indicates your station is live.
- **`🟢 TX (Transmitter Active)`**: Visual transmission indicator showing that audio frames are actively encoding and transmitting over the network.
- **`START / STOP BROADCAST`**: Located in the Encoding & Broadcast card. All broadcast triggers stay 100% synchronized in real time.
- **`Broadcast on Open`**: Toggle checkbox located in the Encoding & Broadcast card. When enabled, Scrim automatically initiates broadcasting as soon as the desktop application opens, ideal for automated station setups.

### Calibrated Dual Stereo & Microphone Audio Meters
- **Stereo Master Peak Meters (L & R)**: High-frequency analog-style response bars displaying broadcast master levels in decibels (`-40 dB` to `0.0 dB`).
- **Live Numeric dB Readout**: Real-time numeric peak indicators showing exact volume levels.
- **Microphone Peak Meter (MIC)**: Dedicated cyan meter tracking microphone gain before sidechain ducking.

---

## 4. DJ Microphone, Sidetone & Voice Changers

### Microphone Control Modes
- **Push-to-Talk (PTT)**: Engages the microphone only while holding the talk button.
- **Push-to-Mute (Cough Button)**: Leaves the microphone open continuously, muting only when pressed.

### Real-Time Headphone Monitoring (Sidetone)
- Click **`[ 🎧 Hear Myself: ON ]`** on the Master VU card to enable ultra-low latency (25ms buffer) local microphone sidetone directly through your headphones.
- Hear your voice exactly as listeners hear it, including real-time voice changer effects.

### Off-Air Mic Check
- Click **`[ 🎙️ Test Mic (Off-Air) ]`** to test your microphone levels, proximity, and voice effects in your headphones.
- Scrim isolates your voice from the live stream channel: your listeners continue hearing station music without hearing your mic test.

### Built-In DSP Voice Changers
Transform your broadcast voice with low-latency granular pitch shifting, time-domain formant filtering, and modulation:
- **Anime Girl**: High-pitch shift (+7.2 semitones, 1.52x ratio), low-end chest cut (<320Hz), and harmonic presence boost (3.5kHz - 8kHz) for a kawaii anime voice.
- **Woman**: Natural upward pitch shift (+4.2 semitones) with low-end rumble attenuation.
- **Man**: Deep downward pitch shift (-4.5 semitones) with low-mid resonance enhancement for a baritone radio announcer tone.
- **Robot**: 50Hz metallic ring modulation.
- **Radio**: Vintage bandpass telephone/intercom filtering with soft clipping saturation.
- **Alien**: High-frequency frequency-modulated pitch flutter.
- **Custom Extensions**: Extensible via Lua scripts, JSON DSP effect chains, and .NET DLL plugins.

### Automatic Sidechain Ducking
- Automatically attenuates background music by **-14 dB** the instant you speak into the microphone.
- Smooth, broadcast-standard attack (20ms) and release (250ms) curves guarantee professional radio transitions without audio pumping.

---

## 5. Live Encoding & Zero-Latency Hot-Swapping

### Supported Codecs
- **MP3 (MPEG Layer III)**: Universal compatibility across all web browsers and media players (64k to 320k CBR).
- **AAC (Advanced Audio Coding)**: High-efficiency, crisp audio encoding via Windows Media Foundation.
- **Opus**: Ultra-low latency, pristine speech and music clarity.
- **FLAC (Free Lossless Audio Codec)**: Bit-perfect 16-bit 44.1kHz lossless streaming.

### Instant On-The-Fly Hot-Swapping
- Switch between **MP3, AAC, Opus, or FLAC** and change bitrates (`64k`, `96k`, `128k`, `192k`, `256k`, `320k`) live while on the air!
- Scrim's decoupled producer-consumer architecture dynamically swaps the active encoder pipeline without disconnecting connected web listeners or dropping HTTP connections.

---

## 6. Network & Internet Broadcasting

### Local Network (Wi-Fi / LAN)
- Share your station with smart TVs, laptops, and mobile devices connected to your home Wi-Fi.
- Listeners connect directly using the Local Network URL: `http://<Local-IP>:<Port>`.

### Outside Network (Internet Broadcasting)
- **Automatic UPnP Port Forwarding**: Broadcasters do not need to manually configure home router NAT settings. Enabling UPnP automatically instructs your home router to forward the station port to your PC.
- **Public IP Sharing**: Remote listeners access your station via your WAN IP: `http://<Public-IP>:<Port>`.

### Custom Public Domain / Host / IP Override
- If you use dynamic DNS (e.g. No-IP, DuckDNS) or have a custom domain name (e.g. `radio.mydomain.com`), enter it into the **Custom Domain / Public Host** field in the Network card:
  ```
  Custom Domain / Public Host (Optional)
  [ radio.mydomain.com:8080 ]
  ```
- Scrim immediately updates all share links, browser buttons, and the Share Broadcast modal to use your domain. Click `✕ Reset` anytime to revert to auto-detected WAN IP.

### Direct VLC & Media Player Endpoint
- Broadcasters and listeners can listen directly in desktop media players (VLC, Winamp, foobar2000, mpv):
  - **Stream URL**: `http://<IP-or-Domain>:<Port>/stream`
  - In VLC: Press **Ctrl+N** (Open Network Stream) and paste this URL.

---

## 7. Responsive Web Player & Visualizers

Scrim serves an embedded, responsive HTML5 web player with real-time Server-Sent Events (SSE) metadata.

### Dark Glassmorphism Design
- Responsive across mobile devices, tablets, and widescreen desktop monitors.
- Displays live station branding, DJ show name, genre tagline, active listener count, and album artwork from Windows System Media Transport Controls (SMTC).

### Selectable Visualizer Styles
Listeners can switch between 4 live sound reaction modes using the visualizer style switcher:
1. **Neon Frequency Spectrum**: Smooth 64-band frequency analyzer with gradient lighting.
2. **Dynamic Waveform**: Flowing oscillographic wave reacting in real time to audio peaks.
3. **Glowing Retro VU Meter**: Twin analog meters with physical needle ballistics.
4. **Pulsing Sound Bars**: Modern jumping sound columns with peak-hold decay caps.

### Instant Autoplay on Load
- When a listener browses to the web player while your station is live, the web player immediately initiates audio playback and visualizers.
- If a browser enforces strict media autoplay restrictions prior to user gesture, the player prompts with *"Click anywhere to listen live"* and unlocks instantly on the listener's first tap or click anywhere on the page.

### Quick Audio Mute & Header Speaker Control
- **Top Speaker Button**: Located next to the format badge in the player card header. Clicking toggles mute/unmute instantly without losing your volume slider position.
- **Keyboard Shortcut**: Press **`M`** anywhere on the page to quickly mute or unmute audio playback.
- **Synchronized UI**: The top speaker button, bottom volume button, and slider all stay synchronized in real time.

### Progressive Web App (PWA) Installation
- **Install as Desktop or Mobile App**: Click the **`[ Install App ]`** button in the web player header to install Scrim as a standalone desktop application (Chrome, Edge) or add it to your mobile home screen (Android, iOS).
- **Service Worker Shell**: Automatically caches web player styling, scripts, and branding assets for instant startup while keeping live audio streaming and metadata events 100% real-time.

---

## 8. Real-Time Anonymous Web Chat & Broadcaster Controls

Scrim features a built-in, low-latency anonymous live chat system connecting listeners and the broadcaster in real time without third-party services or logins.

### Anonymous Web Chat
- **No Sign-Up or Accounts Required**: Listeners can chat completely anonymously.
- **Customizable Nicknames**: Automatic radio listener handles (e.g. `Listener #482`) are assigned on first visit and stored locally in browser storage. Listeners can change their nickname at any time.
- **Sub-Millisecond SSE Push**: Real-time message broadcast pushed via HTTP Server-Sent Events (`/api/events`) immediately as messages are submitted (`POST /api/chat`).
- **Auto-Scrolling & Responsive Bubbles**: Distinct bubble styling, timestamps, and custom avatar color coding for every participant.

### Broadcaster Station Console Chat Card
- **Live Monitoring**: Broadcaster sees all incoming listener messages live in the **Live Station Chat** card.
- **Verified Host / DJ Messaging**: The broadcaster can type in the console input and send replies with a verified, glowing red **`[HOST]`** badge.
- **Broadcaster Enable / Disable Switch**: The station owner can pause or resume chat at any time via the toggle switch in the console card. When paused, the web player immediately shows a "Chat paused by host" banner and disables input.
- **Chat History Management**: Broadcasters can clear chat history at any time with the **`🗑️ Clear`** button.

---

## 9. Interactive Song Requests Queue & Dedications

- **Listener Song Requests**: Listeners can submit song title and artist directly from the web player.
- **Free-Form Dedications**: Listeners can dedicate the song to anyone with a free-form name or shoutout (e.g. "For Maria", "Mom", "The Night Shift Crew").
- **Live Queue Display**: Dedications are displayed with a heart badge in the web player's **UPCOMING: LIVE QUEUE** and in the broadcaster's **Song Requests** console card.
- **DJ Moderation**: The DJ can click **`✔`** to accept or **`✗`** to decline requests in real time.

---

## 10. Studio Soundboard & Custom Sounds

### Built-In Studio SFX
Synthesized in-memory 44.1kHz stereo audio effects mixed directly into your live broadcast stream with automatic ducking and analog VU needle reaction, while simultaneously playing locally in your DJ headphones:
- **Airhorn**: Multi-oscillator detuned brass fanfare.
- **Applause**: Filtered white-noise crowd ovation.
- **Rimshot**: Snappy percussive snare hit with acoustic ring.
- **Bleep**: 1 kHz broadcast censor tone.
- **Laser**: Pitch-swept sci-fi chirp.
- **Vinyl**: Low-frequency turntable scratch / brake.

### Custom Audio Files
- Add custom `.wav` sound clips with a custom title and local file path.
- Automatically converted to 44.1kHz 16-bit stereo PCM and mixed into both the live stream encoder and local DJ playback.
- Custom sounds automatically persist across sessions in your profile.

---

## 11. Persistent Per-Song Reactions & Session Song History

### Per-Song Reactions & Real-Time Floating Particles
- **Single Vote with Vote Switching**: Anonymous listeners can vote once per track (`👍 Thumbs Up`, `❤️ Love`, or `👎 Dislike`), click their vote again to undo, or click another emoji to seamlessly switch their vote.
- **Per-Song Persistence**: Reactions are tracked and persisted per track (`Title::Artist`). When a song is replayed later in a session or across sessions, its accumulated reaction counts and the listener's individual vote are automatically restored on both the web player and broadcaster console.
- **Host Reaction Moderation**: The station owner can view reactions per track in the console Media Player card and clear counts for individual songs or reset all counts.

### Session Song History & Recently Played Modal
- **Session History Tracking**: Tracks every unique song played during a broadcast session in reverse-chronological order.
- **Reaction Badges**: In both the broadcaster console Media Player drawer and the web player's **RECENTLY PLAYED** modal, each track displays its accumulated reaction badges (`👍`, `❤️`, `👎`).
- **Owner Clear Control**: The host can clear session history at any time with a single click.

---

## 12. Console Customization & Profile Persistence

### Modular 3-Column Drag & Drop
- Click **`+ Add / Manage Cards`** to show or hide any of the 11 available studio cards.
- Drag any card header to reorder cards within or across columns.
- **Column Proportions**: Toggle between:
  - **📻 Studio (Little-Big-Little)**: 310px left column, flexible center master column, 340px right column.
  - **⚖️ Equal (1fr - 1fr - 1fr)**: Three evenly balanced columns.

### Persistent Audio Routing & Device Settings
All settings are stored in `C:\Users\<User>\.scrim\Default.json`:
- **Selected Application & Process Tracking**: Retains your chosen music application (e.g. `Spotify`, `Chrome`, `Discord`) across song changes and application restarts by tracking process identity rather than temporary song title strings.
- **Virtual Audio Device Persistence**: If you previously configured `CABLE Input` or a virtual audio cable, Scrim automatically reselects and re-establishes virtual routing on startup.
- **Clean Microphone & Loopback Resampling**: Universal 44.1kHz 16-bit stereo PCM resampling engine prevents hardware sample rate mismatches (such as 48kHz microphone shared-mode formats or 32-bit floating-point audio) from creating static or crashing the transmitter.
- Station branding, show titles, genre tags, custom soundboard effects, and card layouts remain consistent across all sessions.
- **Upgrade-Proof**: Upgrading or reinstalling the Scrim MSI installer never resets or overwrites your personal preferences.

---

## 13. Website Branding, Custom Banners & Header Images

Broadcasters can personalize their public web player with a custom header banner image, station logo, station title, and show subtitle directly from the **Website Branding & Links** console card.

### Top Station Header Banner Overview
The header banner spans the entire top edge of the web player page, resting immediately above the navigation header bar. 

- **Default Behavior (No Banner)**: If no banner image is configured (or if the field is cleared), the web player displays its clean, compact dark studio navigation header with zero dead space or empty layout gaps.
- **Dynamic Display**: When configured, the banner appears with a seamless CSS gradient overlay (`rgba(0, 0, 0, 0.05)` at the top blending down into `var(--header-bg)` at the bottom), providing a smooth cinematic transition into the header controls.

### Recommended Dimensions & Aspect Ratios

The web player banner container is responsive with a fixed height:
- **Desktop Screens (> 768px)**: `180px` height, `100%` viewport width.
- **Mobile Screens (≤ 768px)**: `120px` height, `100%` viewport width.
- **Image Scaling**: Displayed using CSS `object-fit: cover` with `object-position: center`. The image fills the full width and height while preserving its aspect ratio, cleanly centering the artwork.

| Use Case | Optimal Resolution | Aspect Ratio | Notes |
| :--- | :---: | :---: | :--- |
| **Best Overall (1080p Desktop)** | **1920 × 240 px** to **1920 × 360 px** | **8:1** to **16:3** | Recommended. Perfectly fills full-HD screens with minimal vertical cropping. |
| **High-Res / 4K / Ultrawide** | **2560 × 320 px** to **2560 × 480 px** | **8:1** to **16:3** | Crisp on 1440p and 4K ultra-wide monitors. |
| **Standard Banner** | **1200 × 180 px** | **20:3 (6.6:1)** | Exact 1:1 pixel match for standard 1200px centered page layouts. |
| **Standard Wallpaper / Artwork** | **1920 × 1080 px** | **16:9** | Supported via `object-fit: cover`. Ensure subject is vertically centered. |

### Safe Zone & Visual Framing Tips
1. **Vertical Centering**: Because `object-fit: cover` crops evenly from the top and bottom on wide desktop displays, keep logos, station titles, and key visual subjects within the **vertical center 50%** of your canvas.
2. **Bottom Gradient Blend**: A soft gradient overlays the bottom 30% of the banner (`linear-gradient(180deg, ... var(--header-bg) 100%)`) to create a smooth transition into the player header. Avoid placing small text, copyright lines, or delicate logos along the bottom edge.
3. **Contrast & Theme Harmony**: Dark, vibrant, or atmospheric designs (such as synthwave cityscapes, studio neon, or ambient gradients) blend best with Scrim's dark glassmorphism aesthetic.

### Where to Store Assets & File Path Resolution
Custom banners and logos can be stored locally in your Scrim user directory:
```
%USERPROFILE%\.scrim\assets\
```
*(For example: `C:\Users\<Username>\.scrim\assets\`)*

> [!TIP]
> In the Scrim Console, navigate to the **Website Branding & Links** card and click **`📁 Open Assets Folder`** to open this folder directly in Windows File Explorer.

You can reference your image in the **Station Banner Image** input field using any of the following formats:
- **Relative Path**: `.\assets\mybanner.png` or `assets\mybanner.jpg`
- **Default Filename Lookup**: `mybanner.png` (Scrim automatically looks in `~/.scrim/assets/` by default)
- **Extension-Free**: `mybanner` (Scrim automatically probes `.png`, `.jpg`, `.jpeg`, `.webp`, `.svg`, and `.gif`)
- **Direct File Path**: `C:\Banners\mybanner.png`
- **Remote Web URL**: `https://example.com/banner.png`

