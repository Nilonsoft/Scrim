using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            var windows = new List<WindowInfo>();
            var seen = new HashSet<(uint, string)>();
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

                        if (processId != myPid && !seen.Contains((processId, title))) {
                            try {
                                var process = Process.GetProcessById((int)processId);
                                seen.Add((processId, title));
                                windows.Add(new WindowInfo {
                                    ProcessId = processId,
                                    Title = title,
                                    ProcessName = process.ProcessName
                                });
                            } catch { }
                        }
                    }
                }
                return true;
            }, nint.Zero);

            return windows;
        }
    }
}
