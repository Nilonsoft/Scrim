# Scrim Broadcast Console — Post-Installation & User Guide

Welcome to **Scrim**! Whether you are hosting a personal live radio show, streaming music to your friends over the internet, running audio for an online community, or broadcasting across your home Wi-Fi network, Scrim gives you a full-featured broadcast studio on your Windows desktop.

This comprehensive guide walks you through everything from running the installer for the first time, setting up your audio, connecting your microphone, going on air, and sharing your station with listeners.

---

## Table of Contents

1. [System Requirements](#1-system-requirements)
2. [Installation & First Launch](#2-installation--first-launch)
3. [Prerequisites: FFmpeg Setup](#3-prerequisites-ffmpeg-setup)
4. [Quick Start: On Air in 60 Seconds](#4-quick-start-on-air-in-60-seconds)
5. [Audio Routing & Virtual Cable Setup](#5-audio-routing--virtual-cable-setup)
6. [DJ Microphone, Voice Changers & Sidetone](#6-dj-microphone-voice-changers--sidetone)
7. [Transmitter, Meters & Encoding Formats](#7-transmitter-meters--encoding-formats)
8. [Studio Soundboard & Custom SFX](#8-studio-soundboard--custom-sfx)
9. [Sharing Your Stream (Local & Internet)](#9-sharing-your-stream-local--internet)
10. [Web Player Features for Listeners](#10-web-player-features-for-listeners)
11. [Station Chat, Moderation & Emojis](#11-station-chat-moderation--emojis)
12. [Song Requests & Dedications](#12-song-requests--dedications)
13. [Custom Branding & Header Banners](#13-custom-branding--header-banners)
14. [Troubleshooting & FAQ](#14-troubleshooting--faq)
15. [Quick Reference Cheat Sheet](#15-quick-reference-cheat-sheet)

---

## 1. System Requirements

- **Operating System**: Windows 10 (Version 19041 / 20H1 or newer) or Windows 11 (64-bit).
- **Processor**: Dual-core 2.0 GHz or higher (quad-core recommended for high-bitrate FLAC or multi-listener streaming).
- **RAM**: 4 GB minimum (8 GB recommended).
- **Audio Output**: Any standard sound card, USB DAC, or headphones.
- **Microphone**: Any built-in or USB microphone (required only if you wish to speak on air).
- **Network**: Local Wi-Fi / Ethernet for LAN broadcasting; broadband internet connection for public streaming.

---

## 2. Installation & First Launch

### Running the Installer
1. Double-click the installer package provided to you:
   ```text
   Scrim-Setup.msi
   ```
2. If Windows SmartScreen displays a warning (*"Windows protected your PC"*), click **More info**, then click **Run anyway**. *(This prompt appears because private beta builds do not yet have an expensive commercial code-signing certificate).*
3. Follow the installation wizard. Scrim installs to your program directory and automatically creates shortcuts on your **Desktop** and in your **Start Menu**.

### First Launch
1. Launch Scrim from your Desktop shortcut or Start Menu.
2. If Windows Defender Firewall prompts you regarding network permissions:
   - Check **Private networks** (home or work).
   - Check **Public networks** (if applicable on laptops).
   - Click **Allow access**. This allows Scrim's embedded streaming server to serve audio and the web player to your listeners.

---

## 3. Prerequisites: FFmpeg Setup

Scrim uses **FFmpeg** behind the scenes to perform real-time, low-latency audio transcoding into MP3, AAC, OGG Opus, and FLAC.

### Automatic Detection
When you launch Scrim, it checks whether FFmpeg is installed and accessible:
- If FFmpeg is already installed on your system, Scrim starts immediately with no prompts.
- If FFmpeg is not detected, Scrim displays a helpful **FFmpeg Setup Guidance Modal** with instructions.

### How to Install FFmpeg (Quickest Method)
Open PowerShell or Windows Terminal and run:
```powershell
winget install Gyan.FFmpeg
```
Once the download completes, restart Scrim. Scrim will detect FFmpeg automatically.

### Manual Installation (Alternative)
1. Download a portable build from [gyan.dev/ffmpeg/builds](https://www.gyan.dev/ffmpeg/builds/) (choose `ffmpeg-release-essentials.zip`).
2. Extract the archive and copy `ffmpeg.exe` into one of the following locations:
   - Your Scrim installation directory (`%LOCALAPPDATA%\Programs\Scrim\` or wherever Scrim is installed).
   - Any folder on your Windows system `PATH` (such as `C:\Windows\System32`).
3. Re-open Scrim, and you are ready to broadcast!

---

## 4. Quick Start: On Air in 60 Seconds

Here is the fastest way to get your station broadcasting:

1. **Open Scrim**: Launch the desktop application.
2. **Setup Audio Routing (1-Click)**:
   - In the **Audio Routing** card, click the green button:
     ```text
     [ ⚡ Setup Virtual Device ]
     ```
   - If Windows User Account Control (UAC) asks for permission, click **Yes** to allow installation of the bundled WHQL-certified VB-Audio Virtual Cable driver.
   - Scrim automatically sets up the virtual audio cable and configures Windows default sound to route through Scrim.
3. **Play Music**: Start playing music on Spotify, YouTube, Apple Music, VLC, or a web browser.
4. **Go Live**:
   - In the top navigation bar, click the glowing **`🔴 ON AIR`** button (or click **`[ ⚡ START BROADCAST ]`** in the Encoding card).
   - The status badge changes to pulsing red **`ON AIR`**, and the **`🟢 TX`** meter lights up green indicating audio is actively transmitting.
5. **Tune In**:
   - In the top bar, click **`Share Stream`**.
   - Click **Copy Local Link** or scan the QR code with your smartphone connected to your home Wi-Fi.
   - Enjoy your live music stream and interactive web player!

---

## 5. Audio Routing & Virtual Cable Setup

### Why Virtual Audio Routing?
Normally, Windows sends audio directly to your physical speakers. To broadcast your computer's music with DJ commentary cleanly—without room echo or picking up background noise—Scrim captures sound digitally using a virtual audio pipeline.

### The 1-Click Virtual Device Button
Clicking **`[ ⚡ Setup Virtual Device ]`** performs all necessary setup automatically:
1. Installs the included WHQL-certified VB-Audio Virtual Cable driver if it is not already present.
2. Connects Scrim's high-speed WASAPI loopback capture engine to `CABLE Input`.
3. Sets Windows default audio output to the virtual device, routing all music and PC sounds into Scrim.
4. Automatically routes your microphone into the virtual device so recording tools or games capture your voice alongside music.

### Restoring Your Speakers
When you finish broadcasting, simply click:
```text
[ ↩ Restore Speakers ]
```
Windows immediately switches your audio output back to your default physical headphones or speakers (e.g. Realtek Audio, Sound Blaster, or USB DAC).

### Application Window Isolation vs. Desktop Mix
- **Desktop Mix (Default)**: Captures all computer audio (Spotify, games, web browsers, Discord).
- **Specific Window**: In the **Application Window** dropdown, pick a single running application (e.g. `Spotify.exe`) to stream only that application's music while keeping other PC sounds private.

### Local Speaker Monitoring Modes
In the Audio Routing card, you can choose how music sounds on your own PC speakers:
- **Play Locally & Stream**: You hear the music through your PC speakers or headphones while Scrim broadcasts it simultaneously.
- **Stream Only (Muted Locally)**: Silences the music on your physical PC speakers while Scrim broadcasts it to your listeners. This is ideal if you are using open-back headphones or room speakers and want to avoid microphone bleed.

---

## 6. DJ Microphone, Voice Changers & Sidetone

Scrim features a broadcast-grade live voice engine with real-time digital signal processing (DSP).

### Microphone Controls
- **Microphone Selection**: Choose your microphone input device in the dropdown menu.
- **Push-to-Talk (PTT)**: The microphone stays muted until you click and hold the talk button or press the configured hotkey.
- **Push-to-Mute (Cough Button)**: The microphone stays open for continuous talking, muting only when pressed.

### Real-Time Headphone Monitoring (Sidetone)
- Click **`[ 🎧 Hear Myself: ON ]`** on the Master VU card to hear your own voice in your headphones with ultra-low latency (25ms buffer).
- Sidetone lets you monitor your vocal volume, pronunciation, and voice effects in real time.

### Off-Air Mic Check
- Click **`[ 🎙️ Test Mic (Off-Air) ]`** to test your microphone without broadcasting your voice to listeners.
- Your voice plays only in your headphones; listeners continue hearing station music uninterrupted.

### Automatic Sidechain Music Ducking
When you speak into your microphone, Scrim automatically lowers the background music by **-14 dB** using smooth 20ms attack and 250ms release curves. When you stop speaking, the music smoothly returns to full volume.

### Built-in DSP Voice Changers
Transform your voice on the fly:
- **Anime Girl**: High-pitch shift (+7.2 semitones, 1.52x ratio), low-end rumble cut, and treble presence boost for a kawaii anime vocal tone.
- **Woman**: Natural pitch elevation (+4.2 semitones) with warm midrange balance.
- **Man**: Deep downward pitch shift (-4.5 semitones) with low-mid resonance boost for a deep radio announcer sound.
- **Robot**: 50Hz metallic ring modulation.
- **Radio**: Vintage bandpass telephone/intercom filter with soft saturation.
- **Alien**: High-frequency modulated pitch flutter.
- **Clean / Bypass**: Pristine, uncolored studio vocal reproduction.

---

## 7. Transmitter, Meters & Encoding Formats

### Transmitter & Meter Indicators
- **`🔴 ON AIR`**: Indicates the streaming server is live and accepting listener connections.
- **`🟢 TX (Transmitter Active)`**: Lights up green when audio frames are actively encoding and transmitting over the network.
- **Master Peak Meters (L & R)**: Dual analog-style stereo decibel meters (`-40 dB` to `0.0 dB`).
- **MIC Meter**: Dedicated cyan peak meter showing microphone levels before sidechain ducking.

### Audio Encoding Formats
Scrim supports 4 broadcast codecs in the **Encoding & Broadcast** card:
1. **MP3**: Universal compatibility across all web browsers, smartphones, car stereos, and smart TVs. (64k to 320 kbps).
2. **AAC**: Modern high-efficiency encoding with crisp highs and rich bass at lower bitrates. (96k to 256 kbps).
3. **OGG Opus**: Ultra-low latency, speech- and music-optimized codec. (64k to 192 kbps).
4. **FLAC**: Lossless, bit-perfect studio broadcast format. Zero compression artifacts.

> [!TIP]
> **Zero-Latency Hot-Swapping**: You can change audio formats or bitrates while live on air! Scrim switches the encoder in real time without dropping connected listeners.

---

## 8. Studio Soundboard & Custom SFX

The **Studio Soundboard** card allows you to fire off audio stingers and sound effects with a single click.

### Built-in Studio Effects
- **Airhorn**: Classic reggae/DJ multi-oscillator brass fanfare.
- **Applause**: Realistic crowd cheers and ovation.
- **Rimshot**: Snappy comedy drum hit ("ba-dum tss").
- **Bleep**: Standard 1 kHz broadcast censor tone.
- **Laser**: Retro sci-fi laser blast.
- **Vinyl**: Turntable record scratch / tape stop.

### Adding Custom Sounds
1. Click **`+ Add Custom Sound`** in the Soundboard card.
2. Enter a display name for the button.
3. Browse or enter the path to any local `.wav` or `.mp3` audio file.
4. Your custom buttons appear in the soundboard and persist across application restarts.

When fired, sound effects automatically mix into both your live broadcast stream and your local DJ headphones, with analog VU needle reaction.

---

## 9. Sharing Your Stream (Local & Internet)

Click **`Share Stream`** in the top navigation bar to access all sharing links.

### 1. Local Network / Home Wi-Fi Sharing
- **Link format**: `http://192.168.1.X:4242/`
- Any device connected to your home Wi-Fi or local network (phones, laptops, smart TVs, tablets) can open this link in any browser to listen to your broadcast.
- Scan the on-screen **QR Code** with your phone's camera for instant 1-tap listening!

### 2. Internet / Public Broadcasting
To let friends outside your home listen over the internet:
- **Automatic UPnP**: Scrim attempts to open port `4242` on your home router automatically via UPnP.
- **Manual Port Forwarding**: If UPnP is disabled on your router, forward TCP port `4242` to your computer's local IP address.
- **Reverse Proxies / Cloudflare Tunnels**:
  - Scrim includes dedicated toggles for **"Use HTTPS"** and **"Using Reverse Proxy"** (which strips port numbers from links).
  - Perfect for custom domains like `https://radio.yourdomain.com`.

### 3. Private Broadcast Mode
If you only want people in your home or on your local LAN to listen, check **Local Network Only (Private Stream)** in the Network card:
- Local Wi-Fi listeners hear the music and use all features normally.
- Remote internet visitors see a clean **"🔒 Private Stream"** lockout page, completely blocking audio, metadata, and chat.

### 4. Direct Media Player Endpoints
For listeners who prefer standalone media players (VLC, Winamp, foobar2000, mobile streaming apps):
- Direct audio stream: `http://<ip>:4242/live` or `http://<ip>:4242/stream`
- M3U Playlist: `http://<ip>:4242/stream.m3u`
- PLS Playlist: `http://<ip>:4242/stream.pls`

---

## 10. Web Player Features for Listeners

Listeners enjoy a responsive HTML5 web player with rich interactivity:

- **Clean Album Artwork**: Displays high-resolution album cover art synchronized with your media player. No text obscures the artwork.
- **1-Click Google Search**: Clicking the song title or artist in the web player opens a Google search for that track in a new tab.
- **Interactive Audio Visualizer**: 4 real-time visualizer modes:
  - 📊 **Bars**: Classic animated graphic equalizer bars.
  - 📈 **Wave**: Smooth oscilloscope waveform.
  - 🌈 **Spectrum**: Multi-frequency harmonic spectrum.
  - ✨ **Pulse**: Rhythmic radial bass pulse.
- **10 Curated Aesthetic Themes**: Listeners can customize the web player look via the palette icon:
  - Default Synthwave, Sunset, Cyberpunk, Emerald, Amethyst, Midnight, Gold, Space, Lofi, and Ocean.
- **Per-Song Reactions**: Listeners can react with `👍 Thumbs Up`, `❤️ Love`, or `👎 Dislike`, spawning floating reaction particles. Reaction counts persist per song across the broadcast session!
- **Recently Played History**: The **RECENTLY PLAYED** drawer shows the history of all tracks played during the session with their reaction counts.
- **Progressive Web App (PWA)**: Listeners can click **Install App** to install the station as a native app on their phone or desktop.

---

## 11. Station Chat, Moderation & Emojis

Scrim includes a built-in real-time station chat for listeners and the DJ.

### Interactive Emoji Picker & Autoconversion
- **Emoji Picker**: Click the **`😊`** button inside the chat input box to open a quick tray of 32 popular emojis. Click any emoji to insert it at your cursor.
- **Automatic Emoticon Conversion**: Typing common emoticons automatically converts them into rich Unicode emojis in real time and upon sending:
  - `:O` or `:o` ➔ 😮
  - `:)` or `:-)` ➔ 😊
  - `:D` or `XD` ➔ 😀
  - `;)` ➔ 😉
  - `:(` ➔ 😢
  - `:P` ➔ 😛
  - `<3` ➔ ❤️
  - `</3` ➔ 💔
  - `B)` ➔ 😎
  - `:fire:` ➔ 🔥, `:thumbsup:` ➔ 👍, `:thumbsdown:` ➔ 👎, `:party:` ➔ 🎉, `:music:` ➔ 🎵, `:rocket:` ➔ 🚀, `:100:` ➔ 💯, `:skull:` ➔ 💀, `:eyes:` ➔ 👀, `:sparkles:` ➔ ✨

### DJ Host Chat & Badges
- Messages sent by the host from the Scrim console display with a distinctive red **`[DJ]`** verified badge.

### Broadcaster Moderation Tools
In the console's **Live Station Chat** card, the station host has full moderation authority:
- **Assign Nickname**: Click any listener's name to assign them a custom handle.
- **Delete Message**: Click **`✕`** next to any message to remove it instantly for all connected listeners.
- **Ban User**: Click **`🚫 Ban`** next to a message to permanently ban that user from posting.
- **Nickname Blacklist**: Add disallowed words or offensive terms to the automatic nickname filter.
- **Pause Chat**: Temporarily pause chat with a single click during announcements.

---

## 12. Song Requests & Dedications

Listeners can submit song requests directly through the web player interface:
- **Song Query**: Title and artist requested by the listener.
- **Personal Dedication**: Optional message (e.g. *"Dedicated to Sarah for her birthday! <3"*).

In the Scrim console's **Song Requests** card:
- Requests appear in a live queue.
- The DJ can mark requests as **Approved**, **Played**, or **Dismissed**.
- Approved and pending requests appear in the web player's public queue so listeners can see what is coming up next.

---

## 13. Custom Branding & Header Banners

Personalize your station in the **Website Branding & Links** console card:
- **Station Name**: Appears in browser tabs and player navigation (e.g. *"Nilon Factory Radio"*).
- **DJ / Host Name**: Your on-air handle shown on chat messages and player subtitles.
- **Show Subtitle**: Custom tagline (e.g. *"80s Synthwave & Retrowave All Night"*).

### Top Station Header Banner
You can add a custom header banner image spanning the top of your web player.

#### Recommended Dimensions
| Format | Optimal Resolution | Aspect Ratio |
| :--- | :---: | :---: |
| **Best Overall (1080p Desktop)** | **1920 × 240 px** to **1920 × 360 px** | **8:1** to **16:3** |
| **High-Res / 4K / Ultrawide** | **2560 × 320 px** to **2560 × 480 px** | **8:1** to **16:3** |
| **Standard Banner** | **1200 × 180 px** | **20:3** |

#### Storing Banner Files
Place image files in your Scrim assets directory:
```text
C:\Users\<YourUsername>\.scrim\assets\
```
*(In Scrim, click **`📁 Open Assets Folder`** in the Website Branding card to open this folder directly).*

You can reference your image in the **Banner Image** input field using:
- Just the filename: `mybanner.png`
- A relative path: `.\assets\mybanner.png`
- A direct path: `C:\Images\mybanner.jpg`
- A web URL: `https://example.com/banner.png`

---

## 14. Troubleshooting & FAQ

### Q: Scrim says "FFmpeg is missing or not detected."
**A**: Open PowerShell and run `winget install Gyan.FFmpeg`, then restart Scrim. Alternatively, place `ffmpeg.exe` inside your Scrim installation folder.

### Q: Listeners can't hear any music playing.
**A**:
1. Check that Scrim is **`🔴 ON AIR`** and the green **`🟢 TX`** meter is illuminated.
2. In the **Audio Routing** card, make sure **CABLE Input** is selected as the capture device.
3. Verify that your music player (Spotify, YouTube, etc.) is playing audio through the virtual cable.
4. Check that the **App Volume on Stream** slider is set to at least 100% and not muted.

### Q: I hear my own voice echoing or repeating.
**A**: In the Master VU card, check if **`[ 🎧 Hear Myself ]`** is enabled while listening through open PC speakers instead of headphones. Turn off Hear Myself or switch to headphones.

### Q: Friends outside my house cannot connect to the stream link.
**A**:
1. If using automatic UPnP, check your router settings to ensure UPnP is enabled.
2. If manually port forwarding, ensure TCP port `4242` is forwarded to your PC's local IP address.
3. Check Windows Firewall to ensure Scrim has permission on Private and Public networks.
4. Verify that **Local Network Only (Private Stream)** is **unchecked** in the Network card.

### Q: Does reinstalling or updating Scrim erase my settings or soundboard?
**A**: No. All configuration, custom sounds, station branding, and window positions are safely preserved in `%USERPROFILE%\.scrim\Default.json`.

---

## 15. Quick Reference Cheat Sheet

| Action | Where to Click / What to Do |
| :--- | :--- |
| **Start / Stop Broadcasting** | Click **`🔴 ON AIR`** in top bar |
| **Setup Virtual Audio (1-Click)** | Audio Routing card ➔ **`[ ⚡ Setup Virtual Device ]`** |
| **Switch Back to PC Speakers** | Audio Routing card ➔ **`[ ↩ Restore Speakers ]`** |
| **Mute Music on Stream Instantly** | Audio Routing card ➔ Click **`Mute`** button |
| **DJ Mic On / Off** | Hold PTT button or toggle Push-to-Mute |
| **Hear Your Voice in Headphones** | Master VU card ➔ **`[ 🎧 Hear Myself ]`** |
| **Test Mic Privately Off-Air** | Master VU card ➔ **`[ 🎙️ Test Mic (Off-Air) ]`** |
| **Fire Soundboard Effect** | Soundboard card ➔ Click any SFX button |
| **Share Link with Friends** | Top bar ➔ Click **`Share Stream`** |
| **Open Chat Emoji Picker** | Web player or console chat ➔ Click **`😊`** |
| **Search Song on Google** | Click the track title or search icon in web player |
| **Change Web Player Theme** | Web player ➔ Click palette icon in header |
