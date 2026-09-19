using System;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Scrim.Server {
    public class ClientConnectionWorker : IDisposable {
        public string ClientId { get; } = Guid.NewGuid().ToString();
        public Channel<byte[]> AudioChannel { get; }

        public ClientConnectionWorker() {
            AudioChannel = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(500) {
                FullMode = BoundedChannelFullMode.DropOldest
            });
        }

        public void Dispose() {
            AudioChannel.Writer.Complete();
        }
    }
}
