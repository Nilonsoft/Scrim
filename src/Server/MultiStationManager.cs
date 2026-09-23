using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Scrim.Audio;
using Scrim.Configuration;
using Scrim.Metadata;

namespace Scrim.Server {
    public interface IMultiStationManager : IDisposable {
        IReadOnlyList<StationPipeline> GetAllStations();
        StationPipeline? GetStationById(string id);
        StationPipeline? GetStationByMount(string mount);
        StationPipeline GetActiveStation();
        void SetActiveStation(string id);
        StationPipeline AddStation(StationConfig config);
        bool RemoveStation(string id);
        void SyncFromProfile(ScrimProfile profile);
        void StartStation(string id, MicrophoneCaptureService? sharedMic = null, LocalMusicPlayerService? localPlayer = null);
        void StopStation(string id);
        void StartAllStations(MicrophoneCaptureService? sharedMic = null, LocalMusicPlayerService? localPlayer = null);
        void StopAllStations();
        event Action? StationsChanged;
        event Action<StationPipeline, bool>? StationBroadcastingStateChanged;
    }

    public class MultiStationManager : IMultiStationManager {
        private readonly object _lock = new();
        private readonly IProfileManager _profileManager;
        private readonly IMetadataService? _defaultMetadataService;
        private readonly BroadcastHub? _primaryHub;
        private readonly ConcurrentDictionary<string, StationPipeline> _pipelines = new(StringComparer.OrdinalIgnoreCase);
        private string _activeStationId = "";

        public event Action? StationsChanged;
        public event Action<StationPipeline, bool>? StationBroadcastingStateChanged;

        public MultiStationManager(IProfileManager profileManager, IMetadataService? defaultMetadataService = null, BroadcastHub? primaryHub = null) {
            _profileManager = profileManager ?? throw new ArgumentNullException(nameof(profileManager));
            _defaultMetadataService = defaultMetadataService;
            _primaryHub = primaryHub;

            SyncFromProfile(_profileManager.CurrentProfile);
        }

        public void SyncFromProfile(ScrimProfile profile) {
            lock (_lock) {
                var defaultStation = profile.EnsureDefaultStation();
                _activeStationId = profile.ActiveStationId;

                var currentIds = new HashSet<string>(profile.Stations.Select(s => s.Id), StringComparer.OrdinalIgnoreCase);

                // Remove stations no longer in profile
                foreach (var existingId in _pipelines.Keys.ToList()) {
                    if (!currentIds.Contains(existingId)) {
                        if (_pipelines.TryRemove(existingId, out var removed)) {
                            removed.Dispose();
                        }
                    }
                }

                // Add or update stations
                bool isFirst = true;
                foreach (var stationConfig in profile.Stations) {
                    bool isPrimary = isFirst || string.Equals(stationConfig.Id, defaultStation.Id, StringComparison.OrdinalIgnoreCase);
                    if (_pipelines.TryGetValue(stationConfig.Id, out var existingPipeline)) {
                        existingPipeline.ApplyConfig(stationConfig);
                    } else {
                        var pipeline = new StationPipeline(stationConfig, _defaultMetadataService, isPrimary ? _primaryHub : null);
                        pipeline.BroadcastingStateChanged += (sender, isLive) => {
                            if (sender is StationPipeline p) {
                                StationBroadcastingStateChanged?.Invoke(p, isLive);
                            }
                        };
                        _pipelines.TryAdd(stationConfig.Id, pipeline);
                    }
                    isFirst = false;
                }

                if (string.IsNullOrEmpty(_activeStationId) || !_pipelines.ContainsKey(_activeStationId)) {
                    _activeStationId = profile.Stations.FirstOrDefault()?.Id ?? defaultStation.Id;
                    profile.ActiveStationId = _activeStationId;
                }

                StationsChanged?.Invoke();
            }
        }

        public IReadOnlyList<StationPipeline> GetAllStations() {
            return _pipelines.Values.ToList();
        }

        public StationPipeline? GetStationById(string id) {
            if (string.IsNullOrWhiteSpace(id)) return null;
            _pipelines.TryGetValue(id, out var pipeline);
            return pipeline;
        }

