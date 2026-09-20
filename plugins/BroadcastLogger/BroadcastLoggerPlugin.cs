using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Scrim.Metadata;
using Scrim.Plugins;

namespace Scrim.Plugin.BroadcastLogger {
    public class BroadcastLoggerPlugin : IScrimPlugin {
        public string Name => "Broadcast Session & Playlist Logger";
        public string Version => "1.0.0";
        public string Author => "Scrim Studio";

        private IScrimHost? _host;
        private string _showsDir = string.Empty;
        private string? _currentShowFile;
        private string _historyCsvFile = string.Empty;
        private DateTime? _sessionStartTime;
        private int _songsPlayed = 0;
        private int _peakListeners = 0;
        private readonly List<int> _listenerSamples = new();
        private string _lastTrack = string.Empty;
        private readonly object _fileLock = new();

        public void Initialize(IScrimHost host) {
            _host = host;

            try {
                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                _showsDir = Path.Combine(userProfile, ".scrim", "shows");
                if (!Directory.Exists(_showsDir)) {
                    Directory.CreateDirectory(_showsDir);
                }

                _historyCsvFile = Path.Combine(_showsDir, "playlist_history.csv");
                if (!File.Exists(_historyCsvFile)) {
                    File.WriteAllText(_historyCsvFile, "Timestamp,Station,Track,Artist,Album,Duration,Listeners\n");
                }

                _host.Broadcast.BroadcastingStateChanged += OnBroadcastingStateChanged;
                _host.Metadata.MetadataChanged += OnMetadataChanged;

                Console.WriteLine($"[{Name}] Initialized. Broadcast show logs will be saved to {_showsDir}");
            } catch (Exception ex) {
                Console.WriteLine($"[{Name}] Error during initialization: {ex.Message}");
            }
        }

        private void OnBroadcastingStateChanged(object? sender, bool isLive) {
            lock (_fileLock) {
                if (isLive) {
                    StartNewShowSession();
                } else {
                    FinalizeCurrentShowSession();
                }
            }
        }

        private void StartNewShowSession() {
            try {
                _sessionStartTime = DateTime.Now;
                _songsPlayed = 0;
                _peakListeners = _host?.Broadcast.ActiveClientCount ?? 0;
                _listenerSamples.Clear();
                _listenerSamples.Add(_peakListeners);
                _lastTrack = string.Empty;

                string filename = $"Show_{_sessionStartTime.Value:yyyy-MM-dd_HHmmss}.md";
                _currentShowFile = Path.Combine(_showsDir, filename);

                string stationName = _host?.ProfileManager.CurrentProfile.StationName ?? "Scrim Studio";
                int port = _host?.ProfileManager.CurrentProfile.Port ?? 5050;

                string header = $"""
                # Scrim Broadcast Show Log

                - **Station:** {stationName}
                - **Started:** {_sessionStartTime.Value:yyyy-MM-dd HH:mm:ss}
                - **Port:** {port}

                ---

                ## Track Playlist

                | Time | Track | Artist | Album | Duration | Listeners |
                |---|---|---|---|---|---|

                """;

                File.WriteAllText(_currentShowFile, header);
                Console.WriteLine($"[{Name}] Started logging show: {filename}");
            } catch (Exception ex) {
                Console.WriteLine($"[{Name}] Failed to start show session: {ex.Message}");
            }
        }

        private void FinalizeCurrentShowSession() {
            if (string.IsNullOrEmpty(_currentShowFile) || !File.Exists(_currentShowFile) || _sessionStartTime == null) {
                return;
            }

            try {
                var endTime = DateTime.Now;
                var duration = endTime - _sessionStartTime.Value;
                double avgListeners = _listenerSamples.Count > 0 ? Math.Round(_listenerSamples.Average(), 1) : 0;

                string summary = $"""

                ---

                ## Broadcast Summary

                - **Ended:** {endTime:yyyy-MM-dd HH:mm:ss}
                - **Total On-Air Duration:** {duration.Hours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}
                - **Total Tracks Played:** {_songsPlayed}
                - **Peak Concurrent Listeners:** {_peakListeners}
                - **Average Listeners:** {avgListeners}

                *Generated automatically by Scrim Broadcast Logger Plugin*
                """;

                File.AppendAllText(_currentShowFile, summary);
                Console.WriteLine($"[{Name}] Finalized show log: {Path.GetFileName(_currentShowFile)}");
            } catch (Exception ex) {
                Console.WriteLine($"[{Name}] Failed to finalize show session: {ex.Message}");
            } finally {
                _currentShowFile = null;
                _sessionStartTime = null;
            }
        }

        private void OnMetadataChanged(object? sender, MediaMetadata meta) {
            if (string.IsNullOrWhiteSpace(meta.Title) || meta.Title.StartsWith("Awaiting", StringComparison.OrdinalIgnoreCase)) {
                return;
            }

            string trackKey = $"{meta.Title} - {meta.Artist}";
            if (string.Equals(trackKey, _lastTrack, StringComparison.Ordinal)) {
                return;
            }
            _lastTrack = trackKey;

            int listeners = _host?.Broadcast.ActiveClientCount ?? 0;
            _peakListeners = Math.Max(_peakListeners, listeners);
            _listenerSamples.Add(listeners);
            _songsPlayed++;

            string durationStr = meta.Duration > TimeSpan.Zero ? meta.Duration.ToString(@"mm\:ss") : "--:--";
            string stationName = _host?.ProfileManager.CurrentProfile.StationName ?? "Scrim Studio";
            string timestamp = DateTime.Now.ToString("HH:mm:ss");

            lock (_fileLock) {
                // If show file is active, append markdown row
                if (!string.IsNullOrEmpty(_currentShowFile) && File.Exists(_currentShowFile)) {
                    try {
                        string escapedTitle = meta.Title.Replace("|", "\\|");
                        string escapedArtist = meta.Artist.Replace("|", "\\|");
                        string escapedAlbum = meta.Album.Replace("|", "\\|");
                        string row = $"| {timestamp} | {escapedTitle} | {escapedArtist} | {escapedAlbum} | {durationStr} | {listeners} |\n";
                        File.AppendAllText(_currentShowFile, row);
                    } catch { }
                }

                // Append CSV row
                if (!string.IsNullOrEmpty(_historyCsvFile)) {
                    try {
                        string csvLine = $"\"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\",\"{EscapeCsv(stationName)}\",\"{EscapeCsv(meta.Title)}\",\"{EscapeCsv(meta.Artist)}\",\"{EscapeCsv(meta.Album)}\",\"{durationStr}\",{listeners}\n";
                        File.AppendAllText(_historyCsvFile, csvLine);
                    } catch { }
                }
            }
        }

        private static string EscapeCsv(string value) {
            return value.Replace("\"", "\"\"");
        }

        public void Shutdown() {
            if (_host != null) {
                _host.Broadcast.BroadcastingStateChanged -= OnBroadcastingStateChanged;
                _host.Metadata.MetadataChanged -= OnMetadataChanged;
            }

            lock (_fileLock) {
                FinalizeCurrentShowSession();
            }

            Console.WriteLine($"[{Name}] Shutdown complete.");
        }
    }
}
