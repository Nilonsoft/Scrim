using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Xunit;
using Scrim.Audio;

namespace Scrim.Tests {
    public class SilenceWatchdogTests {
        [Fact]
        public async Task StartMonitoring_PassesFramesNormally() {
            var watchdog = new SilenceWatchdog();
            var inputChannel = Channel.CreateUnbounded<byte[]>();
            var cts = new CancellationTokenSource();
            
            watchdog.StartMonitoring(inputChannel.Reader, cts.Token);
            
            var testFrame = new byte[] { 5, 6, 7 };
            await inputChannel.Writer.WriteAsync(testFrame);
            
            var outputFrame = await watchdog.WatchdogStream.ReadAsync();
            Assert.Equal(testFrame, outputFrame);
            
            cts.Cancel();
        }

        [Fact]
        public async Task StartMonitoring_InjectsSilenceOnTimeout() {
            var watchdog = new SilenceWatchdog();
            var inputChannel = Channel.CreateUnbounded<byte[]>();
            var cts = new CancellationTokenSource();
            
            watchdog.StartMonitoring(inputChannel.Reader, cts.Token);
            
            // Do not write anything to input channel, wait for watchdog to inject silence
            // Timeout threshold is 500ms
            var outputFrame = await watchdog.WatchdogStream.ReadAsync();
            
            Assert.Equal(17640, outputFrame.Length); // Synthetic silence frame
            Assert.All(outputFrame, b => Assert.Equal(0, b));
            
            cts.Cancel();
        }
    }
}
