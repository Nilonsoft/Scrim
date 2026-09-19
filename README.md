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

## Documentation

All detailed project specifications, architectural guides, and development resources are stored in the `/docs` directory.

- [Project Specification & Master Implementation Guide](docs/spec.md)
- [Plugin Development Guide](docs/plugin-development.md)

## Building & Deploying

To build, test, or publish Scrim as a self-contained executable, use the provided PowerShell script:

```powershell
.\scrim.ps1 -Build
.\scrim.ps1 -Publish
```
