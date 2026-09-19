using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Scrim.Audio {
    public enum MicControlMode {
        PushToTalk,
        PushToMute
    }

    public class AudioDuckingMixer {
        private readonly Channel<byte[]> _mixedOutput;
        private readonly double _duckingGain = Math.Pow(10, -14.0 / 20.0); // -14 dB
        private readonly object _startLock = new();
        private CancellationTokenSource? _mixingCts;
        
        // Attack 150ms, Release 350ms per-sample coefficients at 44.1kHz
        private double _currentGain = 1.0;
        private readonly double _attackCoef = Math.Exp(-1.0 / (0.150 * 44100));
        private readonly double _releaseCoef = Math.Exp(-1.0 / (0.350 * 44100));

        public float LeftLevel { get; private set; } = 0f;
        public float RightLevel { get; private set; } = 0f;

        public bool PushToTalkActive { get; set; }
        public bool LatchActive { get; set; }
        public bool PushToMuteActive { get; set; }
        public bool IsToggleMuted { get; set; }
        public MicControlMode ControlMode { get; set; } = MicControlMode.PushToTalk;
        public float AppVolume { get; set; } = 1.0f;

        public bool IsMicLive {
            get {
                if (ControlMode == MicControlMode.PushToMute) {
                    return !IsToggleMuted && !PushToMuteActive;
                } else {
                    return LatchActive || PushToTalkActive;
                }
            }
        }

        public ChannelReader<byte[]> MixedStream => _mixedOutput.Reader;

        public AudioDuckingMixer() {
            // Unbounded channel ensures zero dropped frames and no bitstream discontinuities
            _mixedOutput = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions {
                SingleReader = false,
                SingleWriter = false
            });
        }

        public void StopMixing() {
            lock (_startLock) {
                if (_mixingCts != null) {
                    _mixingCts.Cancel();
                    _mixingCts.Dispose();
                    _mixingCts = null;
                }
            }
        }

        public void StartMixing(ChannelReader<byte[]> appAudio, ChannelReader<byte[]> micAudio, CancellationToken externalToken) {
            lock (_startLock) {
                // Cancel and cleanly tear down any previous mixing task
                if (_mixingCts != null) {
                    _mixingCts.Cancel();
                    _mixingCts.Dispose();
                }
                _mixingCts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
                var token = _mixingCts.Token;

                Task.Run(async () => {
                    // Standard 20ms keepalive chunk at 44.1kHz 16-bit stereo (882 samples * 4 bytes = 3528 bytes)
                    const int KeepaliveBytes = 3528;
                    byte[] silentFrame = new byte[KeepaliveBytes];

                    while (!token.IsCancellationRequested) {
                        byte[]? appBuffer = null;

                        // Try non-blocking read from app audio first
                        if (appAudio.TryRead(out var directBuf)) {
                            appBuffer = directBuf;
                        } else {
                            // Wait up to 20ms for incoming app audio so the stream never stalls during pause or silence
                            using var timeoutCts = new CancellationTokenSource(20);
                            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token);
                            try {
                                appBuffer = await appAudio.ReadAsync(linkedCts.Token);
                            } catch (OperationCanceledException) {
                                if (token.IsCancellationRequested) break;
                                // 20ms timeout elapsed: generate keepalive silence
                                appBuffer = silentFrame;
                            }
                        }

                        if (appBuffer != null && appBuffer.Length > 0) {
                            byte[] micBuffer = new byte[appBuffer.Length];
                            if (micAudio.TryRead(out var mBuf)) {
                                Array.Copy(mBuf, micBuffer, Math.Min(mBuf.Length, micBuffer.Length));
                            }

                            ProcessMix(appBuffer, micBuffer);
                        }
                    }
                }, token);
            }
        }

        private void ProcessMix(byte[] appBuffer, byte[] micBuffer) {
            byte[] outBuffer = new byte[appBuffer.Length];
            bool micActive = IsMicLive;
            double targetGain = micActive ? _duckingGain : 1.0;

            float maxL = 0f;
            float maxR = 0f;

            for (int i = 0; i < appBuffer.Length; i += 4) {
                short appSampleL = BitConverter.ToInt16(appBuffer, i);
                short micSampleL = (micActive && i + 1 < micBuffer.Length) ? BitConverter.ToInt16(micBuffer, i) : (short)0;

                short appSampleR = (i + 2 < appBuffer.Length) ? BitConverter.ToInt16(appBuffer, i + 2) : appSampleL;
                short micSampleR = (micActive && i + 3 < micBuffer.Length) ? BitConverter.ToInt16(micBuffer, i + 2) : (short)0;

                // Smooth exponential gain transitions prevent clicking when ducking engages/disengages
                if (_currentGain > targetGain) {
                    _currentGain = _attackCoef * _currentGain + (1 - _attackCoef) * targetGain;
                } else {
                    _currentGain = _releaseCoef * _currentGain + (1 - _releaseCoef) * targetGain;
                }

                // Apply smooth ducking and independent stream volume to app audio
                double mixedAppL = appSampleL * _currentGain * AppVolume;
                double mixedAppR = appSampleR * _currentGain * AppVolume;
                
                // Sum 2-bus
                double sumL = mixedAppL + micSampleL;
                double sumR = mixedAppR + micSampleR;

                // Clamping limiter
                if (sumL > short.MaxValue) sumL = short.MaxValue;
                if (sumL < short.MinValue) sumL = short.MinValue;
                if (sumR > short.MaxValue) sumR = short.MaxValue;
                if (sumR < short.MinValue) sumR = short.MinValue;

                short finalL = (short)sumL;
                short finalR = (short)sumR;

                byte[] bytesL = BitConverter.GetBytes(finalL);
                byte[] bytesR = BitConverter.GetBytes(finalR);

                outBuffer[i] = bytesL[0];
                outBuffer[i + 1] = bytesL[1];
                if (i + 2 < appBuffer.Length) {
                    outBuffer[i + 2] = bytesR[0];
                    outBuffer[i + 3] = bytesR[1];
                }

                float absL = Math.Abs(finalL) / 32768.0f;
                float absR = Math.Abs(finalR) / 32768.0f;
                if (absL > maxL) maxL = absL;
                if (absR > maxR) maxR = absR;
            }

            // Analog VU decay ballistics
            LeftLevel = Math.Max(maxL, LeftLevel * 0.85f);
            RightLevel = Math.Max(maxR, RightLevel * 0.85f);

            _mixedOutput.Writer.TryWrite(outBuffer);
        }
    }
}
