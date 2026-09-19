using System;

namespace Scrim.Audio {
    public class VoiceChangerProcessor {
        private double _phase = 0;
        private double _alienPhase = 0;
        private double _lastSample = 0; // For simple 1-pole lowpass

        public void Process(byte[] buffer, VoiceEffect effect) {
            if (effect == VoiceEffect.Normal) return;

            int sampleCount = buffer.Length / 2;
            
            // Assume 44.1kHz sample rate
            double sampleRate = 44100.0;

            for (int i = 0; i < buffer.Length; i += 2) {
                short sample = BitConverter.ToInt16(buffer, i);
                double normalized = sample / 32768.0;

                switch (effect) {
                    case VoiceEffect.Robot:
                        // 50 Hz Ring Modulation
                        double mod = Math.Sin(_phase);
                        normalized *= mod;
                        _phase += 2.0 * Math.PI * 50.0 / sampleRate;
                        if (_phase > 2.0 * Math.PI) _phase -= 2.0 * Math.PI;
                        break;

                    case VoiceEffect.Alien:
                        // Higher frequency tremolo / ring mod (e.g. 120Hz)
                        double alienMod = Math.Sin(_alienPhase);
                        normalized *= alienMod;
                        _alienPhase += 2.0 * Math.PI * 120.0 / sampleRate;
                        if (_alienPhase > 2.0 * Math.PI) _alienPhase -= 2.0 * Math.PI;
                        break;

                    case VoiceEffect.Radio:
                        // Hard clipping distortion
                        normalized *= 4.0; // Boost
                        if (normalized > 1.0) normalized = 1.0;
                        if (normalized < -1.0) normalized = -1.0;

                        // Simple low-pass filter (approx 3kHz) to emulate cheap speaker
                        // y[n] = y[n-1] + alpha * (x[n] - y[n-1])
                        double alpha = 0.3; 
                        normalized = _lastSample + alpha * (normalized - _lastSample);
                        _lastSample = normalized;
                        break;
                }

                short processed = (short)(normalized * 32767.0);
                byte[] bytes = BitConverter.GetBytes(processed);
                buffer[i] = bytes[0];
                buffer[i + 1] = bytes[1];
            }
        }
    }
}
