using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Scrim.Audio {
    public class PcmAudioResampler {
        private readonly Channel<byte[]> _outputChannel;

        public ChannelReader<byte[]> ResampledStream => _outputChannel.Reader;

        public PcmAudioResampler() {
            _outputChannel = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(100) {
                FullMode = BoundedChannelFullMode.DropOldest
            });
        }

        public void StartResampling(ChannelReader<byte[]> inputStream, CancellationToken token) {
            Task.Run(async () => {
                await foreach (var buffer in inputStream.ReadAllAsync(token)) {
                    // For now, assume it's already in 44.1kHz 16-bit format based on our WASAPI format struct.
                    // Future: implement high-quality SRC if WASAPI forces a different mix format.
                    _outputChannel.Writer.TryWrite(buffer);
                }
            }, token);
        }
    }
}
