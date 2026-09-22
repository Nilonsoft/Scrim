using System;

namespace Scrim.Audio {
    /// <summary>
    /// Professional DJ Stereo Mixer & Multi-Band Equalizer DSP Engine.
    /// Supports 3-Band DJ Isolator with Kill switches, DJ Sound Color Filter (HPF/LPF),
    /// 10-Band ISO Graphic EQ, Mid-Side Stereo Width Expander, Channel Panning, and Soft Limiter.
    /// </summary>
    public class DjEqualizerProcessor {
        public const int SampleRate = 44100;

        // --- Channel Strip: Music Deck ---
        public float MusicTrimDb { get; set; } = 0.0f;
        public float MusicPan { get; set; } = 0.0f; // -1.0 (Left) to +1.0 (Right)
        public float MusicStereoWidth { get; set; } = 1.0f; // 0.0 (Mono) to 2.0 (Super-Wide)
        public bool MusicMuted { get; set; } = false;
        public bool MusicSolo { get; set; } = false;

        // --- Channel Strip: Microphone ---
        public float MicGainDb { get; set; } = 0.0f;
        public float MicPan { get; set; } = 0.0f;
        public bool MicLowCut { get; set; } = true; // 80Hz rumble high-pass filter
        public bool MicMuted { get; set; } = false;

        // --- Master Bus ---
        public float MasterGainDb { get; set; } = 0.0f;
        public float MasterBalance { get; set; } = 0.0f;
        public bool SoftLimiterEnabled { get; set; } = true;
        public bool EqBypass { get; set; } = false;

        // --- Peak VU Meters (0.0 to 1.0+) ---
        public float MusicPeakL { get; private set; }
        public float MusicPeakR { get; private set; }
        public float MicPeakL { get; private set; }
        public float MicPeakR { get; private set; }
        public float MasterPeakL { get; private set; }
        public float MasterPeakR { get; private set; }

        // --- 3-Band DJ Isolator ---
        public float LowGainDb { get; set; } = 0.0f; // -26dB to +6dB
        public float MidGainDb { get; set; } = 0.0f;
        public float HighGainDb { get; set; } = 0.0f;

        public bool LowKill { get; set; } = false;
        public bool MidKill { get; set; } = false;
        public bool HighKill { get; set; } = false;

        // --- DJ Sound Color Filter Knob (-100 to +100) ---
        // < 0: Low-Pass Filter (cuts highs); > 0: High-Pass Filter (cuts lows); 0: Bypassed
        public float ColorFilterKnob { get; set; } = 0.0f;

        // --- 10-Band Graphic EQ ---
        public bool Is10BandMode { get; set; } = false;
        public static readonly float[] IsoFrequencies = new float[] {
            31.25f, 62.5f, 125f, 250f, 500f, 1000f, 2000f, 4000f, 8000f, 16000f
        };
        public float[] Bands10Db { get; } = new float[10];

        // Bi-quad IIR Filter Instances
        private readonly BiquadFilter _isoLowL = new();
        private readonly BiquadFilter _isoLowR = new();
        private readonly BiquadFilter _isoMidL = new();
        private readonly BiquadFilter _isoMidR = new();
        private readonly BiquadFilter _isoHighL = new();
        private readonly BiquadFilter _isoHighR = new();

        private readonly BiquadFilter _colorLpfL = new();
        private readonly BiquadFilter _colorLpfR = new();
        private readonly BiquadFilter _colorHpfL = new();
        private readonly BiquadFilter _colorHpfR = new();

        private readonly BiquadFilter _micHpfL = new();
        private readonly BiquadFilter _micHpfR = new();

        private readonly BiquadFilter[] _eq10L = new BiquadFilter[10];
        private readonly BiquadFilter[] _eq10R = new BiquadFilter[10];

        private readonly object _dspLock = new();

        public DjEqualizerProcessor() {
            for (int i = 0; i < 10; i++) {
                _eq10L[i] = new BiquadFilter();
                _eq10R[i] = new BiquadFilter();
            }
            RecalculateFilters();
        }

