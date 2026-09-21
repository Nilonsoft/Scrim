# Scrim Release v1.0.6.1 Patch Notes

**Release Date:** 2026-09-21  
**Installer Package:** ScrimSetup-v1.0.6.1.msi  

## What's New in v1.0.6.1

### 🎤 Live Synced & Plain Lyrics Drawer
* **LRCLIB Integration**: Real-time line-by-line synchronized lyrics querying the free, open LRCLIB database without requiring any API keys.
* **Audio Buffer Sync Calibration**: Added stream buffer compensation (default 2.0s delay offset) with interactive `-` / `+` micro-adjustment controls in the lyrics footer (`Sync: -2.0s`), automatically saved to `localStorage`.
* **Plain Lyrics Fallback**: Automatically renders formatted plain lyrics when synchronized timestamps are unavailable.

---

### 🎨 Dynamic Ambient Backdrop
* **Real-time Color Extraction**: Ambient glassmorphic backdrop dynamically extracts vibrant palette colors from the current song's album art in real-time.
* **Owner Theme Precedence Toggle**: Added an option in Website Branding allowing station owners to prioritize their chosen theme over dynamic color extraction.
* **Reactive Palette Updates**: Palette transitions reactively on track change or when toggling settings without requiring a page refresh.

---

### 📺 Party / TV Full-Screen Mode
* **Cinema Display Layout**: 1-click full-screen TV mode with enlarged glowing album art, expanded audio visualizer, and large clock.
* **Auto-Idle Inactivity Hiding**: Automatically fades out navigation controls, header, and mouse cursor after 3.5 seconds of inactivity.

---

### 📅 Station Schedule & Broadcaster Bio Modal
* **Broadcaster Profile Fields**: Station owners can configure their broadcast schedule, bio, and social links (Discord, Twitch, Twitter / X).
* **Interactive Web Modal**: Web listeners can view station schedule and host information by clicking "Schedule" or "About" in the top navigation.

---

### ⚔️ DJ Live Polls & Track Battles
* **Studio Console Poll Launcher**: DJ can launch custom 2-option listener polls or 1-click "Track Battles" comparing the current track with the previous track.
* **Real-Time Web Voting**: Real-time listener voting with animated percentage bars and live vote counters pushed via Server-Sent Events.

---

### 🛡️ 24/7 Continuous Streaming & Server Resiliency
* **Fault-Tolerant Accept Loops**: Hardened HTTP and TCP bridge accept loops against transient network glitches, DHCP lease renewals, and socket resets.
* **Asynchronous Request Isolation**: Every web asset and API request is dispatched in an isolated task so client disconnects can never crash or block the server.
* **Self-Healing Watchdog**: Added a background watchdog in the console that continuously verifies HTTP server health and automatically re-binds if interrupted.
* **Connection Timeouts**: Enforced 15-second read timeouts on bridge headers to eliminate zombie sockets from stalled clients.
