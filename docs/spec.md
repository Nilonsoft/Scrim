# Scrim — Project Specification & Master Implementation Guide

A Windows desktop radio broadcast console built with C# and Blazor Hybrid. Scrim intercepts audio streams from a targeted application or window process tree in real time, overlays an on-air DJ microphone bus with automatic sidechain ducking, encodes the mixed stream on the fly across multiple formats, and broadcasts the audio as an Icecast-compatible HTTP stream alongside an embedded, responsive web player.

## Technical & Coding Conventions

- **Brace Style:** Strict K&R / One True Brace Style (OTBS). All opening braces reside on the same line as statements; `else`, `catch`, and `finally` share the line with closing braces.
- **Type Architecture:** Strict one-type-per-file layout across all namespaces.
- **Root Hygiene:** Only solution/project files and the root `README.md` reside in the repository root. All design specs, documentation, and operational guides belong in `/docs`.
- **Flow Control:** Guard clauses over nested conditionals. Return early on invalid handles, unallocated pointers, or null checks.
- **Concurrency Discipline:** Asynchronous, non-blocking pipelines utilizing `System.Threading.Channels.Channel<T>` and `CancellationToken`. Capture and socket write loops must never block UI or audio pump threads.
- **Development Cadence:** Work incrementally, generating and verifying one file at a time.

## Architectural Layout

### Input Layer:
- **Application Audio ->** WASAPI Process Loopback (captures target PID and child worker processes).
- **Host Microphone ->** WASAPI Recording Device.

### Mixing & Dynamics:
- 2-Bus Master Mixer combines App PCM and Mic PCM.
- Sidechain auto-ducker dynamically attenuates application audio by -14 dB when speech is detected or Push-to-Talk is engaged.
- Master soft-knee limiter prevents clipping.

### Transcoding Engine:
- Multi-format encoder (MP3 via LAME, AAC via MediaFoundation, Opus via libopus) operating at selectable bitrates (64k to 320k CBR).

### Distribution Hub:
- Broadcast ring buffer with a 2-second pre-roll burst cache.
- Dedicated per-client bounded channels with tail-drop frame shedding for slow/jittery Wi-Fi connections.

### Endpoints & Presentation:
- `GET /stream`: Live chunked audio stream for web or media players.
- `GET /`: Self-contained HTML5 responsive web player with real-time album art and on-air indicator.
- `GET /api/events`: Server-Sent Events (SSE) feed for track metadata and listener counts.
- **Desktop Shell:** Blazor Hybrid running on WPF with a neutral dark-grey theme and live dual VU meters.

## Core Systems Specification

### 1. Process Audio Tap & Normalization (`/src/Audio`)
- **Process-Tree Loopback:** Utilizes WASAPI's `ActivateAudioInterfaceAsync` paired with `VIRTUAL_AUDIO_DEVICE_PROCESS_LOOPBACK` and `PROCESS_LOOPBACK_MODE_INCLUDE_TARGET_PROCESS_TREE`. Capturing the root window process automatically pulls audio from nested renderer and audio-utility subprocesses (crucial for Chrome, Edge, and Electron apps).
- **Format Standardization:** Converts variable application output (often multi-channel 32-bit float) into a uniform 44.1 kHz, 16-bit stereo PCM stream via `PcmAudioResampler.cs`.
- **Silence Watchdog:** Automatically injects synthetic zero-filled PCM frames if the source app pauses, changes tracks, or sleeps, ensuring the encoder and client HTTP connections remain active.

### 2. Live DJ Talkback & Auto-Ducker
- **Microphone Capture:** Dedicated WASAPI input loop pulling from the user's selected recording device.
- **Talk-Over Modes:**
  - **Push-to-Talk (PTT):** Opens the microphone bus while a hotkey or UI button is depressed.
  - **Latch ("On Air"):** Toggles the microphone bus open for extended spoken commentary.
- **Sidechain Ducking:** When microphone input exceeds the noise gate threshold or when PTT is activated, the music bus attenuates by -14 dB using a 150 ms attack curve. Once speech ceases, volume restores over a 350 ms exponential release curve.
- **Master Peak Limiter:** Applies a soft-knee limiter on the summed 2-bus output to eliminate clipping distortion when speaking over loud tracks.

