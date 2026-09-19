using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Scrim.Encoding {
    public class AacEncoder : IAudioEncoder {
        private readonly Channel<byte[]> _outputChannel;
        private CancellationTokenSource? _cts;

        public AudioFormat Format => AudioFormat.Aac;
        public int Bitrate { get; }
        public ChannelReader<byte[]> EncodedStream => _outputChannel.Reader;

        public AacEncoder(int bitrate = 128) {
            Bitrate = bitrate;
            _outputChannel = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(100) {
                FullMode = BoundedChannelFullMode.DropOldest
            });
        }

        public void StartEncoding(ChannelReader<byte[]> pcmStream) {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            Task.Run(async () => {
                // TODO: Initialize MediaFoundation AAC encoder
                while (!token.IsCancellationRequested) {
                    var pcmBuffer = await pcmStream.ReadAsync(token);
                    byte[] encodedBuffer = new byte[pcmBuffer.Length / 4]; 
                    _outputChannel.Writer.TryWrite(encodedBuffer);
                }
            }, token);
        }

        public void StopEncoding() {
            _cts?.Cancel();
        }

        public void Dispose() {
            StopEncoding();
        }
    }
}
