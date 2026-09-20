using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Scrim.Metadata;
using Scrim.Plugins;

namespace Scrim.Plugin.JingleSweeper {
    public class JingleSweeperPlugin : IScrimPlugin {
        public string Name => "Station Jingle & Sweeper Player";
        public string Version => "1.0.0";
        public string Author => "Scrim Studio";

        private IScrimHost? _host;
        private string _jinglesDir = string.Empty;
        private int _intervalMinutes = 15;
        private bool _enabled = true;
        private bool _playBetweenTracks = false;
        private DateTime _lastJingleTime = DateTime.UtcNow;
        private Timer? _timer;
        private readonly Random _rng = new();

        public void Initialize(IScrimHost host) {
            _host = host;

            try {
                LoadConfig();

                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (string.IsNullOrWhiteSpace(_jinglesDir) || !Directory.Exists(_jinglesDir)) {
                    _jinglesDir = Path.Combine(userProfile, ".scrim", "jingles");
                }
                if (!Directory.Exists(_jinglesDir)) {
                    Directory.CreateDirectory(_jinglesDir);
                }

                _host.Broadcast.BroadcastingStateChanged += OnBroadcastingStateChanged;
                _host.Metadata.MetadataChanged += OnMetadataChanged;

                _timer = new Timer(CheckInterval, null, 60000, 60000);

                var files = Directory.GetFiles(_jinglesDir, "*.wav");
                if (files.Length == 0) {
                    Console.WriteLine($"[{Name}] Initialized. Place .wav station IDs or drops in {_jinglesDir} to enable automatic sweepers.");
                } else {
                    Console.WriteLine($"[{Name}] Initialized with {files.Length} jingle(s) loaded from {_jinglesDir} (Interval: {_intervalMinutes}m).");
                }
            } catch (Exception ex) {
                Console.WriteLine($"[{Name}] Initialization warning: {ex.Message}");
            }
        }

        private void LoadConfig() {
            try {
                string? pluginDir = Path.GetDirectoryName(typeof(JingleSweeperPlugin).Assembly.Location);
                string cfgPath = Path.Combine(pluginDir ?? "", "jingle_config.json");
                if (!File.Exists(cfgPath)) {
                    cfgPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins", "jingle_config.json");
                }

                if (File.Exists(cfgPath)) {
                    string json = File.ReadAllText(cfgPath);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("enabled", out var e)) _enabled = e.GetBoolean();
                    if (doc.RootElement.TryGetProperty("intervalMinutes", out var im) && im.TryGetInt32(out int interval)) _intervalMinutes = Math.Max(1, interval);
                    if (doc.RootElement.TryGetProperty("playBetweenTracks", out var pbt)) _playBetweenTracks = pbt.GetBoolean();
                    if (doc.RootElement.TryGetProperty("jinglesFolder", out var jf)) _jinglesDir = jf.GetString() ?? "";
                }
            } catch { }
        }

        private void OnBroadcastingStateChanged(object? sender, bool isLive) {
            if (isLive) {
                _lastJingleTime = DateTime.UtcNow;
            }
        }

        private void OnMetadataChanged(object? sender, MediaMetadata meta) {
            if (!_enabled || !_playBetweenTracks || _host == null || !_host.Broadcast.IsBroadcasting) {
                return;
            }

            var elapsed = DateTime.UtcNow - _lastJingleTime;
            if (elapsed.TotalMinutes >= _intervalMinutes) {
                PlayRandomJingle();
            }
        }

        private void CheckInterval(object? state) {
            if (!_enabled || _host == null || !_host.Broadcast.IsBroadcasting || _playBetweenTracks) {
                return;
            }

            var elapsed = DateTime.UtcNow - _lastJingleTime;
            if (elapsed.TotalMinutes >= _intervalMinutes) {
                PlayRandomJingle();
            }
        }

        private void PlayRandomJingle() {
            try {
                if (string.IsNullOrEmpty(_jinglesDir) || !Directory.Exists(_jinglesDir)) return;
                var files = Directory.GetFiles(_jinglesDir, "*.wav");
                if (files.Length == 0) return;

                string chosen = files[_rng.Next(files.Length)];
                _lastJingleTime = DateTime.UtcNow;

                _host?.AudioMixer.PlayAudioFile(chosen);
                Console.WriteLine($"[{Name}] Played station sweeper: {Path.GetFileName(chosen)}");
            } catch (Exception ex) {
                Console.WriteLine($"[{Name}] Error playing jingle: {ex.Message}");
            }
        }

        public void Shutdown() {
            _timer?.Dispose();
            if (_host != null) {
                _host.Broadcast.BroadcastingStateChanged -= OnBroadcastingStateChanged;
                _host.Metadata.MetadataChanged -= OnMetadataChanged;
            }
            Console.WriteLine($"[{Name}] Shutdown complete.");
        }
    }
}
