# Scrim Broadcast Console

Scrim is a Windows desktop radio broadcast console built with C# and Blazor Hybrid. It intercepts audio streams from a targeted application in real time, overlays an on-air DJ microphone bus with automatic sidechain ducking, encodes the mixed stream on the fly across multiple formats, and broadcasts the audio as an Icecast-compatible HTTP stream.

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
