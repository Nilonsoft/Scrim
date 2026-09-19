using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Scrim.Encoding {
    public class AacEncoder : IAudioEncoder {
        private readonly Channel<byte[]> _outputChannel;
        private CancellationTokenSource? _cts;
        private Process? _ffmpegProcess;

        public AudioFormat Format => AudioFormat.Aac;
        public int Bitrate { get; }
        public ChannelReader<byte[]> EncodedStream => _outputChannel.Reader;

        public AacEncoder(int bitrate = 128) {
            Bitrate = bitrate;
            _outputChannel = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(500) {
                FullMode = BoundedChannelFullMode.DropOldest
            });
        }

        public void StartEncoding(ChannelReader<byte[]> pcmStream) {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            var psi = new ProcessStartInfo {
                FileName = "ffmpeg",
                Arguments = $"-f s16le -ar 44100 -ac 2 -i pipe:0 -c:a aac -b:a {Bitrate}k -f adts pipe:1",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            try {
                _ffmpegProcess = Process.Start(psi);
                
                Task.Run(async () => {
                    using var stdin = _ffmpegProcess!.StandardInput.BaseStream;
                    while (!token.IsCancellationRequested) {
                        var pcmBuffer = await pcmStream.ReadAsync(token);
                        await stdin.WriteAsync(pcmBuffer, token);
                    }
                }, token);

                Task.Run(async () => {
                    using var stdout = _ffmpegProcess!.StandardOutput.BaseStream;
                    byte[] buffer = new byte[4096];
                    int bytesRead;
                    while (!token.IsCancellationRequested && (bytesRead = await stdout.ReadAsync(buffer, token)) > 0) {
                        byte[] chunk = new byte[bytesRead];
                        Array.Copy(buffer, chunk, bytesRead);
                        _outputChannel.Writer.TryWrite(chunk);
                    }
                }, token);
                
            } catch {
                // FFmpeg not found, fallback to silent failure
            }
        }

        public void StopEncoding() {
            _cts?.Cancel();
            try {
                if (_ffmpegProcess != null && !_ffmpegProcess.HasExited) {
                    _ffmpegProcess.Kill();
                }
            } catch { }
        }

        public void Dispose() {
            StopEncoding();
            _ffmpegProcess?.Dispose();
        }
    }
}
