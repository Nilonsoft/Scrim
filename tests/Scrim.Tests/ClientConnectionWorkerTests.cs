using System.Threading.Channels;
using System.Threading.Tasks;
using Xunit;
using Scrim.Server;

namespace Scrim.Tests {
    public class ClientConnectionWorkerTests {
        [Fact]
        public async Task Worker_DropsOldestFrames_WhenBufferFull() {
            var worker = new ClientConnectionWorker();
            
            // The capacity is 500. Let's write 501 items.
            for (int i = 0; i < 501; i++) {
                await worker.AudioChannel.Writer.WriteAsync(new byte[] { (byte)(i % 255) });
            }
            
            // The first read should be 1 (because 0 was dropped when 500th frame was added)
            var readItem = await worker.AudioChannel.Reader.ReadAsync();
            Assert.Equal(1, readItem[0]);
            
            worker.Dispose();
        }

        [Fact]
        public async Task Dispose_CompletesAudioChannel() {
            var worker = new ClientConnectionWorker();
            worker.Dispose();
            
            await Assert.ThrowsAsync<ChannelClosedException>(async () => await worker.AudioChannel.Reader.ReadAsync());
        }
    }
}