        /// <summary>
        /// Recalculates all filter coefficients. Call when gains, frequencies, or filter parameters change.
        /// </summary>
        public void RecalculateFilters() {
            lock (_dspLock) {
                // 3-Band Isolator:
                // Low shelf at 300Hz, High shelf at 2500Hz, Peaking bell at 1000Hz (Q = 0.8)
                float effLowDb = LowKill ? -60.0f : Math.Clamp(LowGainDb, -26.0f, 6.0f);
                float effMidDb = MidKill ? -60.0f : Math.Clamp(MidGainDb, -26.0f, 6.0f);
                float effHighDb = HighKill ? -60.0f : Math.Clamp(HighGainDb, -26.0f, 6.0f);

                _isoLowL.SetLowShelf(SampleRate, 300.0f, effLowDb, 0.707f);
                _isoLowR.CopyCoefficientsFrom(_isoLowL);

                _isoMidL.SetPeakingEq(SampleRate, 1000.0f, effMidDb, 0.8f);
                _isoMidR.CopyCoefficientsFrom(_isoMidL);

                _isoHighL.SetHighShelf(SampleRate, 2500.0f, effHighDb, 0.707f);
                _isoHighR.CopyCoefficientsFrom(_isoHighL);

                // DJ Color Sweep Filter (-100 to +100)
                if (ColorFilterKnob < -1.0f) {
                    // Sweeps LPF cutoff from 20kHz down to 160Hz with resonant Q of 1.4
                    float norm = Math.Clamp(-ColorFilterKnob / 100.0f, 0.0f, 1.0f);
                    float cutoff = MathF.Pow(10.0f, MathF.Log10(20000.0f) - norm * (MathF.Log10(20000.0f) - MathF.Log10(160.0f)));
                    _colorLpfL.SetLowPass(SampleRate, cutoff, 1.3f);
                    _colorLpfR.CopyCoefficientsFrom(_colorLpfL);
                } else if (ColorFilterKnob > 1.0f) {
                    // Sweeps HPF cutoff from 20Hz up to 3500Hz with resonant Q of 1.4
                    float norm = Math.Clamp(ColorFilterKnob / 100.0f, 0.0f, 1.0f);
                    float cutoff = MathF.Pow(10.0f, MathF.Log10(20.0f) + norm * (MathF.Log10(3500.0f) - MathF.Log10(20.0f)));
                    _colorHpfL.SetHighPass(SampleRate, cutoff, 1.3f);
                    _colorHpfR.CopyCoefficientsFrom(_colorHpfL);
                }

                // 80Hz Mic Low-Cut (High-Pass)
                _micHpfL.SetHighPass(SampleRate, 80.0f, 0.707f);
                _micHpfR.CopyCoefficientsFrom(_micHpfL);

                // 10-Band Graphic EQ
                for (int i = 0; i < 10; i++) {
                    float freq = IsoFrequencies[i];
                    float gain = Math.Clamp(Bands10Db[i], -12.0f, 12.0f);
                    if (i == 0) {
                        _eq10L[i].SetLowShelf(SampleRate, freq, gain, 0.707f);
                    } else if (i == 9) {
                        _eq10L[i].SetHighShelf(SampleRate, freq, gain, 0.707f);
                    } else {
                        _eq10L[i].SetPeakingEq(SampleRate, freq, gain, 1.414f); // 1-octave bandwidth
                    }
                    _eq10R[i].CopyCoefficientsFrom(_eq10L[i]);
                }
            }
        }

        /// <summary>
        /// Resets all filter state buffers (clears delay registers).
        /// </summary>
        public void ResetFilterState() {
            lock (_dspLock) {
                _isoLowL.Reset(); _isoLowR.Reset();
                _isoMidL.Reset(); _isoMidR.Reset();
                _isoHighL.Reset(); _isoHighR.Reset();
                _colorLpfL.Reset(); _colorLpfR.Reset();
                _colorHpfL.Reset(); _colorHpfR.Reset();
                _micHpfL.Reset(); _micHpfR.Reset();
                for (int i = 0; i < 10; i++) {
                    _eq10L[i].Reset(); _eq10R[i].Reset();
                }
            }
        }

