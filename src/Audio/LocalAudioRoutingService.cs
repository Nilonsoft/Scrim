using System;
using System.Collections.Generic;
using System.Diagnostics;
using NAudio.CoreAudioApi;

namespace Scrim.Audio {
    public class LocalAudioRoutingService : IDisposable {
        private readonly HashSet<uint> _mutedProcessIds = new();
        private readonly object _lock = new();

        public bool SetProcessMute(uint processId, bool mute) {
            if (processId == 0) return false;

            lock (_lock) {
                try {
                    // If un-muting, restore audio session volume mute flag in Windows CoreAudio
                    if (!mute) {
                        UnmuteProcess(processId);
                        _mutedProcessIds.Remove(processId);
                        return true;
                    }

                    // Windows CoreAudio Architecture Note:
                    // Setting session.SimpleAudioVolume.Mute = true stops the Windows Audio Engine
                    // from rendering samples to the endpoint, which causes loopback capture to receive silence (muting the stream too).
                    // Audio isolation without stream muting is achieved by routing the app to an isolated playback device.
                    // We record the flag here for state tracking without destructively silencing the audio engine.
                    _mutedProcessIds.Add(processId);
                    return true;
                } catch {
                    return false;
                }
            }
        }

        public void UnmuteProcess(uint processId) {
            if (processId == 0) return;

            lock (_lock) {
                try {
                    var enumerator = new MMDeviceEnumerator();
                    var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
                    var targetPids = GetProcessTreePids(processId);

                    foreach (var device in devices) {
                        try {
                            var sessions = device.AudioSessionManager.Sessions;
                            for (int i = 0; i < sessions.Count; i++) {
                                var session = sessions[i];
                                uint sessionPid = (uint)session.GetProcessID;
                                if (targetPids.Contains(sessionPid)) {
                                    session.SimpleAudioVolume.Mute = false;
                                }
                            }
                        } catch { }
                    }
                    _mutedProcessIds.Remove(processId);
                } catch { }
            }
        }

        public bool IsProcessMuted(uint processId) {
            if (processId == 0) return false;

            lock (_lock) {
                return _mutedProcessIds.Contains(processId);
            }
        }

        public void RestoreAllMuted() {
            lock (_lock) {
                try {
                    var enumerator = new MMDeviceEnumerator();
                    var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

                    foreach (var device in devices) {
                        try {
                            var sessions = device.AudioSessionManager.Sessions;
                            for (int i = 0; i < sessions.Count; i++) {
                                var session = sessions[i];
                                uint pid = (uint)session.GetProcessID;
                                if (_mutedProcessIds.Contains(pid)) {
                                    try {
                                        session.SimpleAudioVolume.Mute = false;
                                    } catch { }
                                }
                            }
                        } catch { }
                    }
                } catch { }

                _mutedProcessIds.Clear();
            }
        }

        public void UnmuteAllSessions() {
            lock (_lock) {
                try {
                    var enumerator = new MMDeviceEnumerator();
                    var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

                    foreach (var device in devices) {
                        try {
                            var sessions = device.AudioSessionManager.Sessions;
                            for (int i = 0; i < sessions.Count; i++) {
                                try {
                                    sessions[i].SimpleAudioVolume.Mute = false;
                                } catch { }
                            }
                        } catch { }
                    }
                } catch { }
                _mutedProcessIds.Clear();
            }
        }

        private HashSet<uint> GetProcessTreePids(uint rootPid) {
            var set = new HashSet<uint> { rootPid };

            try {
                var allProcs = Process.GetProcesses();
                string? rootName = null;
                try {
                    rootName = Process.GetProcessById((int)rootPid).ProcessName;
                } catch { }

                if (!string.IsNullOrEmpty(rootName)) {
                    foreach (var p in allProcs) {
                        try {
                            if (p.ProcessName.Equals(rootName, StringComparison.OrdinalIgnoreCase)) {
                                set.Add((uint)p.Id);
                            }
                        } catch { }
                    }
                }
            } catch { }

            return set;
        }

        public void Dispose() {
            RestoreAllMuted();
        }
    }
}
