using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Xunit;
using Scrim.Audio;

namespace Scrim.Tests {
    public class PcmAudioResamplerTests {
        [Fact]
        public async Task StartResampling_PassesDataToOutputChannel() {
            var resampler = new PcmAudioResampler();
            var inputChannel = Channel.CreateUnbounded<byte[]>();
            var cts = new CancellationTokenSource();
            
            resampler.StartResampling(inputChannel.Reader, cts.Token);
            
            var testFrame = new byte[] { 1, 2, 3, 4 };
            await inputChannel.Writer.WriteAsync(testFrame);
            
            var outputFrame = await resampler.ResampledStream.ReadAsync();
            Assert.Equal(testFrame, outputFrame);
            
            cts.Cancel();
        }
    }
}