        /// <summary>
        /// Process stereo 16-bit 44.1kHz PCM music buffer in-place.
        /// Applies Music Trim, Pan, Stereo Width (Mid-Side), and active EQ / Color Filters.
        /// </summary>
        public void ProcessMusicBuffer(byte[] buffer, int length) {
            if (buffer == null || length <= 0) return;

            float trimLinear = MathF.Pow(10.0f, MusicTrimDb / 20.0f);
            if (MusicMuted) trimLinear = 0.0f;

            // Constant power stereo panning law
            float panAngle = (Math.Clamp(MusicPan, -1.0f, 1.0f) + 1.0f) * (MathF.PI / 4.0f);
            float panL = MathF.Cos(panAngle) * 1.4142f * trimLinear;
            float panR = MathF.Sin(panAngle) * 1.4142f * trimLinear;

            float width = Math.Clamp(MusicStereoWidth, 0.0f, 2.0f);
            bool applyEq = !EqBypass;
            bool is10 = Is10BandMode;
            bool applyLpf = ColorFilterKnob < -1.0f;
            bool applyHpf = ColorFilterKnob > 1.0f;

            float maxL = 0.0f;
            float maxR = 0.0f;

            lock (_dspLock) {
                int frameBytes = length - (length % 4);
                for (int i = 0; i < frameBytes; i += 4) {
                    short rawL = BitConverter.ToInt16(buffer, i);
                    short rawR = BitConverter.ToInt16(buffer, i + 2);

                    float sampleL = (rawL / 32768.0f) * panL;
                    float sampleR = (rawR / 32768.0f) * panR;

                    // Stereo Width Expander (Mid-Side Matrix)
                    if (Math.Abs(width - 1.0f) > 0.001f) {
                        float mid = 0.5f * (sampleL + sampleR);
                        float side = 0.5f * (sampleL - sampleR);
                        sampleL = mid + side * width;
                        sampleR = mid - side * width;
                    }

                    // Equalizer Processing
                    if (applyEq) {
                        if (is10) {
                            for (int b = 0; b < 10; b++) {
                                sampleL = _eq10L[b].Process(sampleL);
                                sampleR = _eq10R[b].Process(sampleR);
                            }
                        } else {
                            sampleL = _isoLowL.Process(sampleL);
                            sampleR = _isoLowR.Process(sampleR);

                            sampleL = _isoMidL.Process(sampleL);
                            sampleR = _isoMidR.Process(sampleR);

                            sampleL = _isoHighL.Process(sampleL);
                            sampleR = _isoHighR.Process(sampleR);
                        }

                        // DJ Color Sweep Filter
                        if (applyLpf) {
                            sampleL = _colorLpfL.Process(sampleL);
                            sampleR = _colorLpfR.Process(sampleR);
                        } else if (applyHpf) {
                            sampleL = _colorHpfL.Process(sampleL);
                            sampleR = _colorHpfR.Process(sampleR);
                        }
                    }

                    float absL = MathF.Abs(sampleL);
                    float absR = MathF.Abs(sampleR);
                    if (absL > maxL) maxL = absL;
                    if (absR > maxR) maxR = absR;

                    short outL = (short)Math.Clamp(sampleL * 32767.0f, -32768.0f, 32767.0f);
                    short outR = (short)Math.Clamp(sampleR * 32767.0f, -32768.0f, 32767.0f);

                    buffer[i] = (byte)(outL & 0xFF);
                    buffer[i + 1] = (byte)((outL >> 8) & 0xFF);
                    buffer[i + 2] = (byte)(outR & 0xFF);
                    buffer[i + 3] = (byte)((outR >> 8) & 0xFF);
                }
            }

            MusicPeakL = MathF.Max(maxL, MusicPeakL * 0.88f);
            MusicPeakR = MathF.Max(maxR, MusicPeakR * 0.88f);
        }

