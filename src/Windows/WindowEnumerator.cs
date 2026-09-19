using System.Collections.Generic;
using System.Text;
using Scrim.Interop;

namespace Scrim.Windows {
    public class WindowEnumerator : IWindowEnumerator {
        public IEnumerable<WindowInfo> GetOpenWindows() {
            var windows = new List<WindowInfo>();

            NativeMethods.EnumWindows((hWnd, lParam) => {
                if (NativeMethods.IsWindowVisible(hWnd)) {
                    int length = NativeMethods.GetWindowTextLength(hWnd);
                    if (length > 0) {
                        StringBuilder sb = new StringBuilder(length + 1);
                        NativeMethods.GetWindowText(hWnd, sb, sb.Capacity);
                        string title = sb.ToString();

                        NativeMethods.GetWindowThreadProcessId(hWnd, out uint processId);
                        
                        windows.Add(new WindowInfo(hWnd, title, processId));
                    }
                }
                return true;
            }, 0);

            return windows;
        }
    }
}
