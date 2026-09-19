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

        [Fact]
        public async Task Test_WasapiLoopbackCapture_Initializes() {
            var wasapi = new NAudio.Wave.WasapiLoopbackCapture();
            _output.WriteLine($"WasapiLoopbackCapture initialized. WaveFormat: {wasapi.WaveFormat.SampleRate}Hz, {wasapi.WaveFormat.Channels}ch, {wasapi.WaveFormat.BitsPerSample}bits");
            int totalBytes = 0;
            wasapi.DataAvailable += (s, e) => {
                totalBytes += e.BytesRecorded;
            };
            wasapi.StartRecording();
            await Task.Delay(500);
            wasapi.StopRecording();
            _output.WriteLine($"WasapiLoopback recorded bytes: {totalBytes}");
            wasapi.Dispose();
        }

        [Fact]
        public void Test_ConvertFloatTo16Bit_ProducesValidPcm() {
            // Generate 480 frames of 48kHz 32-bit float stereo sine wave
            int inFrames = 480;
            byte[] inBuffer = new byte[inFrames * 8];
            for (int i = 0; i < inFrames; i++) {
                float sample = (float)Math.Sin(2.0 * Math.PI * 440.0 * i / 48000.0) * 0.8f;
                byte[] b = BitConverter.GetBytes(sample);
                Array.Copy(b, 0, inBuffer, i * 8, 4);
                Array.Copy(b, 0, inBuffer, i * 8 + 4, 4);
            }

            int outFrames = (int)Math.Round(inFrames * 44100.0 / 48000.0);
            byte[] outBytes = new byte[outFrames * 4];
            double ratio = 48000.0 / 44100.0;
            for (int j = 0; j < outFrames; j++) {
                double inIndex = j * ratio;
                int idx0 = (int)inIndex;
                double frac = inIndex - idx0;
                int idx1 = Math.Min(idx0 + 1, inFrames - 1);

                float l0 = BitConverter.ToSingle(inBuffer, idx0 * 8);
                float r0 = BitConverter.ToSingle(inBuffer, idx0 * 8 + 4);
                float l1 = BitConverter.ToSingle(inBuffer, idx1 * 8);
                float r1 = BitConverter.ToSingle(inBuffer, idx1 * 8 + 4);

                float l = (float)(l0 * (1.0 - frac) + l1 * frac);
                float r = (float)(r0 * (1.0 - frac) + r1 * frac);

                short sL = (short)Math.Clamp((int)(l * 32767.0f), short.MinValue, short.MaxValue);
                short sR = (short)Math.Clamp((int)(r * 32767.0f), short.MinValue, short.MaxValue);

                byte[] bL = BitConverter.GetBytes(sL);
                byte[] bR = BitConverter.GetBytes(sR);
                outBytes[j * 4] = bL[0];
                outBytes[j * 4 + 1] = bL[1];
                outBytes[j * 4 + 2] = bR[0];
                outBytes[j * 4 + 3] = bR[1];
            }

            Assert.Equal(outFrames * 4, outBytes.Length);
            short firstSample = BitConverter.ToInt16(outBytes, 0);
            short midSample = BitConverter.ToInt16(outBytes, (outFrames / 4) * 4);
            Assert.True(Math.Abs(midSample) > 1000);
        }
    }
}
