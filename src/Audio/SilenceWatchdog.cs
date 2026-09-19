using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Scrim.Audio {
    public class SilenceWatchdog {
        private readonly Channel<byte[]> _outputChannel;
        private readonly TimeSpan _timeoutThreshold = TimeSpan.FromMilliseconds(500);

        public ChannelReader<byte[]> WatchdogStream => _outputChannel.Reader;

        public SilenceWatchdog() {
            _outputChannel = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(100) {
                FullMode = BoundedChannelFullMode.DropOldest
            });
        }

        public void StartMonitoring(ChannelReader<byte[]> inputStream, CancellationToken token) {
            Task.Run(async () => {
                var timeoutTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);
                
                while (!token.IsCancellationRequested) {
                    try {
                        var buffer = await inputStream.ReadAsync(timeoutTokenSource.Token).AsTask().WaitAsync(_timeoutThreshold, token);
                        _outputChannel.Writer.TryWrite(buffer);
                    } catch (TimeoutException) {
                        // Generate synthetic silence frame if we timed out
                        // 44.1kHz * 2 channels * 2 bytes = 176,400 bytes/sec
                        // For 100ms of silence: 17,640 bytes
                        byte[] silenceBuffer = new byte[17640];
                        _outputChannel.Writer.TryWrite(silenceBuffer);
                    } catch (OperationCanceledException) {
                        break;
                    } catch (ChannelClosedException) {
                        break;
                    }
                }
                _outputChannel.Writer.Complete();
            }, token);
        }
    }
}
