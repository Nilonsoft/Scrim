using System;
using System.Threading;
using System.Threading.Tasks;

namespace Scrim.Server {
    public interface IStreamServer : IDisposable {
        void Start(int port);
        void Stop();
    }
}