### 3. Multi-Format Transcoding Engine (`/src/Encoding`)
- **Dynamic Codec Switching:** Encodes on the fly to MP3 (via LAME), AAC (via MediaFoundation), or Opus (via libopus).
- **Bitrate Profiles:** Selectable CBR settings (64 kbps, 128 kbps default, 192 kbps, 320 kbps).
- **Hot-Swap Support:** Changing formats or bitrates drains pending frames, injects new sync headers, and shifts downstream distribution without terminating client sockets.

### 4. Resilient Multi-Client Fan-Out Server (`/src/Server`)
- **Connection Isolation:** Each connected listener runs an independent `ClientConnectionWorker` consuming an isolated `Channel<byte[]>`. Slow consumers never throttle other listeners or backpressure the audio encoder.
- **Pre-Roll Burst Buffer:** Retains a rolling 2-second cache of audio frames. Upon receiving `GET /stream`, the server bursts this cache to instantly prime the listener's buffer, eliminating playback stutter on Wi-Fi.
- **Drop-Tail Lag Prevention:** Drops the oldest frames in a client's channel if connection throughput dips, forcing clients to remain locked to the live broadcast edge.
- **Embedded Player & Endpoints:**
  - `GET /`: Serves an embedded, dependency-free responsive HTML5 player with live album art, an "ON AIR" indicator, a live queue viewer, and a song request submission form.
  - `GET /stream`: Serves chunked live audio (`audio/mpeg`, `audio/aac`, or `audio/ogg`).
  - `GET /api/events`: Server-Sent Events (SSE) stream pushing track titles, artist names, listener metrics, DJ on-air alerts, and real-time synchronization of the song request queue.
  - `POST /api/requests`: Endpoint for listeners to submit song requests directly to the DJ.

### 5. Cloud/Desktop Metadata Extraction (`/src/Metadata`)
- Interops with WinRT `GlobalSystemMediaTransportControlsSessionManager`.
- Resolves active sessions from the target application (Spotify, YouTube Music, Tidal, etc.).
- Broadcasts extracted track title, artist name, and album artwork directly to the host Blazor UI and the web player interface.

### 6. User Interface & Theming (`/src/UI`)
- **Host Runtime:** Blazor Hybrid running on WPF with WebView2.
- **Theme Styling:** Deep, neutral-grey dark aesthetic.
  - **Background:** `#121212`
  - **Panels/Cards:** `#181818`
  - **Elevation Surfaces:** `#242424`
  - **Borders:** `#383838`
  - **Text:** High-contrast off-white (`#EDEDED`) and muted secondary grey (`#9E9E9E`).
  - **Accents:** Studio-meter green (`#22C55E`), amber warning (`#F59E0B`), and vibrant "ON AIR" red (`#EF4444`). Zero default OS blues.
- **Console Controls:** Window/App picker, Mic selector, PTT/Latch switches, dual stereo VU meters, format/bitrate pills, copyable stream URL, dynamic QR code modal, and active listener tally.
- **Song Request Queue:** A dedicated draggable card populated in real-time from web listener submissions. The DJ can rearrange, edit, and dismiss these incoming requests to manage their playlist.
- **Responsive & Customizable Layout:** The UI utilizes a responsive CSS grid that automatically adapts to window resizing. Cards are modular and can be dragged, dropped, and rearranged by the user to fit their workflow.

### 7. Configuration & Profiles (`/src/Config`)
- **Storage Location:** All configurations are stored in the user's home directory under `~/.scrim/` as JSON files.
- **Profiles:** Users can create, update, export, and import multiple streaming configuration profiles.
- **Auto-Apply:** Selecting a profile automatically applies its settings in real-time.
- **Profile Editor:** A dedicated UI within the application allows for creating and managing configuration profiles.

### 8. Plugin System & Extensibility API (`/src/Plugins`)
- **API Surface:** An internal API exposes hooks into the audio pipeline, metadata stream, and broadcast status.
- **Custom UI Cards:** Users can develop their own Blazor components as plugins, which the application dynamically loads and renders as new draggable cards on the dashboard.
- **Developer Documentation:** A comprehensive guide (`plugin-development.md`) will be provided in the `/docs` folder to instruct users on how to scaffold, build, and deploy custom plugins.

