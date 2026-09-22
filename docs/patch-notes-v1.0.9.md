# Scrim Release v1.0.9 Patch Notes

**Release Date:** 2026-09-21  
**Installer Package:** ScrimSetup-v1.0.9.msi  

## What's New in v1.0.9

### 🎵 Local Music Playback from Console
* **Console-Native Playback**: Play local audio tracks directly from Scrim's console workstation without needing third-party players (Spotify, VLC, Winamp).
* **Broad Audio Format Support**: Plays `.mp3` (with automatic ID3v1 / ID3v2 tag decoding), `.wav` (PCM and IEEE float), `.flac`, `.m4a`, `.aac`, `.wma`, and `.aiff`.
* **Local Headphone & Speaker Monitoring**: Dedicated low-latency WASAPI output monitor (`WasapiOut`) allows DJs to listen locally while broadcasting with individual volume and mute controls.
* **Pro DJ Pipeline Integration**: Local music flows into the **Music** channel strip in `AudioDuckingMixer`, applying the 10-Band ISO Graphic EQ, Low/Mid/High Isolator kills, DJ Color Filter, stereo width, and pan.
* **Microphone Sidechain Auto-Ducking**: Local music automatically ducks down by `-14 dB` whenever the DJ speaks on the microphone (PTT or Voice Activated).

---

### 🎚️ Integrated Console Player & Transport
* **Now Playing Strip**: Live display of current track title, artist, album, format badge, and animated visualizer equalizer bars.
* **Interactive Seek Scrubber**: Jump to any point in the track with precision elapsed time and total duration timestamps (`mm:ss` / `h:mm:ss`).
* **Hardware-Style Transport Controls**: Previous Track (⏮), Play / Pause (▶ / ⏸), Stop (⏹), and Next Track (⏭).
* **Shuffle & Repeat Modes**:
  * **Shuffle (🔀)**: Randomizes track selection across the entire playlist without repeating until the playlist is exhausted.
  * **Repeat (🔁)**: Cycle through **Repeat Off**, **Repeat All** (continuous loop), and **Repeat 1 (🔂)** (loop current track).
  * **Auto-Advance**: Automatic gapless transition to the next track when the current song completes.

---

### 📂 Playlist Management Workstation (`card-playlist`)
* **Add Audio Files**: Multi-select audio tracks from anywhere on your drive via standard Windows file dialog.
* **Add Music Folder**: Scan whole music folders and libraries; Scrim discovers all supported audio files and parses metadata automatically.
* **Save & Load Playlists**: Export and load standard `.m3u` / `.m3u8` or `.json` playlists directly to/from `%USERPROFILE%\.scrim\playlists\`.
* **Instant Search & Filter**: Real-time filter bar to instantly locate tracks in large playlists by song title or artist.
* **Track Reordering**: Move tracks up (▲) or down (▼) to organize setlists.
* **Dual Deck Cueing**: 1-click cue buttons **`[A]`** and **`[B]`** to instantly load any track into Deck A or Deck B of the Dual Decks card.
* **Double-Click Play**: Double-click any track in the playlist table to immediately start playback.
