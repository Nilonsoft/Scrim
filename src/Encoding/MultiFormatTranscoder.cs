using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Scrim.Encoding {
    public class MultiFormatTranscoder {
        private IAudioEncoder? _currentEncoder;
        private readonly Channel<byte[]> _transcodedOutput;
        private CancellationTokenSource? _cts;

        public ChannelReader<byte[]> OutputStream => _transcodedOutput.Reader;

        public MultiFormatTranscoder() {
            _transcodedOutput = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(100) {
                FullMode = BoundedChannelFullMode.DropOldest
            });
        }

        public void SetFormat(AudioFormat format, int bitrate) {
            _currentEncoder?.Dispose();
            
            _currentEncoder = format switch {
                AudioFormat.Mp3 => new LameMp3Encoder(bitrate),
                AudioFormat.Aac => new AacEncoder(bitrate),
                AudioFormat.Opus => new OpusEncoder(bitrate),
                _ => throw new NotSupportedException()
            };
        }

        public void StartTranscoding(ChannelReader<byte[]> duckedPcmStream) {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            if (_currentEncoder == null) {
                SetFormat(AudioFormat.Mp3, 128);
            }

            _currentEncoder!.StartEncoding(duckedPcmStream);

            Task.Run(async () => {
                while (!token.IsCancellationRequested) {
                    var encodedBuf = await _currentEncoder.EncodedStream.ReadAsync(token);
                    _transcodedOutput.Writer.TryWrite(encodedBuf);
                }
            }, token);
        }

        public void StopTranscoding() {
            _cts?.Cancel();
            _currentEncoder?.Dispose();
        }
    }
}
