using System;

namespace Scrim.Audio {
    public class VoiceChangerProcessor {
        private double _phase = 0;
        private double _alienPhase = 0;
        private double _lastSample = 0; // For simple 1-pole lowpass (Radio)

        // Real-time Granular Crossfade Pitch Shifter for Woman and Man voices
        private const int BufferSize = 4096;
        private const int WindowSize = 1470; // ~33.3ms grain window at 44.1kHz

        private readonly double[] _delayL = new double[BufferSize];
        private readonly double[] _delayR = new double[BufferSize];
        private int _writePos = 0;
        private double _grainPhase = 0.0;

        // Formant & presence filters
        private double _womanHpL = 0, _womanHpR = 0;
        private double _womanPrevL = 0, _womanPrevR = 0;
        private double _manLpL = 0, _manLpR = 0;

        public void Process(byte[] buffer, VoiceEffect effect) {
            if (effect == VoiceEffect.Normal) return;

            // Assume 44.1kHz sample rate
            double sampleRate = 44100.0;

            if (effect == VoiceEffect.Woman || effect == VoiceEffect.Man) {
                ProcessPitchShift(buffer, effect);
                return;
            }

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
                        // Higher frequency tremolo / ring mod (120Hz)
                        double alienMod = Math.Sin(_alienPhase);
                        normalized *= alienMod;
                        _alienPhase += 2.0 * Math.PI * 120.0 / sampleRate;
                        if (_alienPhase > 2.0 * Math.PI) _alienPhase -= 2.0 * Math.PI;
                        break;

                    case VoiceEffect.Radio:
                        // Hard clipping distortion + low-pass filter
                        normalized *= 4.0;
                        if (normalized > 1.0) normalized = 1.0;
                        if (normalized < -1.0) normalized = -1.0;

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

        private void ProcessPitchShift(byte[] buffer, VoiceEffect effect) {
            // Target pitch ratios: Woman ~ +4.2 semitones (1.28), Man ~ -4.5 semitones (0.77)
            double pitchRatio = (effect == VoiceEffect.Woman) ? 1.28 : 0.77;
            double phaseDelta = (1.0 - pitchRatio) / WindowSize;

            bool isStereo = buffer.Length >= 4 && (buffer.Length % 4 == 0);
            int step = isStereo ? 4 : 2;

            for (int i = 0; i < buffer.Length; i += step) {
                short sampleL = BitConverter.ToInt16(buffer, i);
                short sampleR = isStereo ? BitConverter.ToInt16(buffer, i + 2) : sampleL;

                double inL = sampleL / 32768.0;
                double inR = sampleR / 32768.0;

                // Push to delay line
                _delayL[_writePos] = inL;
                _delayR[_writePos] = inR;

                // Calculate dual-tap grain positions and Hann window crossfades
                double phase1 = _grainPhase;
                double phase2 = phase1 + 0.5;
                if (phase2 >= 1.0) phase2 -= 1.0;

                double delay1 = phase1 * WindowSize;
                double delay2 = phase2 * WindowSize;

                double readPos1 = _writePos - delay1;
                while (readPos1 < 0) readPos1 += BufferSize;
                while (readPos1 >= BufferSize) readPos1 -= BufferSize;

                double readPos2 = _writePos - delay2;
                while (readPos2 < 0) readPos2 += BufferSize;
                while (readPos2 >= BufferSize) readPos2 -= BufferSize;

                double w1 = 0.5 * (1.0 - Math.Cos(2.0 * Math.PI * phase1));
                double w2 = 0.5 * (1.0 - Math.Cos(2.0 * Math.PI * phase2));

                double outL = (ReadInterpolated(_delayL, readPos1) * w1) + (ReadInterpolated(_delayL, readPos2) * w2);
                double outR = isStereo ? (ReadInterpolated(_delayR, readPos1) * w1) + (ReadInterpolated(_delayR, readPos2) * w2) : outL;

                // Vocal tract formant and spectral shaping
                if (effect == VoiceEffect.Woman) {
                    // High-pass filter removes chest rumble (<200Hz) and boosts feminine presence/clarity
                    _womanHpL = 0.92 * (_womanHpL + outL - _womanPrevL);
                    _womanPrevL = outL;
                    outL = _womanHpL * 1.2;

                    if (isStereo) {
                        _womanHpR = 0.92 * (_womanHpR + outR - _womanPrevR);
                        _womanPrevR = outR;
                        outR = _womanHpR * 1.2;
                    }
                } else if (effect == VoiceEffect.Man) {
                    // Low-pass filter adds chest resonance and warmth (<3.5kHz)
                    _manLpL = _manLpL + 0.55 * (outL - _manLpL);
                    outL = _manLpL * 1.25;

                    if (isStereo) {
                        _manLpR = _manLpR + 0.55 * (outR - _manLpR);
                        outR = _manLpR * 1.25;
                    }
                }

                // Soft limiter clamp
                if (outL > 1.0) outL = 1.0;
                if (outL < -1.0) outL = -1.0;
                if (outR > 1.0) outR = 1.0;
                if (outR < -1.0) outR = -1.0;

                short finalL = (short)(outL * 32767.0);
                byte[] bytesL = BitConverter.GetBytes(finalL);
                buffer[i] = bytesL[0];
                buffer[i + 1] = bytesL[1];

                if (isStereo) {
                    short finalR = (short)(outR * 32767.0);
                    byte[] bytesR = BitConverter.GetBytes(finalR);
                    buffer[i + 2] = bytesR[0];
                    buffer[i + 3] = bytesR[1];
                }

                // Advance grain phase and write index
                _grainPhase += phaseDelta;
                while (_grainPhase >= 1.0) _grainPhase -= 1.0;
                while (_grainPhase < 0.0) _grainPhase += 1.0;

                _writePos = (_writePos + 1) % BufferSize;
            }
        }

        private static double ReadInterpolated(double[] buffer, double pos) {
            int idx1 = (int)Math.Floor(pos);
            double frac = pos - idx1;
            int idx2 = (idx1 + 1) % BufferSize;
            if (idx1 < 0) idx1 += BufferSize;
            if (idx2 < 0) idx2 += BufferSize;
            return buffer[idx1] * (1.0 - frac) + buffer[idx2] * frac;
        }
    }
}