        /// <summary>
        /// Process stereo 16-bit 44.1kHz PCM microphone buffer in-place.
        /// Applies Mic Gain, Pan, and 80Hz Low-Cut rumble filter.
        /// </summary>
        public void ProcessMicBuffer(byte[] buffer, int length) {
            if (buffer == null || length <= 0) return;

            float gainLinear = MathF.Pow(10.0f, MicGainDb / 20.0f);
            if (MicMuted) gainLinear = 0.0f;

            float panAngle = (Math.Clamp(MicPan, -1.0f, 1.0f) + 1.0f) * (MathF.PI / 4.0f);
            float panL = MathF.Cos(panAngle) * 1.4142f * gainLinear;
            float panR = MathF.Sin(panAngle) * 1.4142f * gainLinear;

            bool applyLowCut = MicLowCut;
            float maxL = 0.0f;
            float maxR = 0.0f;

            lock (_dspLock) {
                int frameBytes = length - (length % 4);
                for (int i = 0; i < frameBytes; i += 4) {
                    short rawL = BitConverter.ToInt16(buffer, i);
                    short rawR = BitConverter.ToInt16(buffer, i + 2);

                    float sampleL = (rawL / 32768.0f) * panL;
                    float sampleR = (rawR / 32768.0f) * panR;

                    if (applyLowCut) {
                        sampleL = _micHpfL.Process(sampleL);
                        sampleR = _micHpfR.Process(sampleR);
                    }

                    float absL = MathF.Abs(sampleL);
                    float absR = MathF.Abs(sampleR);
                    if (absL > maxL) maxL = absL;
                    if (absR > maxR) maxR = absR;

                    short outL = (short)Math.Clamp(sampleL * 32767.0f, -32768.0f, 32767.0f);
                    short outR = (short)Math.Clamp(sampleR * 32767.0f, -32768.0f, 32767.0f);

                    buffer[i] = (byte)(outL & 0xFF);
                    buffer[i + 1] = (byte)((outL >> 8) & 0xFF);
                    buffer[i + 2] = (byte)(outR & 0xFF);
                    buffer[i + 3] = (byte)((outR >> 8) & 0xFF);
                }
            }

            MicPeakL = MathF.Max(maxL, MicPeakL * 0.88f);
            MicPeakR = MathF.Max(maxR, MicPeakR * 0.88f);
        }

        /// <summary>
        /// Applies Master Gain, Balance, and Soft Limiter to an output sample pair.
        /// </summary>
        public (short Left, short Right) ProcessMasterSample(double sumL, double sumR) {
            float masterLinear = MathF.Pow(10.0f, MasterGainDb / 20.0f);

            float balAngle = (Math.Clamp(MasterBalance, -1.0f, 1.0f) + 1.0f) * (MathF.PI / 4.0f);
            float balL = MathF.Cos(balAngle) * 1.4142f * masterLinear;
            float balR = MathF.Sin(balAngle) * 1.4142f * masterLinear;

            float normL = (float)(sumL / 32768.0) * balL;
            float normR = (float)(sumR / 32768.0) * balR;

            if (SoftLimiterEnabled) {
                // Analog hyperbolic tangent soft saturation limiter
                // Transparently preserves dynamics below 0.85, gently saturates peaks above 0.85
                normL = SoftLimit(normL);
                normR = SoftLimit(normR);
            }

            float absL = MathF.Abs(normL);
            float absR = MathF.Abs(normR);
            if (absL > MasterPeakL) MasterPeakL = absL;
            if (absR > MasterPeakR) MasterPeakR = absR;

            short outL = (short)Math.Clamp(normL * 32767.0f, -32768.0f, 32767.0f);
            short outR = (short)Math.Clamp(normR * 32767.0f, -32768.0f, 32767.0f);

            return (outL, outR);
        }

        public void DecayMeters() {
            MasterPeakL *= 0.88f;
            MasterPeakR *= 0.88f;
            MusicPeakL *= 0.88f;
            MusicPeakR *= 0.88f;
            MicPeakL *= 0.88f;
            MicPeakR *= 0.88f;
        }

