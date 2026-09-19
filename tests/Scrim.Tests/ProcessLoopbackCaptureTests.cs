using System;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Scrim.Audio;

namespace Scrim.Tests {
    public class ProcessLoopbackCaptureTests {
        private readonly ITestOutputHelper _output;

        public ProcessLoopbackCaptureTests(ITestOutputHelper output) {
            _output = output;
        }

        [Fact]
        public async Task StartCapture_SystemAudio_CapturesOrInitializes() {
            var capture = new ProcessLoopbackCapture();
            capture.StartCapture(0);
            
            _output.WriteLine("StartCapture(0) invoked. Waiting 1.5s for audio or initialization...");
            var delayTask = Task.Delay(1500);
            
            byte[]? received = null;
            var readTask = Task.Run(async () => {
                try {
                    received = await capture.AudioStream.ReadAsync();
                } catch (Exception ex) {
                    _output.WriteLine($"Read exception: {ex}");
                }
            });

            await Task.WhenAny(readTask, delayTask);
            
            _output.WriteLine($"Received bytes: {(received != null ? received.Length : 0)}");
            capture.StopCapture();
        }
    }
}
