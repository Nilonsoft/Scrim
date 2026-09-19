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

        public bool IsMixing => _mixingCts != null && !_mixingCts.IsCancellationRequested;

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
                LeftLevel = 0f;
                RightLevel = 0f;
                _activeSfxQueue.Clear();
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
                    // Standard keepalive chunk at 44.1kHz 16-bit stereo (882 samples * 4 bytes = 3528 bytes = 20ms)
                    const int KeepaliveBytes = 3528;
                    byte[] silentFrame = new byte[KeepaliveBytes];
                    var micQueue = new System.Collections.Generic.Queue<byte>();
                    long lastAppPacketTicks = 0;
                    bool wasSilent = false;

                    while (!token.IsCancellationRequested) {
                        byte[]? appBuffer = null;

                        try {
                            // Try non-blocking read from app audio first
                            if (appAudio.TryRead(out var directBuf)) {
                                appBuffer = directBuf;
                                lastAppPacketTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                                wasSilent = false;
                            } else {
                                // If in active playback mode (or initial startup), wait up to 350ms to accommodate
                                // NAudio's 50-100ms buffer delivery intervals without injecting silence gaps.
                                // If idle/silent, pace keepalive silence frames at 20ms (10ms if sfx/mic queued)
                                // so soundboard effects and VU decay ballistics run smoothly in real time.
                                bool isRecentPlayback = !wasSilent && (lastAppPacketTicks == 0 || System.Diagnostics.Stopwatch.GetElapsedTime(lastAppPacketTicks).TotalMilliseconds < 350);
                                int waitTimeoutMs;
                                if (isRecentPlayback) {
                                    waitTimeoutMs = 350;
                                } else {
                                    waitTimeoutMs = (_sfxQueue.Count > 0 || _activeSfxQueue.Count >= 4 || micQueue.Count >= KeepaliveBytes) ? 10 : 20;
                                }

                                using var timeoutCts = new CancellationTokenSource(waitTimeoutMs);
                                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token);
                                try {
                                    appBuffer = await appAudio.ReadAsync(linkedCts.Token);
                                    lastAppPacketTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                                    wasSilent = false;
                                } catch (OperationCanceledException) {
                                    if (token.IsCancellationRequested) break;
                                    appBuffer = silentFrame;
                                    wasSilent = true;
                                }
                            }

                            if (appBuffer != null && appBuffer.Length > 0) {
                                // Smooth micro-ramp (1ms = 44 samples) when transitioning from silence to audio
                                // to eliminate any DC offset step discontinuity / pop artifact
                                if (wasSilent && appBuffer != silentFrame) {
                                    wasSilent = false;
                                    int rampSamples = Math.Min(44, appBuffer.Length / 4);
                                    for (int s = 0; s < rampSamples; s++) {
                                        float ramp = (float)s / rampSamples;
                                        int idx = s * 4;
                                        short sampleL = (short)(BitConverter.ToInt16(appBuffer, idx) * ramp);
                                        short sampleR = (short)(BitConverter.ToInt16(appBuffer, idx + 2) * ramp);
                                        byte[] bL = BitConverter.GetBytes(sampleL);
                                        byte[] bR = BitConverter.GetBytes(sampleR);
                                        appBuffer[idx] = bL[0];
                                        appBuffer[idx + 1] = bL[1];
                                        appBuffer[idx + 2] = bR[0];
                                        appBuffer[idx + 3] = bR[1];
                                    }
                                } else if (appBuffer != silentFrame) {
                                    wasSilent = false;
                                }

                                // Buffer incoming microphone packets continuously without dropping residual bytes
                                while (micAudio.TryRead(out var mBuf)) {
                                    for (int b = 0; b < mBuf.Length; b++) {
                                        micQueue.Enqueue(mBuf[b]);
                                    }
                                }

                                // Keep microphone queue bounded to max 120ms (21168 bytes at 44.1kHz 16-bit stereo)
                                // to prevent latency buildup or clock drift between capture and playback devices
                                const int MaxMicQueueBytes = 21168;
                                if (micQueue.Count > MaxMicQueueBytes) {
                                    int excess = micQueue.Count - MaxMicQueueBytes;
                                    excess -= excess % 4; // strictly maintain 4-byte frame alignment
                                    for (int b = 0; b < excess; b++) {
                                        micQueue.Dequeue();
                                    }
                                }

                                // Extract matching amount of microphone audio for this mixing frame
                                byte[] micBuffer = new byte[appBuffer.Length];
                                int micBytesToCopy = Math.Min(micQueue.Count, appBuffer.Length);
                                micBytesToCopy -= micBytesToCopy % 4; // ensure strictly 4-byte sample-aligned reads

                                for (int b = 0; b < micBytesToCopy; b++) {
                                    micBuffer[b] = micQueue.Dequeue();
                                }

                                ProcessMix(appBuffer, micBuffer);
                            }
                        } catch (OperationCanceledException) when (token.IsCancellationRequested) {
                            break;
                        } catch (Exception ex) {
                            Console.WriteLine($"[AudioDuckingMixer] Mix error: {ex.Message}");
                            try {
                                await Task.Delay(20, token);
                            } catch { }
                        }
                    }

                    LeftLevel = 0f;
                    RightLevel = 0f;
                }, token);
            }
        }

        private readonly System.Collections.Concurrent.ConcurrentQueue<byte[]> _sfxQueue = new();
        private readonly System.Collections.Generic.Queue<byte> _activeSfxQueue = new();

        public void PlaySoundEffect(byte[] pcm16StereoBytes) {
            if (pcm16StereoBytes != null && pcm16StereoBytes.Length > 0) {
                _sfxQueue.Enqueue(pcm16StereoBytes);
            }
        }

        private void ProcessMix(byte[] appBuffer, byte[] micBuffer) {
            byte[] outBuffer = new byte[appBuffer.Length];
            bool micActive = IsMicLive;
            double targetGain = micActive ? _duckingGain : 1.0;

            while (_sfxQueue.TryDequeue(out var sfxBytes)) {
                for (int b = 0; b < sfxBytes.Length; b++) {
                    _activeSfxQueue.Enqueue(sfxBytes[b]);
                }
            }

            float maxL = 0f;
            float maxR = 0f;

            int frameBytes = appBuffer.Length - (appBuffer.Length % 4);

            for (int i = 0; i < frameBytes; i += 4) {
                short appSampleL = BitConverter.ToInt16(appBuffer, i);
                short micSampleL = (micActive && i + 1 < micBuffer.Length) ? BitConverter.ToInt16(micBuffer, i) : (short)0;

                short appSampleR = (i + 2 < appBuffer.Length) ? BitConverter.ToInt16(appBuffer, i + 2) : appSampleL;
                short micSampleR = (micActive && i + 3 < micBuffer.Length) ? BitConverter.ToInt16(micBuffer, i + 2) : (short)0;

                short sfxL = 0;
                short sfxR = 0;
                if (_activeSfxQueue.Count >= 4) {
                    byte b0 = _activeSfxQueue.Dequeue();
                    byte b1 = _activeSfxQueue.Dequeue();
                    byte b2 = _activeSfxQueue.Dequeue();
                    byte b3 = _activeSfxQueue.Dequeue();
                    sfxL = (short)(b0 | (b1 << 8));
                    sfxR = (short)(b2 | (b3 << 8));
                }

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
                double sumL = mixedAppL + micSampleL + sfxL;
                double sumR = mixedAppR + micSampleR + sfxR;

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
            if (LeftLevel < 0.001f) LeftLevel = 0f;
            if (RightLevel < 0.001f) RightLevel = 0f;

            _mixedOutput.Writer.TryWrite(outBuffer);
        }
    }
}