        private static float SoftLimit(float x) {
            const float threshold = 0.85f;
            float absX = MathF.Abs(x);
            if (absX <= threshold) return x;

            float sign = x < 0.0f ? -1.0f : 1.0f;
            float excess = absX - threshold;
            float saturated = threshold + (1.0f - threshold) * MathF.Tanh(excess / (1.0f - threshold));
            return sign * saturated;
        }

        /// <summary>
        /// Calculates the theoretical combined frequency response (in dB) across the spectrum for SVG graph rendering.
        /// </summary>
        public float[] CalculateResponseCurve(int pointCount = 60) {
            float[] curve = new float[pointCount];
            if (EqBypass) return curve; // All 0dB

            float minLog = MathF.Log10(20.0f);
            float maxLog = MathF.Log10(20000.0f);

            for (int i = 0; i < pointCount; i++) {
                float freq = MathF.Pow(10.0f, minLog + ((float)i / (pointCount - 1)) * (maxLog - minLog));
                float totalDb = 0.0f;

                if (Is10BandMode) {
                    for (int b = 0; b < 10; b++) {
                        totalDb += _eq10L[b].EvaluateMagnitudeDb(freq, SampleRate);
                    }
                } else {
                    totalDb += _isoLowL.EvaluateMagnitudeDb(freq, SampleRate);
                    totalDb += _isoMidL.EvaluateMagnitudeDb(freq, SampleRate);
                    totalDb += _isoHighL.EvaluateMagnitudeDb(freq, SampleRate);
                }

                if (ColorFilterKnob < -1.0f) {
                    totalDb += _colorLpfL.EvaluateMagnitudeDb(freq, SampleRate);
                } else if (ColorFilterKnob > 1.0f) {
                    totalDb += _colorHpfL.EvaluateMagnitudeDb(freq, SampleRate);
                }

                curve[i] = Math.Clamp(totalDb, -30.0f, 15.0f);
            }

            return curve;
        }

        /// <summary>
        /// Direct Form II Transposed Bi-quad IIR Filter implementation.
        /// </summary>
        private class BiquadFilter {
            private float _b0 = 1.0f, _b1 = 0.0f, _b2 = 0.0f;
            private float _a1 = 0.0f, _a2 = 0.0f;
            private float _z1 = 0.0f, _z2 = 0.0f;

            public void Reset() {
                _z1 = 0.0f;
                _z2 = 0.0f;
            }

            public void CopyCoefficientsFrom(BiquadFilter other) {
                _b0 = other._b0;
                _b1 = other._b1;
                _b2 = other._b2;
                _a1 = other._a1;
                _a2 = other._a2;
            }

            public float Process(float input) {
                float output = _b0 * input + _z1;
                _z1 = _b1 * input - _a1 * output + _z2;
                _z2 = _b2 * input - _a2 * output;
                return output;
            }

            public void SetPeakingEq(float sampleRate, float frequency, float gainDb, float q) {
                float a = MathF.Pow(10.0f, gainDb / 40.0f);
                float omega = 2.0f * MathF.PI * frequency / sampleRate;
                float sn = MathF.Sin(omega);
                float cs = MathF.Cos(omega);
                float alpha = sn / (2.0f * q);

                float b0 = 1.0f + alpha * a;
                float b1 = -2.0f * cs;
                float b2 = 1.0f - alpha * a;
                float a0 = 1.0f + alpha / a;
                float a1 = -2.0f * cs;
                float a2 = 1.0f - alpha / a;

                SetNormalized(b0, b1, b2, a0, a1, a2);
            }

            public void SetLowShelf(float sampleRate, float frequency, float gainDb, float q) {
                float a = MathF.Pow(10.0f, gainDb / 40.0f);
                float omega = 2.0f * MathF.PI * frequency / sampleRate;
                float sn = MathF.Sin(omega);
                float cs = MathF.Cos(omega);
                float alpha = sn / (2.0f * q);
                float twoSqrtAAlpha = 2.0f * MathF.Sqrt(a) * alpha;

                float b0 = a * ((a + 1.0f) - (a - 1.0f) * cs + twoSqrtAAlpha);
                float b1 = 2.0f * a * ((a - 1.0f) - (a + 1.0f) * cs);
                float b2 = a * ((a + 1.0f) - (a - 1.0f) * cs - twoSqrtAAlpha);
                float a0 = (a + 1.0f) + (a - 1.0f) * cs + twoSqrtAAlpha;
                float a1 = -2.0f * ((a - 1.0f) + (a + 1.0f) * cs);
                float a2 = (a + 1.0f) + (a - 1.0f) * cs - twoSqrtAAlpha;

                SetNormalized(b0, b1, b2, a0, a1, a2);
            }

