using System;
using System.IO;
using System.Text.Json;
using Scrim.Plugins;

namespace Scrim.Plugin.StreamArchiver {
    public class StreamArchiverPlugin : IScrimPlugin {
        public string Name => "Live Stream Audio Archiver";
        public string Version => "1.0.0";
        public string Author => "Scrim Studio";

        private IScrimHost? _host;
        private string _recordingsDir = string.Empty;
        private bool _enabled = true;
        private string _ext = "mp3";

        private FileStream? _currentStream;
        private string? _currentFilePath;
        private long _bytesRecorded = 0;
        private readonly object _lock = new();

        public void Initialize(IScrimHost host) {
            _host = host;

            try {
                LoadConfig();

                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (string.IsNullOrWhiteSpace(_recordingsDir) || !Directory.Exists(_recordingsDir)) {
                    _recordingsDir = Path.Combine(userProfile, ".scrim", "recordings");
                }
                if (!Directory.Exists(_recordingsDir)) {
                    Directory.CreateDirectory(_recordingsDir);
                }

                _host.Broadcast.BroadcastingStateChanged += OnBroadcastingStateChanged;
                _host.Broadcast.AudioFrameAvailable += OnAudioFrameAvailable;

                if (_host.Broadcast.IsBroadcasting) {
                    StartRecording();
                }

                Console.WriteLine($"[{Name}] Initialized. Live broadcasts will be recorded to {_recordingsDir}");
            } catch (Exception ex) {
                Console.WriteLine($"[{Name}] Initialization warning: {ex.Message}");
            }
        }

        private void LoadConfig() {
            try {
                string? pluginDir = Path.GetDirectoryName(typeof(StreamArchiverPlugin).Assembly.Location);
                string cfgPath = Path.Combine(pluginDir ?? "", "archiver_config.json");
                if (!File.Exists(cfgPath)) {
                    cfgPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins", "archiver_config.json");
                }

                if (File.Exists(cfgPath)) {
                    string json = File.ReadAllText(cfgPath);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("enabled", out var e)) _enabled = e.GetBoolean();
                    if (doc.RootElement.TryGetProperty("outputFolder", out var of)) _recordingsDir = of.GetString() ?? "";
                    if (doc.RootElement.TryGetProperty("fileExtension", out var fe)) _ext = fe.GetString() ?? "mp3";
                }
            } catch { }
        }

        private void OnBroadcastingStateChanged(object? sender, bool isLive) {
            lock (_lock) {
                if (isLive) {
                    StartRecording();
                } else {
                    StopRecording();
                }
            }
        }

        private void StartRecording() {
            if (!_enabled) return;

            try {
                StopRecording();

                string filename = $"Broadcast_{DateTime.Now:yyyy-MM-dd_HHmmss}.{_ext.TrimStart('.')}";
                _currentFilePath = Path.Combine(_recordingsDir, filename);
                _currentStream = new FileStream(_currentFilePath, FileMode.Create, FileAccess.Write, FileShare.Read);
                _bytesRecorded = 0;

                Console.WriteLine($"[{Name}] Recording started: {filename}");
            } catch (Exception ex) {
                Console.WriteLine($"[{Name}] Failed to start recording: {ex.Message}");
            }
        }

        private void OnAudioFrameAvailable(object? sender, byte[] frame) {
            if (!_enabled || frame == null || frame.Length == 0) return;

            lock (_lock) {
                if (_currentStream != null && _currentStream.CanWrite) {
                    try {
                        _currentStream.Write(frame, 0, frame.Length);
                        _bytesRecorded += frame.Length;
                    } catch { }
                }
            }
        }

        private void StopRecording() {
            if (_currentStream != null) {
                try {
                    _currentStream.Flush();
                    _currentStream.Dispose();
                    double mb = Math.Round(_bytesRecorded / (1024.0 * 1024.0), 2);
                    Console.WriteLine($"[{Name}] Recording finished: {Path.GetFileName(_currentFilePath)} ({mb} MB)");
                } catch { } finally {
                    _currentStream = null;
                    _currentFilePath = null;
                    _bytesRecorded = 0;
                }
            }
        }

        public void Shutdown() {
            if (_host != null) {
                _host.Broadcast.BroadcastingStateChanged -= OnBroadcastingStateChanged;
                _host.Broadcast.AudioFrameAvailable -= OnAudioFrameAvailable;
            }

            lock (_lock) {
                StopRecording();
            }

            Console.WriteLine($"[{Name}] Shutdown complete.");
        }
    }
}
