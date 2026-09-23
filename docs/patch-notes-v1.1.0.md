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
