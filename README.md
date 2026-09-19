# Scrim Broadcast Console

Scrim is a Windows desktop radio broadcast console built with C# and Blazor Hybrid. It intercepts audio streams from a targeted application in real time, overlays an on-air DJ microphone bus with automatic sidechain ducking, encodes the mixed stream on the fly across multiple formats, and broadcasts the audio as an Icecast-compatible HTTP stream.

## Prerequisites

Scrim relies on **FFmpeg** to encode audio streams on the fly into MP3, AAC, or Opus formats. You must have FFmpeg installed and accessible in your system's `PATH`.

### Installing FFmpeg on Windows:
1. Open PowerShell as Administrator.
2. Install via Winget:
   ```powershell
   winget install ffmpeg
   ```
3. Restart your terminal or the Scrim application to ensure the `PATH` variables have refreshed.

## Voice Changers & Processing FX

Scrim includes built-in real-time DSP voice changers with dual-tap granular pitch shifting and vocal formant filtering:
- **Anime Girl**: High-pitch shift (+7.2 semitones, 1.52x ratio) with chest resonance high-pass cut (<320Hz) and sparkling presence boost (3.5kHz - 8kHz) for a cute anime heroine vocal tone.
- **Woman**: Shifts vocal fundamental frequency up (+4.2 semitones) and filters low-end rumble for a clear, natural feminine tone.
- **Man**: Shifts pitch down (-4.5 semitones) and shapes low-mid chest resonance for a deep baritone broadcast voice.
- **Robot**: 50Hz metallic ring modulation.
- **Radio**: Vintage bandpass speaker filtering with soft clipping saturation.
- **Alien**: High-frequency frequency-modulated tremolo.
- Custom extensions via Lua scripts, JSON DSP effect chains, and .NET DLL plugins.

## Documentation

All detailed project specifications, architectural guides, and development resources are stored in the `/docs` directory.

- [Project Specification & Master Implementation Guide](docs/spec.md)
- [Network & Outside Broadcasting Guide](docs/network-broadcasting.md)
- [Plugin Development Guide](docs/plugin-development.md)

## Building & Deploying

To build, test, or publish Scrim as a self-contained executable, use the provided PowerShell script:

```powershell
.\scrim.ps1 -Build
.\scrim.ps1 -Publish
```
