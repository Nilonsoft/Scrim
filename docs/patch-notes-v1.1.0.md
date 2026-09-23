# Scrim Release v1.1.0 Patch Notes

**Release Date:** 2026-09-22  
**Installer Package:** ScrimSetup-v1.1.0.msi  

## What's New in v1.1.0

### 📡 Seamless Stream Auto-Reconnect & Connection Resilience
* **Persistent Listener Intent**: Resolved web frontend playback interruption when restarting the broadcaster server or during transient network disconnects. Listener playback intent is preserved across drops so audio resumes seamlessly without requiring manual intervention or page reloads.
* **Intelligent Probe & Progressive Backoff**: Reconnection mechanism utilizes lightweight `/api/status` probing with exponential backoff (1.5s to 5.0s) to detect server reboot completion while preventing browser media decoder lockups or unhandled 503 errors.
* **Resilient EventSource (SSE) Watchdog**: Event stream automatically re-establishes real-time connection on server revival, clearing stale socket states and triggering immediate audio resynchronization.
* **Cache-Busted Stream Reset**: Refreshes `<audio>` elements with unique timestamp markers on reconnect to guarantee zero latency and immediate real-time synchronization.

---

### 🎛️ Native Multi-Station Console & Independent Streams
* **Multi-Station Workstation**: Run multiple discrete broadcast stations from a single Scrim console instance. Each station operates its own independent encoding pipeline, audio buffer, listener hub, metadata feed, and web portal.
* **Dedicated Mount Points**: Default primary station broadcasts to `/stream`, while secondary and tertiary stations stream to dedicated mount points (e.g. `/rock`, `/ambient`, `/chill`).
* **Console Station Switcher**: Quick-switch between active stations in the console toolbar to configure station profiles, adjust channel volume, inspect listener metrics, and monitor audio meters.
* **Interactive Channel Dial**: Web frontend includes an intuitive Channel Dial pill navigation bar allowing listeners to hop between your active stations in 1 click.
* **Main Page Linking Control (Excluded by Default)**: Extra stations can be excluded from linking on the main page Channel Dial. Excluded stations remain unlisted and private from general visitors, but accessible via their direct mount URL or dedicated PWA. Configurable when creating stations and toggleable at any time from the console.

---

### 🎧 Application-Level Audio Routing & Process Loopback
* **Process Loopback Capture**: Capture audio directly from specific running applications without stereo mix clutter (e.g. Route Spotify to Station 1, Google Chrome to Station 2, Console Media Player to Station 3).
* **Physical Device Routing**: Assign different WASAPI audio input or output loopback devices to each station.
* **Custom Encoders per Station**: Configure independent audio formats (MP3, AAC, FLAC, Opus) and bitrates (64 kbps to 320 kbps / Lossless) tailored to each station's genre and audience.

---

### 📱 Station-Scoped Progressive Web Apps (PWA)
* **Independent Web App Installation**: Each station features a station-scoped Web App Manifest (`/manifest.webmanifest?station={mount}`) with unique application IDs (`/pwa/{mount}`).
* **Custom Home Screen Icons & Colors**: Mobile listeners can install distinct web apps for each station with dedicated station branding, theme colors, and icons.
* **Scoped Start URLs**: Opening the installed PWA launches directly into that specific station's stream and interface.

---

### 🎧 External DJ Ingest (Icecast SOURCE & HTTP PUT)
* **Remote Broadcaster & Guest DJ Support**: Spin up a dedicated station and allow external guest DJs to stream directly into Scrim using standard broadcasting software (e.g. BUTT, Mixxx, OBS Studio, Traktor, VirtualDJ, SAM Broadcaster).
* **Native Icecast Protocol**: Supports standard Icecast `SOURCE /mount ICE/1.0` and HTTP `PUT /mount` protocols with per-station password authentication.
* **Real-Time Track Metadata Integration**: Supports Icecast `/admin/metadata?mode=updinfo` endpoint, automatically displaying the DJ's live "Now Playing" track titles and artists in the web player and chat.
* **Zero Quality Loss Direct Pass-Through**: DJ audio streams are received and relayed directly through the station's broadcast hub with zero re-encoding loss and negligible CPU footprint.
* **1-Click Connection Guide**: Built-in credential generator and configuration helper in the console with Host, Port, Mount Point, Username, and Password.