        public StationPipeline? GetStationByMount(string mount) {
            if (string.IsNullOrWhiteSpace(mount)) return GetActiveStation();

            string normalized = mount.Trim().Trim('/').ToLowerInvariant();
            if (normalized.EndsWith(".mp3")) normalized = normalized.Substring(0, normalized.Length - 4);
            if (normalized.EndsWith(".aac")) normalized = normalized.Substring(0, normalized.Length - 4);
            if (normalized.EndsWith(".ogg")) normalized = normalized.Substring(0, normalized.Length - 4);
            if (normalized.EndsWith(".flac")) normalized = normalized.Substring(0, normalized.Length - 4);

            var match = _pipelines.Values.FirstOrDefault(p => {
                string pMount = p.Config.MountPoint?.Trim().Trim('/').ToLowerInvariant() ?? "";
                return string.Equals(pMount, normalized, StringComparison.OrdinalIgnoreCase);
            });

            return match ?? GetActiveStation();
        }

        public StationPipeline GetActiveStation() {
            if (!string.IsNullOrEmpty(_activeStationId) && _pipelines.TryGetValue(_activeStationId, out var active)) {
                return active;
            }

            var first = _pipelines.Values.FirstOrDefault();
            if (first != null) {
                _activeStationId = first.Config.Id;
                return first;
            }

            // Fallback emergency station
            var profile = _profileManager.CurrentProfile;
            var defaultCfg = profile.EnsureDefaultStation();
            var fallback = new StationPipeline(defaultCfg, _defaultMetadataService);
            _pipelines.TryAdd(defaultCfg.Id, fallback);
            _activeStationId = defaultCfg.Id;
            return fallback;
        }

        public void SetActiveStation(string id) {
            lock (_lock) {
                if (_pipelines.ContainsKey(id)) {
                    _activeStationId = id;
                    _profileManager.CurrentProfile.ActiveStationId = id;
                    _profileManager.SaveProfile(_profileManager.CurrentProfile);
                    StationsChanged?.Invoke();
                }
            }
        }

        public StationPipeline AddStation(StationConfig config) {
            lock (_lock) {
                if (config == null) throw new ArgumentNullException(nameof(config));
                if (string.IsNullOrWhiteSpace(config.Id)) config.Id = Guid.NewGuid().ToString("N");

                var profile = _profileManager.CurrentProfile;
                profile.Stations.Add(config);
                _profileManager.SaveProfile(profile);

                var pipeline = new StationPipeline(config, _defaultMetadataService);
                pipeline.BroadcastingStateChanged += (sender, isLive) => {
                    if (sender is StationPipeline p) {
                        StationBroadcastingStateChanged?.Invoke(p, isLive);
                    }
                };
                _pipelines.TryAdd(config.Id, pipeline);

                StationsChanged?.Invoke();
                return pipeline;
            }
        }

        public bool RemoveStation(string id) {
            lock (_lock) {
                if (string.IsNullOrWhiteSpace(id)) return false;
                if (_pipelines.Count <= 1) return false; // Prevent removing the last station

                var profile = _profileManager.CurrentProfile;
                var item = profile.Stations.FirstOrDefault(s => s.Id == id);
                if (item != null) {
                    profile.Stations.Remove(item);
                    if (_activeStationId == id) {
                        _activeStationId = profile.Stations.FirstOrDefault()?.Id ?? "";
                        profile.ActiveStationId = _activeStationId;
                    }
                    _profileManager.SaveProfile(profile);
                }

                if (_pipelines.TryRemove(id, out var pipeline)) {
                    pipeline.Dispose();
                    StationsChanged?.Invoke();
                    return true;
                }

                return false;
            }
        }

        public void StartStation(string id, MicrophoneCaptureService? sharedMic = null, LocalMusicPlayerService? localPlayer = null) {
            var station = GetStationById(id);
            station?.StartBroadcast(sharedMic, localPlayer);
        }

        public void StopStation(string id) {
            var station = GetStationById(id);
            station?.StopBroadcast();
        }

        public void StartAllStations(MicrophoneCaptureService? sharedMic = null, LocalMusicPlayerService? localPlayer = null) {
            foreach (var station in _pipelines.Values) {
                if (!station.IsLive) {
                    try {
                        station.StartBroadcast(sharedMic, localPlayer);
                    } catch { }
                }
            }
        }

        public void StopAllStations() {
            foreach (var station in _pipelines.Values) {
                if (station.IsLive) {
                    station.StopBroadcast();
                }
            }
        }

        public void Dispose() {
            StopAllStations();
            foreach (var station in _pipelines.Values) {
                station.Dispose();
            }
            _pipelines.Clear();
        }
    }
}
