using System.Collections.Generic;

namespace Scrim.Windows {
    public interface IWindowEnumerator {
        IEnumerable<WindowInfo> GetOpenWindows();
    }
}