## Deployment & Tooling

- **Self-Contained Executable:** The application must be compiled and published as a completely self-contained, single-file executable without external runtime dependencies.
- **Build Script:** A PowerShell script (`scrim.ps1`) located in the repository root provides automated commands to `clean`, `build`, `test`, and `publish` the application via CLI switches.

## Repository Structure

```
/docs
  spec.md
scrim.ps1
/src
  Scrim.sln
  Config/
    IProfileManager.cs
    ProfileManager.cs
    ScrimProfile.cs
  Audio/
    IAudioCaptureService.cs
    ProcessLoopbackCapture.cs
    IMicrophoneCaptureService.cs
    MicrophoneCaptureService.cs
    AudioDuckingMixer.cs
    PcmAudioResampler.cs
    SilenceWatchdog.cs
  Interop/
    NativeMethods.cs
    AudioClientActivationParams.cs
    AudioCaptureInterfaces.cs
  Encoding/
    IAudioEncoder.cs
    AudioFormat.cs
    MultiFormatTranscoder.cs
    LameMp3Encoder.cs
    AacEncoder.cs
    OpusEncoder.cs
  Metadata/
    IMetadataService.cs
    MediaMetadata.cs
    WindowsMediaMetadataService.cs
  Server/
    IStreamServer.cs
    HttpStreamServer.cs
    BroadcastHub.cs
    ClientConnectionWorker.cs
    PreRollBuffer.cs
  Web/
    EmbeddedWebPlayer.cs
    Assets/
      index.html
      player.js
      player.css
  Windows/
    IWindowEnumerator.cs
    WindowEnumerator.cs
    WindowInfo.cs
  UI/
    MainWindow.xaml
    MainWindow.xaml.cs
  Blazor/
    App.razor
    Components/
      MeterBar.razor
      MicControlCard.razor
      StreamControlCard.razor
      TrackInfoCard.razor
    Styles/
      dark-theme.css
```

## Phased Implementation Sequence

### Phase 1: Window Interception & Capture
- `WindowInfo.cs`
- `IWindowEnumerator.cs`
- `WindowEnumerator.cs`
- `NativeMethods.cs`
- `AudioClientActivationParams.cs`
- `AudioCaptureInterfaces.cs`
- `IAudioCaptureService.cs`
- `ProcessLoopbackCapture.cs`
- `PcmAudioResampler.cs`
- `SilenceWatchdog.cs`

### Phase 2: DJ Mic & Sidechain Mixer
- `IMicrophoneCaptureService.cs`
- `MicrophoneCaptureService.cs`
- `AudioDuckingMixer.cs`

### Phase 3: Transcoding & Network Broadcast
- `AudioFormat.cs`
- `IAudioEncoder.cs`
- `LameMp3Encoder.cs`
- `AacEncoder.cs`
- `OpusEncoder.cs`
- `MultiFormatTranscoder.cs`
- `PreRollBuffer.cs`
- `ClientConnectionWorker.cs`
- `BroadcastHub.cs`
- `IStreamServer.cs`
- `HttpStreamServer.cs`

### Phase 4: Web Player & System Metadata
- `EmbeddedWebPlayer.cs`
- `MediaMetadata.cs`
- `IMetadataService.cs`
- `WindowsMediaMetadataService.cs`
- `SongRequestController.cs` (handling `POST /api/requests`)

### Phase 5: Blazor Hybrid UI & Host Assembly
- `dark-theme.css`
- `MeterBar.razor`
- `MicControlCard.razor`
- `StreamControlCard.razor`
- `TrackInfoCard.razor`
- `App.razor`
- `MainWindow.xaml`
- `MainWindow.xaml.cs`

### Phase 6: Configuration & Profiles
- `ScrimProfile.cs`
- `IProfileManager.cs`
- `ProfileManager.cs`
- Profile Editor UI components in Blazor
- Implement JSON storage in `~/.scrim/`
- `scrim.ps1` helper script for self-contained publishing

### Phase 7: Plugin System & Extensibility
- `IScrimPlugin.cs`
- `IUiExtension.cs`
- `PluginLoader.cs`
- Implement drag-and-drop dashboard grid in Blazor