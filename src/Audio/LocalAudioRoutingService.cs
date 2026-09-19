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
                    var enumerator = new MMDeviceEnumerator();
                    var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                    var sessionManager = device.AudioSessionManager;
                    var sessions = sessionManager.Sessions;

                    bool foundAny = false;
                    var targetPids = GetProcessTreePids(processId);

                    for (int i = 0; i < sessions.Count; i++) {
                        var session = sessions[i];
                        uint sessionPid = (uint)session.GetProcessID;

                        if (targetPids.Contains(sessionPid)) {
                            session.SimpleAudioVolume.Mute = mute;
                            foundAny = true;
                        }
                    }

                    if (mute) {
                        _mutedProcessIds.Add(processId);
                    } else {
                        _mutedProcessIds.Remove(processId);
                    }

                    return foundAny;
                } catch {
                    return false;
                }
            }
        }

        public bool IsProcessMuted(uint processId) {
            if (processId == 0) return false;

            lock (_lock) {
                try {
                    var enumerator = new MMDeviceEnumerator();
                    var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                    var sessions = device.AudioSessionManager.Sessions;

                    for (int i = 0; i < sessions.Count; i++) {
                        var session = sessions[i];
                        if (session.GetProcessID == (int)processId) {
                            return session.SimpleAudioVolume.Mute;
                        }
                    }
                } catch { }
                return _mutedProcessIds.Contains(processId);
            }
        }

        public void RestoreAllMuted() {
            lock (_lock) {
                if (_mutedProcessIds.Count == 0) return;

                try {
                    var enumerator = new MMDeviceEnumerator();
                    var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
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

                _mutedProcessIds.Clear();
            }
        }

        private HashSet<uint> GetProcessTreePids(uint rootPid) {
            var set = new HashSet<uint> { rootPid };

            try {
                var allProcs = Process.GetProcesses();
                foreach (var p in allProcs) {
                    try {
                        // Check if process name matches the root process (handles multi-process apps like Chrome, Spotify, Edge)
                        var rootProc = Process.GetProcessById((int)rootPid);
                        if (p.ProcessName.Equals(rootProc.ProcessName, StringComparison.OrdinalIgnoreCase)) {
                            set.Add((uint)p.Id);
                        }
                    } catch { }
                }
            } catch { }

            return set;
        }

        public void Dispose() {
            RestoreAllMuted();
        }
    }
}
