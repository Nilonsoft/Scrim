# Scrim Broadcast Console — Comprehensive User & Feature Guide

Scrim is a state-of-the-art Windows desktop radio broadcast console built with C# and Blazor Hybrid. It allows you to intercept application audio, mix live DJ microphone audio with automated sidechain ducking, apply real-time DSP voice changers, transcode live audio into multiple formats with zero latency, and stream high-fidelity audio over your local network and the internet.

---

## Table of Contents

1. [Quick Start Guide (Get On Air in 60 Seconds)](#1-quick-start-guide)
2. [Audio Routing & Virtual Audio Setup](#2-audio-routing--virtual-audio-setup)
3. [Master Broadcast & Transmitter (TX) Meters](#3-master-broadcast--transmitter-tx-meters)
4. [DJ Microphone, Sidetone & Voice Changers](#4-dj-microphone-sidetone--voice-changers)
5. [Live Encoding & Zero-Latency Hot-Swapping](#5-live-encoding--zero-latency-hot-swapping)
6. [Network & Internet Broadcasting](#6-network--internet-broadcasting)
7. [Responsive Web Player & Visualizers](#7-responsive-web-player--visualizers)
8. [Interactive Song Requests Queue](#8-interactive-song-requests-queue)
9. [Studio Soundboard & Custom Sounds](#9-studio-soundboard--custom-sounds)
10. [Console Customization & Profile Persistence](#10-console-customization--profile-persistence)

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

---

## 8. Interactive Song Requests Queue

- **Listener Submission**: Listeners can submit song title, artist, and a personal dedication note directly from the web player.
- **DJ Moderation Queue**: Incoming requests appear in the **Song Requests Queue** card in the Scrim console.
- **1-Click Queue Management**: The DJ can click **`✔ Accept`** to move songs into the on-air queue or **`✕ Reject`** to discard.

---

## 9. Studio Soundboard & Custom Sounds

### Built-In Studio SFX
Synthesized in-memory 44.1kHz stereo audio effects:
- **Airhorn**: Multi-oscillator detuned brass fanfare.
- **Applause**: Filtered white-noise crowd ovation.
- **Rimshot**: Snappy percussive snare hit with acoustic ring.
- **Bleep**: 1 kHz broadcast censor tone.
- **Laser**: Pitch-swept sci-fi chirp.
- **Vinyl**: Low-frequency turntable scratch / brake.

### Custom Audio Files
- Add custom `.wav` sound clips with a custom title and local file path.
- Custom sounds automatically persist across sessions in your profile.

---

## 10. Console Customization & Profile Persistence

### Modular 3-Column Drag & Drop
- Click **`+ Add / Manage Cards`** to show or hide any of the 10 available studio cards.
- Drag any card header to reorder cards within or across columns.
- **Column Proportions**: Toggle between:
  - **📻 Studio (Little-Big-Little)**: 310px left column, flexible center master column, 340px right column.
  - **⚖️ Equal (1fr - 1fr - 1fr)**: Three evenly balanced columns.

### Seamless Persistence in `~/.scrim/`
All settings are stored in `C:\Users\<User>\.scrim\Default.json`:
- Selected audio devices, microphone modes, and virtual routing.
- Custom card order and column layout.
- Station branding, show titles, and genre tags.
- Custom domain URLs and custom soundboard effects.
- **Upgrade-Proof**: Upgrading or reinstalling the Scrim MSI installer never resets or overwrites your personal preferences.
