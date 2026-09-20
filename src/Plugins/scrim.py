"""
Scrim Python Plugin SDK
Provides base classes and runtime loop for building Scrim broadcast plugins in Python.
"""

import sys
import json
from dataclasses import dataclass
from typing import Optional, Dict, Any

@dataclass
class TrackInfo:
    artist: str = ""
    title: str = ""
    album: str = ""
    duration_seconds: int = 0
    station: str = ""

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> "TrackInfo":
        return cls(
            artist=data.get("artist", "") or "",
            title=data.get("title", "") or "",
            album=data.get("album", "") or "",
            duration_seconds=int(data.get("duration", 0) or 0),
            station=data.get("station", "") or ""
        )


class ScrimPlugin:
    """Base class for Scrim Python plugins."""

    def __init__(self):
        self.station_name: str = ""
        self.port: int = 8000
        self.is_live: bool = False
        self.listener_count: int = 0
        self.current_track: TrackInfo = TrackInfo()

    def log(self, message: str) -> None:
        """Send a log message back to Scrim."""
        self._send({"type": "log", "message": str(message)})

    def update_metadata(self, artist: str, title: str, album: Optional[str] = None) -> None:
        """Request Scrim to update the broadcast metadata."""
        payload = {"type": "update_metadata", "artist": artist, "title": title}
        if album:
            payload["album"] = album
        self._send(payload)

    def _send(self, data: Dict[str, Any]) -> None:
        try:
            line = json.dumps(data)
            sys.stdout.write(line + "\n")
            sys.stdout.flush()
        except Exception as e:
            sys.stderr.write(f"[scrim.py] Error writing to stdout: {e}\n")
            sys.stderr.flush()

    # Lifecycle Hooks - Override these in your plugin
    def on_init(self, station_info: Dict[str, Any]) -> None:
        """Called when Scrim initializes the plugin."""
        pass

    def on_metadata_changed(self, track: TrackInfo) -> None:
        """Called when track or song metadata changes."""
        pass

    def on_broadcast_state_changed(self, is_live: bool) -> None:
        """Called when broadcasting starts or stops."""
        pass

    def on_listener_count_changed(self, count: int) -> None:
        """Called when the active listener count changes."""
        pass

    def on_song_requested(self, request: Dict[str, Any]) -> None:
        """Called when a listener submits a song request."""
        pass

    def on_shutdown(self) -> None:
        """Called when the plugin is being unloaded or Scrim is exiting."""
        pass

    def run(self) -> None:
        """Starts the stdin/stdout event processing loop."""
        self._send({"type": "status", "status": "ready"})

        for line in sys.stdin:
            line = line.strip()
            if not line:
                continue

            try:
                msg = json.loads(line)
            except Exception as ex:
                self.log(f"Invalid JSON received from host: {ex}")
                continue

            msg_type = msg.get("type", "")

            if msg_type == "init":
                station = msg.get("station", {})
                self.station_name = station.get("station_name", "")
                self.port = station.get("port", 8000)
                try:
                    self.on_init(station)
                except Exception as ex:
                    self.log(f"Error in on_init: {ex}")

            elif msg_type == "event":
                event_name = msg.get("event", "")
                event_data = msg.get("data", {})

                try:
                    if event_name == "metadata_changed":
                        track = TrackInfo.from_dict(event_data)
                        self.current_track = track
                        self.on_metadata_changed(track)

                    elif event_name == "broadcasting_state_changed":
                        is_live = bool(event_data.get("is_live", False))
                        self.is_live = is_live
                        self.on_broadcast_state_changed(is_live)

                    elif event_name == "listener_count_changed":
                        count = int(event_data.get("count", 0))
                        self.listener_count = count
                        self.on_listener_count_changed(count)

                    elif event_name == "song_requested":
                        self.on_song_requested(event_data)

                except Exception as ex:
                    self.log(f"Error handling event '{event_name}': {ex}")

            elif msg_type == "shutdown":
                try:
                    self.on_shutdown()
                except Exception as ex:
                    self.log(f"Error in on_shutdown: {ex}")
                break


def run_plugin(plugin_cls) -> None:
    """Convenience helper to instantiate and execute a ScrimPlugin."""
    plugin = plugin_cls()
    plugin.run()
