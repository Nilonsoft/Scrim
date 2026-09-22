using System;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Scrim.Server {
    public class ClientConnectionWorker : IDisposable {
        public string ClientId { get; } = Guid.NewGuid().ToString();
        public Channel<byte[]> AudioChannel { get; }
        public string IpAddress { get; set; } = "127.0.0.1";
        public string UserAgent { get; set; } = "Web Player";
        public string MountPoint { get; set; } = "stream";
        public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;
        public long BytesTransferred { get; set; } = 0;
        public System.Threading.CancellationTokenSource? Cts { get; set; }

        public ClientConnectionWorker() {
            AudioChannel = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(500) {
                FullMode = BoundedChannelFullMode.DropOldest
            });
        }

        public void Dispose() {
            AudioChannel.Writer.Complete();
            try {
                Cts?.Cancel();
                Cts?.Dispose();
            } catch { }
        }
    }
}
