using System;
using Xunit;
using Scrim.Audio;

namespace Scrim.Tests {
    public class DjEqualizerProcessorTests {
        [Fact]
        public void StereoWidth_MonoSum_OutputsIdenticalLeftAndRight() {
            var dsp = new DjEqualizerProcessor {
                MusicStereoWidth = 0.0f // Pure Mono Sum
            };

            // 100 frames of 16-bit stereo: Left has signal, Right is silent
            byte[] buffer = new byte[400];
            for (int i = 0; i < buffer.Length; i += 4) {
                var bytesL = BitConverter.GetBytes((short)20000);
                buffer[i] = bytesL[0];
                buffer[i + 1] = bytesL[1];
                // Right is 0
                buffer[i + 2] = 0;
                buffer[i + 3] = 0;
            }

            dsp.ProcessMusicBuffer(buffer, buffer.Length);

            // In mono sum, Left and Right should now both have half the original Left sample (~10000)
            short outL = BitConverter.ToInt16(buffer, 0);
            short outR = BitConverter.ToInt16(buffer, 2);

            Assert.Equal(outL, outR);
            Assert.InRange(outL, 9000, 11000);
        }

        [Fact]
        public void LowKill_SignificantlyAttenuatesLowFrequency() {
            var dsp = new DjEqualizerProcessor();

            // Generate 100Hz sine wave (50ms)
            const int sampleRate = 44100;
            const float frequency = 100.0f;
            int sampleCount = (int)(0.050 * sampleRate);
            byte[] normalBuffer = new byte[sampleCount * 4];
            byte[] killedBuffer = new byte[sampleCount * 4];

            for (int i = 0; i < sampleCount; i++) {
                short val = (short)(MathF.Sin(2.0f * MathF.PI * frequency * i / sampleRate) * 15000.0f);
                var b = BitConverter.GetBytes(val);
                normalBuffer[i * 4] = b[0];
                normalBuffer[i * 4 + 1] = b[1];
                normalBuffer[i * 4 + 2] = b[0];
                normalBuffer[i * 4 + 3] = b[1];

                killedBuffer[i * 4] = b[0];
                killedBuffer[i * 4 + 1] = b[1];
                killedBuffer[i * 4 + 2] = b[0];
                killedBuffer[i * 4 + 3] = b[1];
            }

            // Normal processing (flat)
            dsp.ProcessMusicBuffer(normalBuffer, normalBuffer.Length);

            // Enable Low Kill
            dsp.LowKill = true;
            dsp.RecalculateFilters();
            dsp.ProcessMusicBuffer(killedBuffer, killedBuffer.Length);

            // Measure peak amplitude in last 20% of frame to allow IIR filter settling
            int startSample = (int)(sampleCount * 0.8);
            float maxNormal = 0f;
            float maxKilled = 0f;

            for (int i = startSample; i < sampleCount; i++) {
                short nL = BitConverter.ToInt16(normalBuffer, i * 4);
                short kL = BitConverter.ToInt16(killedBuffer, i * 4);
                if (Math.Abs(nL) > maxNormal) maxNormal = Math.Abs(nL);
                if (Math.Abs(kL) > maxKilled) maxKilled = Math.Abs(kL);
            }

            Assert.True(maxKilled < maxNormal * 0.25f,
                $"LowKill should attenuate 100Hz by at least 75%, got normal: {maxNormal}, killed: {maxKilled}");
        }

        [Fact]
        public void ColorFilter_LowPass_AttenuatesHighFrequency() {
            var dsp = new DjEqualizerProcessor {
                ColorFilterKnob = -80.0f // Strong LPF
            };
            dsp.RecalculateFilters();

            // Generate 8kHz high-frequency tone (50ms)
            const int sampleRate = 44100;
            const float frequency = 8000.0f;
            int sampleCount = (int)(0.050 * sampleRate);
            byte[] buffer = new byte[sampleCount * 4];

            for (int i = 0; i < sampleCount; i++) {
                short val = (short)(MathF.Sin(2.0f * MathF.PI * frequency * i / sampleRate) * 15000.0f);
                var b = BitConverter.GetBytes(val);
                buffer[i * 4] = b[0];
                buffer[i * 4 + 1] = b[1];
                buffer[i * 4 + 2] = b[0];
                buffer[i * 4 + 3] = b[1];
            }

            dsp.ProcessMusicBuffer(buffer, buffer.Length);

            int startSample = (int)(sampleCount * 0.8);
            float maxL = 0f;
            for (int i = startSample; i < sampleCount; i++) {
                short sample = BitConverter.ToInt16(buffer, i * 4);
                if (Math.Abs(sample) > maxL) maxL = Math.Abs(sample);
            }

            Assert.True(maxL < 3000.0f, $"LPF should heavily attenuate 8kHz, but max peak was {maxL}");
        }

        [Fact]
        public void SoftLimiter_PreventsDigitalClipping() {
            var dsp = new DjEqualizerProcessor {
                SoftLimiterEnabled = true
            };

            // Input 3x over 0dBFS (e.g. 100,000)
            (short outL, short outR) = dsp.ProcessMasterSample(100000.0, -100000.0);

            Assert.InRange(outL, (short)0, short.MaxValue);
            Assert.InRange(outR, short.MinValue, (short)0);
            Assert.True(outL <= short.MaxValue, "Output must not overflow 16-bit integer bounds");
        }

        [Fact]
        public void CalculateResponseCurve_ReturnsValidPoints() {
            var dsp = new DjEqualizerProcessor {
                LowGainDb = 6.0f,
                HighGainDb = -4.0f
            };
            dsp.RecalculateFilters();

            float[] curve = dsp.CalculateResponseCurve(50);
            Assert.Equal(50, curve.Length);

            // Bass frequency (first point) should be boosted (+ dB)
            Assert.True(curve[0] > 1.0f, $"Bass point should be boosted, got {curve[0]} dB");
            // Treble frequency (last point) should be attenuated (- dB)
            Assert.True(curve[49] < -1.0f, $"Treble point should be cut, got {curve[49]} dB");
        }
    }
}
