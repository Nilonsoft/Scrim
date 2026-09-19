using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Xunit;
using Scrim.Audio;

namespace Scrim.Tests {
    public class AudioDuckingMixerTests {
        [Fact]
        public async Task StartMixing_WithPushToTalk_DucksAppAudio() {
            var mixer = new AudioDuckingMixer();
            var appChannel = Channel.CreateUnbounded<byte[]>();
            var micChannel = Channel.CreateUnbounded<byte[]>();
            var cts = new CancellationTokenSource();
            
            mixer.PushToTalkActive = true;
            mixer.StartMixing(appChannel.Reader, micChannel.Reader, cts.Token);
            
            // Create a loud app signal (e.g. constant max value)
            byte[] appFrame = new byte[26460]; // 300ms of audio
            for (int i = 0; i < appFrame.Length; i += 2) {
                var bytes = BitConverter.GetBytes((short)10000);
                appFrame[i] = bytes[0];
                appFrame[i + 1] = bytes[1];
            }
            
            await appChannel.Writer.WriteAsync(appFrame);
            var mixedFrame = await mixer.MixedStream.ReadAsync();
            
            // The first few samples will be 10000, but by the end of 100ms the attack curve 
            // should have reduced the amplitude significantly towards -14dB (approx 1995)
            short lastSample = BitConverter.ToInt16(mixedFrame, mixedFrame.Length - 2);
            
            Assert.True(lastSample < 5000, $"Expected ducking to reduce volume, but got {lastSample}");
            
            cts.Cancel();
        }

        [Fact]
        public async Task StartMixing_WithLoudMic_DucksAppAudio() {
            var mixer = new AudioDuckingMixer();
            var appChannel = Channel.CreateUnbounded<byte[]>();
            var micChannel = Channel.CreateUnbounded<byte[]>();
            var cts = new CancellationTokenSource();
            
            mixer.StartMixing(appChannel.Reader, micChannel.Reader, cts.Token);
            
            byte[] appFrame = new byte[26460]; // 300ms
            for (int i = 0; i < appFrame.Length; i += 2) {
                var bytes = BitConverter.GetBytes((short)10000);
                appFrame[i] = bytes[0];
                appFrame[i + 1] = bytes[1];
            }

            byte[] micFrame = new byte[26460]; // 300ms loud mic
            for (int i = 0; i < micFrame.Length; i += 2) {
                var bytes = BitConverter.GetBytes((short)20000);
                micFrame[i] = bytes[0];
                micFrame[i + 1] = bytes[1];
            }
            
            await micChannel.Writer.WriteAsync(micFrame);
            await appChannel.Writer.WriteAsync(appFrame);
            
            var mixedFrame = await mixer.MixedStream.ReadAsync();
            
            // App audio is 10000. Mic is 20000. Sum = 30000 initially.
            // Ducking should reduce the app to ~2000. Sum = ~22000.
            short lastSample = BitConverter.ToInt16(mixedFrame, mixedFrame.Length - 2);
            
            Assert.True(lastSample < 25000, $"Expected ducking to reduce mixed volume, but got {lastSample}");
            
            cts.Cancel();
        }
    }
}