            public void SetHighShelf(float sampleRate, float frequency, float gainDb, float q) {
                float a = MathF.Pow(10.0f, gainDb / 40.0f);
                float omega = 2.0f * MathF.PI * frequency / sampleRate;
                float sn = MathF.Sin(omega);
                float cs = MathF.Cos(omega);
                float alpha = sn / (2.0f * q);
                float twoSqrtAAlpha = 2.0f * MathF.Sqrt(a) * alpha;

                float b0 = a * ((a + 1.0f) + (a - 1.0f) * cs + twoSqrtAAlpha);
                float b1 = -2.0f * a * ((a - 1.0f) + (a + 1.0f) * cs);
                float b2 = a * ((a + 1.0f) + (a - 1.0f) * cs - twoSqrtAAlpha);
                float a0 = (a + 1.0f) - (a - 1.0f) * cs + twoSqrtAAlpha;
                float a1 = 2.0f * ((a - 1.0f) - (a + 1.0f) * cs);
                float a2 = (a + 1.0f) - (a - 1.0f) * cs - twoSqrtAAlpha;

                SetNormalized(b0, b1, b2, a0, a1, a2);
            }

            public void SetLowPass(float sampleRate, float frequency, float q) {
                float omega = 2.0f * MathF.PI * frequency / sampleRate;
                float sn = MathF.Sin(omega);
                float cs = MathF.Cos(omega);
                float alpha = sn / (2.0f * q);

                float b0 = (1.0f - cs) / 2.0f;
                float b1 = 1.0f - cs;
                float b2 = (1.0f - cs) / 2.0f;
                float a0 = 1.0f + alpha;
                float a1 = -2.0f * cs;
                float a2 = 1.0f - alpha;

                SetNormalized(b0, b1, b2, a0, a1, a2);
            }

            public void SetHighPass(float sampleRate, float frequency, float q) {
                float omega = 2.0f * MathF.PI * frequency / sampleRate;
                float sn = MathF.Sin(omega);
                float cs = MathF.Cos(omega);
                float alpha = sn / (2.0f * q);

                float b0 = (1.0f + cs) / 2.0f;
                float b1 = -(1.0f + cs);
                float b2 = (1.0f + cs) / 2.0f;
                float a0 = 1.0f + alpha;
                float a1 = -2.0f * cs;
                float a2 = 1.0f - alpha;

                SetNormalized(b0, b1, b2, a0, a1, a2);
            }

            private void SetNormalized(float b0, float b1, float b2, float a0, float a1, float a2) {
                float invA0 = 1.0f / a0;
                _b0 = b0 * invA0;
                _b1 = b1 * invA0;
                _b2 = b2 * invA0;
                _a1 = a1 * invA0;
                _a2 = a2 * invA0;
            }

            public float EvaluateMagnitudeDb(float frequency, float sampleRate) {
                float phi = MathF.Pow(MathF.Sin(2.0f * MathF.PI * frequency / (2.0f * sampleRate)), 2.0f);
                float num = MathF.Pow(_b0 + _b1 + _b2, 2.0f) - 4.0f * (_b0 * _b1 + 4.0f * _b0 * _b2 + _b1 * _b2) * phi + 16.0f * _b0 * _b2 * phi * phi;
                float den = MathF.Pow(1.0f + _a1 + _a2, 2.0f) - 4.0f * (_a1 + 4.0f * _a2 + _a1 * _a2) * phi + 16.0f * _a2 * phi * phi;
                if (den <= 0.0f || num <= 0.0f) return 0.0f;
                return 10.0f * MathF.Log10(num / den);
            }
        }
    }
}
