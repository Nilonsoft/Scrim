using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Scrim.Encoding {
    public class MultiFormatTranscoder {
        private readonly object _lock = new object();
        private IAudioEncoder? _currentEncoder;
        private readonly Channel<byte[]> _transcodedOutput;
        private Channel<byte[]>? _currentEncoderPcmChannel;
        private ChannelReader<byte[]>? _inputPcmStream;
        private CancellationTokenSource? _mainCts;
        private CancellationTokenSource? _encoderCts;
        private AudioFormat _currentFormat = AudioFormat.Mp3;
        private int _currentBitrate = 128;
        private bool _isTranscoding = false;

        public ChannelReader<byte[]> OutputStream => _transcodedOutput.Reader;
        public AudioFormat CurrentFormat => _currentFormat;
        public int CurrentBitrate => _currentBitrate;

        public MultiFormatTranscoder() {
            _transcodedOutput = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(100) {
                SingleReader = false,
                SingleWriter = true,
                FullMode = BoundedChannelFullMode.DropOldest
            });
        }

        public void FlushOutput() {
            while (_transcodedOutput.Reader.TryRead(out _)) { }
        }

        public void SetFormat(AudioFormat format, int bitrate) {
            lock (_lock) {
                _currentFormat = format;
                _currentBitrate = bitrate;

                if (_isTranscoding && _inputPcmStream != null) {
                    // Hot-swap active encoder immediately without stopping the main transcoded stream
                    RestartEncoderInternal();
                }
            }
        }

        public void StartTranscoding(ChannelReader<byte[]> duckedPcmStream) {
            lock (_lock) {
                StopTranscoding();
                FlushOutput();
                _isTranscoding = true;
                _inputPcmStream = duckedPcmStream;
                _mainCts = new CancellationTokenSource();
                var mainToken = _mainCts.Token;

                RestartEncoderInternal();

                // Continuous loop reading incoming PCM and forwarding to the active encoder's input channel
                Task.Run(async () => {
                    try {
                        while (!mainToken.IsCancellationRequested) {
                            var pcmBuffer = await duckedPcmStream.ReadAsync(mainToken);
                            var pcmChan = _currentEncoderPcmChannel;
                            pcmChan?.Writer.TryWrite(pcmBuffer);
                        }
                    } catch { }
                }, mainToken);
            }
        }

        private void RestartEncoderInternal() {
            // Cancel and clean up previous encoder worker
            _encoderCts?.Cancel();
            try {
                _currentEncoder?.Dispose();
            } catch { }

            _encoderCts = new CancellationTokenSource();
            var token = _encoderCts.Token;

            _currentEncoder = _currentFormat switch {
                AudioFormat.Mp3 => new LameMp3Encoder(_currentBitrate),
                AudioFormat.Aac => new AacEncoder(_currentBitrate),
                AudioFormat.Opus => new OpusEncoder(_currentBitrate),
                AudioFormat.Flac => new FlacEncoder(_currentBitrate),
                _ => new LameMp3Encoder(_currentBitrate)
            };

            _currentEncoderPcmChannel = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(50) {
                SingleReader = true,
                SingleWriter = true,
                FullMode = BoundedChannelFullMode.DropOldest
            });

            _currentEncoder.StartEncoding(_currentEncoderPcmChannel.Reader);

            var activeEncoder = _currentEncoder;
            Task.Run(async () => {
                try {
                    while (!token.IsCancellationRequested) {
                        var encodedBuf = await activeEncoder.EncodedStream.ReadAsync(token);
                        _transcodedOutput.Writer.TryWrite(encodedBuf);
                    }
                } catch { }
            }, token);
        }

        public void StopTranscoding() {
            lock (_lock) {
                _isTranscoding = false;
                _mainCts?.Cancel();
                _encoderCts?.Cancel();
                try {
                    _currentEncoder?.Dispose();
                } catch { }
                _currentEncoder = null;
                _currentEncoderPcmChannel = null;
                _inputPcmStream = null;
                FlushOutput();
            }
        }
    }
}
