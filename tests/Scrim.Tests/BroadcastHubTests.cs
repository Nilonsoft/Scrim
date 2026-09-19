using System.Threading.Channels;
using System.Threading.Tasks;
using Xunit;
using Scrim.Server;

namespace Scrim.Tests {
    public class BroadcastHubTests {
        [Fact]
        public async Task RegisterClient_ReceivesBroadcastFrames() {
            var hub = new BroadcastHub();
            var encoderChannel = Channel.CreateUnbounded<byte[]>();
            
            hub.StartBroadcasting(encoderChannel.Reader);
            
            var client = hub.RegisterClient();
            Assert.Equal(1, hub.ActiveClientCount);
            
            byte[] frame = new byte[] { 10, 20, 30 };
            await encoderChannel.Writer.WriteAsync(frame);
            
            var receivedFrame = await client.AudioChannel.Reader.ReadAsync();
            Assert.Equal(frame, receivedFrame);
            
            hub.StopBroadcasting();
        }

        [Fact]
        public void UnregisterClient_RemovesClient() {
            var hub = new BroadcastHub();
            var client = hub.RegisterClient();
            Assert.Equal(1, hub.ActiveClientCount);
            
            hub.UnregisterClient(client.ClientId);
            Assert.Equal(0, hub.ActiveClientCount);
        }

        [Fact]
        public async Task RegisterClient_ReceivesPreRollBurst() {
            var hub = new BroadcastHub();
            var encoderChannel = Channel.CreateUnbounded<byte[]>();
            
            hub.StartBroadcasting(encoderChannel.Reader);
            
            // Push frames before client joins
            await encoderChannel.Writer.WriteAsync(new byte[] { 1 });
            await encoderChannel.Writer.WriteAsync(new byte[] { 2 });
            
            // Wait slightly for background thread to process frames
            await Task.Delay(50);
            
            var client = hub.RegisterClient();
            
            var burst1 = await client.AudioChannel.Reader.ReadAsync();
            var burst2 = await client.AudioChannel.Reader.ReadAsync();
            
            Assert.Equal(1, burst1[0]);
            Assert.Equal(2, burst2[0]);
            
            hub.StopBroadcasting();
        }
    }
}
