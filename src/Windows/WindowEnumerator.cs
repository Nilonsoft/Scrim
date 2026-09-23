using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Scrim.Windows {
    public class WindowEnumerator : IWindowEnumerator {
        private delegate bool EnumWindowsProc(nint hWnd, nint lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, nint lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(nint hWnd, StringBuilder strText, int maxCount);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(nint hWnd, out uint processId);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(nint hWnd);

        public IEnumerable<WindowInfo> GetActiveWindows() {
            var windowMap = new Dictionary<uint, WindowInfo>();
            uint myPid = (uint)Environment.ProcessId;

            EnumWindows((hWnd, lParam) => {
                if (IsWindowVisible(hWnd)) {
                    var sb = new StringBuilder(256);
                    GetWindowText(hWnd, sb, sb.Capacity);
                    string title = sb.ToString().Trim();

                    if (!string.IsNullOrWhiteSpace(title) &&
                        title != "Default IME" &&
                        title != "MSCTFIME UI" &&
                        title != "Program Manager") {
                        GetWindowThreadProcessId(hWnd, out uint processId);

                        if (processId != myPid) {
                            try {
                                if (windowMap.TryGetValue(processId, out var existing)) {
                                    if (title.Length > existing.Title.Length) {
                                        existing.Title = title;
                                    }
                                } else {
                                    var process = Process.GetProcessById((int)processId);
                                    windowMap[processId] = new WindowInfo {
                                        ProcessId = processId,
                                        Title = title,
                                        ProcessName = process.ProcessName
                                    };
                                }
                            } catch { }
                        }
                    }
                }
                return true;
            }, nint.Zero);

            // Ensure running music players (like Spotify) are always present even when minimized to background or tray
            try {
                string[] knownAudioApps = new[] { "Spotify", "AppleMusic", "TIDAL", "foobar2000", "vlc", "MusicBee", "AIMP", "iTunes" };
                foreach (var appName in knownAudioApps) {
                    try {
                        var procs = Process.GetProcessesByName(appName);
                        if (procs.Length > 0) {
                            bool alreadyInMap = windowMap.Values.Any(w => w.ProcessName.Equals(appName, StringComparison.OrdinalIgnoreCase));
                            if (!alreadyInMap) {
                                var mainProc = procs.OrderBy(p => {
                                    try {
                                        return p.StartTime;
                                    } catch {
                                        return DateTime.MaxValue;
                                    }
                                }).First();

                                string title = appName;
                                foreach (var p in procs) {
                                    try {
                                        if (!string.IsNullOrWhiteSpace(p.MainWindowTitle)) {
                                            title = p.MainWindowTitle;
                                            break;
                                        }
                                    } catch { }
                                }

                                uint mainPid = (uint)mainProc.Id;
                                windowMap[mainPid] = new WindowInfo {
                                    ProcessId = mainPid,
                                    Title = title,
                                    ProcessName = appName
                                };
                            }
                        }
                    } catch { }
                }
            } catch { }

            // Identify processes currently playing audio via Windows CoreAudio API
            try {
                using var enumerator = new NAudio.CoreAudioApi.MMDeviceEnumerator();
                var devices = enumerator.EnumerateAudioEndPoints(NAudio.CoreAudioApi.DataFlow.Render, NAudio.CoreAudioApi.DeviceState.Active);
                foreach (var device in devices) {
                    try {
                        var sessionManager = device.AudioSessionManager;
                        if (sessionManager == null) continue;
                        var sessions = sessionManager.Sessions;
                        for (int i = 0; i < sessions.Count; i++) {
                            try {
                                var session = sessions[i];
                                uint pid = (uint)session.GetProcessID;
                                if (pid == 0 || pid == myPid) continue;

                                bool isPlaying = false;
                                if (session.State == NAudio.CoreAudioApi.Interfaces.AudioSessionState.AudioSessionStateActive) {
                                    isPlaying = true;
                                }
                                try {
                                    if (session.AudioMeterInformation != null && session.AudioMeterInformation.MasterPeakValue > 0.0001f) {
                                        isPlaying = true;
                                    }
                                } catch { }

                                if (isPlaying) {
                                    if (windowMap.TryGetValue(pid, out var existingInfo)) {
                                        existingInfo.IsPlayingAudio = true;
                                    } else {
                                        try {
                                            var proc = Process.GetProcessById((int)pid);
                                            string pName = proc.ProcessName;
                                            string title = !string.IsNullOrWhiteSpace(proc.MainWindowTitle) ? proc.MainWindowTitle : pName;
                                            windowMap[pid] = new WindowInfo {
                                                ProcessId = pid,
                                                ProcessName = pName,
                                                Title = title,
                                                IsPlayingAudio = true
                                            };
                                        } catch { }
                                    }
                                }
                            } catch { }
                        }
                    } catch { }
                }
            } catch { }

            return windowMap.Values
                .OrderByDescending(w => w.IsPlayingAudio)
                .ThenBy(w => w.ProcessName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(w => w.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
