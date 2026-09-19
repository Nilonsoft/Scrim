using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Scrim.Server {
    public class BroadcastHub {
        private readonly ConcurrentDictionary<string, ClientConnectionWorker> _clients = new();
        private readonly PreRollBuffer _preRollBuffer = new PreRollBuffer();
        private CancellationTokenSource? _cts;

        public int ActiveClientCount => _clients.Count;
        public bool IsBroadcasting { get; private set; } = false;
        public event EventHandler<bool>? BroadcastingStateChanged;

        public void StartBroadcasting(ChannelReader<byte[]> encodedStream) {
            StopBroadcasting();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            IsBroadcasting = true;
            BroadcastingStateChanged?.Invoke(this, true);

            Task.Run(async () => {
                while (!token.IsCancellationRequested) {
                    var frame = await encodedStream.ReadAsync(token);
                    
                    _preRollBuffer.PushFrame(frame);

                    foreach (var client in _clients.Values) {
                        client.AudioChannel.Writer.TryWrite(frame);
                    }
                }
            }, token);
        }

        public ClientConnectionWorker RegisterClient() {
            var worker = new ClientConnectionWorker();
            
            foreach (var frame in _preRollBuffer.GetBurst()) {
                worker.AudioChannel.Writer.TryWrite(frame);
            }

            _clients.TryAdd(worker.ClientId, worker);
            return worker;
        }

        public void UnregisterClient(string clientId) {
            if (_clients.TryRemove(clientId, out var worker)) {
                worker.Dispose();
            }
        }

        public void StopBroadcasting() {
            bool wasBroadcasting = IsBroadcasting;
            IsBroadcasting = false;
            _cts?.Cancel();
            foreach (var client in _clients.Values) {
                client.Dispose();
            }
            _clients.Clear();
            if (wasBroadcasting) {
                BroadcastingStateChanged?.Invoke(this, false);
            }
        }
    }
}
