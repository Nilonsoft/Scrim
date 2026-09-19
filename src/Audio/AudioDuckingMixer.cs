using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Scrim.Audio {
    public class AudioDuckingMixer {
        private readonly Channel<byte[]> _mixedOutput;
        private readonly double _duckingGain = Math.Pow(10, -14.0 / 20.0); // -14 dB
        private readonly double _noiseGateThreshold = Math.Pow(10, -40.0 / 20.0); // -40 dB
        
        // Attack 150ms, Release 350ms
        // Based on 44.1kHz block rates. A simple alpha filter.
        private double _currentGain = 1.0;
        private double _attackCoef = Math.Exp(-1.0 / (0.150 * 44100)); // Simplified per-sample coefficient
        private double _releaseCoef = Math.Exp(-1.0 / (0.350 * 44100));

        public bool PushToTalkActive { get; set; }
        public bool LatchActive { get; set; }

        public ChannelReader<byte[]> MixedStream => _mixedOutput.Reader;

        public AudioDuckingMixer() {
            _mixedOutput = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(100) {
                FullMode = BoundedChannelFullMode.DropOldest
            });
        }

        public void StartMixing(ChannelReader<byte[]> appAudio, ChannelReader<byte[]> micAudio, CancellationToken token) {
            Task.Run(async () => {
                while (!token.IsCancellationRequested) {
                    var appBuffer = await appAudio.ReadAsync(token);
                    
                    // Non-blocking read from mic if available
                    byte[] micBuffer = new byte[appBuffer.Length];
                    if (micAudio.TryRead(out var mBuf)) {
                        Array.Copy(mBuf, micBuffer, Math.Min(mBuf.Length, micBuffer.Length));
                    }

                    ProcessMix(appBuffer, micBuffer);
                }
            }, token);
        }

        private void ProcessMix(byte[] appBuffer, byte[] micBuffer) {
            // Both are assumed 16-bit stereo PCM
            byte[] outBuffer = new byte[appBuffer.Length];
            
            bool micActive = PushToTalkActive || LatchActive;
            
            // Check noise gate
            double rms = CalculateRms(micBuffer);
            if (rms > _noiseGateThreshold) {
                micActive = true;
            }

            double targetGain = micActive ? _duckingGain : 1.0;

            for (int i = 0; i < appBuffer.Length; i += 2) {
                short appSample = BitConverter.ToInt16(appBuffer, i);
                short micSample = BitConverter.ToInt16(micBuffer, i);

                // Smooth gain
                if (_currentGain > targetGain) {
                    _currentGain = _attackCoef * _currentGain + (1 - _attackCoef) * targetGain;
                } else {
                    _currentGain = _releaseCoef * _currentGain + (1 - _releaseCoef) * targetGain;
                }

                // Apply ducking to app audio
                double mixedApp = appSample * _currentGain;
                
                // Sum 2-bus
                double sum = mixedApp + micSample;

                // Soft-knee limiter (naive hard clip for now)
                if (sum > short.MaxValue) sum = short.MaxValue;
                if (sum < short.MinValue) sum = short.MinValue;

                short finalSample = (short)sum;
                byte[] sampleBytes = BitConverter.GetBytes(finalSample);
                outBuffer[i] = sampleBytes[0];
                outBuffer[i + 1] = sampleBytes[1];
            }

            _mixedOutput.Writer.TryWrite(outBuffer);
        }

        private double CalculateRms(byte[] buffer) {
            double sumSquares = 0;
            int samples = buffer.Length / 2;
            if (samples == 0) return 0;
            
            for (int i = 0; i < buffer.Length; i += 2) {
                short sample = BitConverter.ToInt16(buffer, i);
                double normalized = sample / 32768.0;
                sumSquares += normalized * normalized;
            }
            
            return Math.Sqrt(sumSquares / samples);
        }
    }
}
