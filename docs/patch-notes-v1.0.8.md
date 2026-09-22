# Scrim Release v1.0.8 Patch Notes

**Release Date:** 2026-09-21  
**Installer Package:** ScrimSetup-v1.0.8.msi  

## What's New in v1.0.8

### 🎛️ Pro DJ Stereo Mixer & Discrete Channels
* **Hardware-Class Channel Architecture**: Separate discrete channels for Stereo Music Deck, Microphone Talkover, and Master Bus inspired by club-standard DJ consoles (Pioneer DJM, Traktor Pro, Serato DJ).
* **Constant-Power Stereo Panning**: Smooth Left/Right balance control with quick center detent reset (`C`).
* **Mid-Side Stereo Width Expander**: Variable stereo field processing from `0.0x (True Mono sum)` for mono compatibility checks to `1.0x (Standard Stereo)` and `2.0x (Super-Wide Stereo)` for immersive spatial imaging in headphones and stereo monitors.
* **Music Trim & Microphone Gain**: Fine-grained `-12.0 dB` to `+12.0 dB` pre-gain leveling, channel mutes, and solo switches.
* **80Hz Rumble Cut (High-Pass Filter)**: Dedicated broadcast-grade high-pass filter tuned to 80Hz to eliminate desk thumps, vibrations, and vocal plosives.

---

### 🎚️ 3-Band DJ Isolator & Instant Kill Switches
* **3-Band Frequency Isolator**: Dedicated HIGH (2.5k-20kHz), MID (300-2.5kHz), and LOW (20-300Hz) bands with `-26 dB` to `+6 dB` throw.
* **Instant Kill Buttons**: Illuminated **LOW KILL**, **MID KILL**, and **HIGH KILL** buttons with `-inf dB` full cutoff for drop transitions, vocal isolation, and beatmatching without clashing basslines.
* **DJ Sound Color Sweep Filter**: Bi-polar filter sweeping from a resonant Low-Pass Filter (sweeps down to 160Hz) to neutral bypass to a resonant High-Pass Filter (sweeps up to 3.5kHz).

---

### 📊 10-Band ISO Graphic Equalizer
* **10 ISO Frequency Bands**: Surgical acoustic tuning across 31Hz, 63Hz, 125Hz, 250Hz, 500Hz, 1kHz, 2kHz, 4kHz, 8kHz, and 16kHz with +/- 12 dB precision vertical faders.
* **Pro DJ Presets**: 1-click loading for *Flat / Bypass*, *Club / EDM Bass*, *Radio DJ Voice*, *Rock & Live*, *Warm Vinyl & Acoustic*, *Hip-Hop Thump*, *Lofi Lounge*, and *Treble Sparkle*.

---

### 📈 Real-Time Vector Response Curve & Soft Limiter
* **Live SVG Response Curve**: Real-time vector curve displaying aggregate frequency response across 20Hz - 20kHz with illuminated cyan/purple gradient fill.
* **Analog Soft-Clip Limiter**: Hyperbolic tangent (`tanh`) saturation preventing harsh digital clipping while delivering punchy club-level loudness.
* **Modular Studio Card**: Added `"card-dj-eq"` ("Pro DJ Mixer & 10-Band EQ") to the console layout with full persistence across restarts.

---

### 📻 SAM Broadcaster PRO Modular Suite (7 New Modular Cards)
* **Modular On-Demand Architecture**: Keep the studio interface clean and lightweight while unlocking powerful station automation. None of these cards clutter the console by default; users can enable, position, and close them as desired via **+ Add / Manage Cards**.
* **Dual Decks & Crossfader (`card-dual-deck`)**: Interactive Deck A / Deck B controls with CUE, Stop, Pitch tempo shifting (-8% to +8%), and equal-loudness crossfader curves with 1-click Auto-DJ crossfade.
* **Automated Show Clock & Scheduler (`card-event-scheduler`)**: Time-of-day automated sweeps, top-of-hour station IDs, chat announcements, theme switching, and poll triggers on hourly, daily, or interval cadences.
* **Voice-Tracking Transition Recorder (`card-voice-tracking`)**: Record DJ voice talk-overs ramped with automated music bed ducking over track intros/outros before air.
* **Multi-Encoder & Relay Rack (`card-multi-encoder`)**: Broadcast multiple simultaneous stream formats (MP3 128k, AAC+ 64k mobile, FLAC lossless) or relay to external Icecast / SHOUTcast radio clusters.
* **Live Listener Connections & IP Inspector (`card-listener-inspector`)**: Real-time listener connection table displaying IP addresses, mount points, session duration, bandwidth transferred, player types, and 1-click listener disconnect/kick.
* **Dead-Air Auto-Recovery & Alarm (`card-silence-recovery`)**: Master bus audio energy watchdog with configurable silence threshold and live countdown timer that auto-triggers backup jingles or audio alarms.
* **Music Rotation Rules & Category Bins (`card-rotation-rules`)**: Categorical crates (Heavy, Medium, Gold Classics, Sweepers), artist separation constraints, and automated 1-hour clockwheel queue generation.
* **Comprehensive Documentation**: Detailed feature breakdown in [`docs/sam-pro-features-guide.md`](file:///c:/Users/donwo/source/Scrim/docs/sam-pro-features-guide.md).

