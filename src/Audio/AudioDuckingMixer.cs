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

        // Native Pro DJ Channel Mixer & Multi-Band EQ Engine
        public DjEqualizerProcessor DjEq { get; } = new();

        public float MusicLeftLevel => DjEq.MusicPeakL;
        public float MusicRightLevel => DjEq.MusicPeakR;
        public float MicLeftLevel => DjEq.MicPeakL;
        public float MicRightLevel => DjEq.MicPeakR;

        public bool PushToTalkActive { get; set; }
        public bool LatchActive { get; set; }
        public bool PushToMuteActive { get; set; }
        public bool IsToggleMuted { get; set; }
        public MicControlMode ControlMode { get; set; } = MicControlMode.PushToTalk;
        public float AppVolume { get; set; } = 1.0f;
        public float RelayVolume { get; set; } = 1.0f;
        public bool TalkbackActive { get; set; } = false;
        public ChannelReader<byte[]>? LocalMusicAudioStream { get; set; }
        public ChannelReader<byte[]>? RelayAudioStream { get; set; }

        public bool IsMicLive {
            get {
                if (TalkbackActive) return false;
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
            // Bounded channel with DropOldest prevents unbounded memory bloat during offline audio monitoring
            _mixedOutput = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(100) {
                SingleReader = false,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.DropOldest
            });
        }

        public void FlushOutput() {
            while (_mixedOutput.Reader.TryRead(out _)) { }
        }

        private CancellationTokenSource? _crossfadeCts;

        public void StartB2bCrossfade(float targetRelayVol, float targetAppVol, int durationMs = 8000) {
            _crossfadeCts?.Cancel();
            _crossfadeCts?.Dispose();
            _crossfadeCts = new CancellationTokenSource();
            var token = _crossfadeCts.Token;

            float startRelay = RelayVolume;
            float startApp = AppVolume;

            Task.Run(async () => {
                int steps = Math.Clamp(durationMs / 20, 5, 40);
                int stepDelay = Math.Max(5, durationMs / steps);
                for (int i = 1; i <= steps && !token.IsCancellationRequested; i++) {
                    float t = (float)i / steps;
                    float smoothT = (1.0f - MathF.Cos(t * MathF.PI)) * 0.5f;
                    RelayVolume = startRelay + (targetRelayVol - startRelay) * smoothT;
                    AppVolume = startApp + (targetAppVol - startApp) * smoothT;
                    await Task.Delay(stepDelay, token);
                }
                if (!token.IsCancellationRequested) {
                    RelayVolume = targetRelayVol;
                    AppVolume = targetAppVol;
                }
            }, token);
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
                FlushOutput();
            }
        }

        public void StartMixing(ChannelReader<byte[]> appAudio, ChannelReader<byte[]> micAudio, CancellationToken externalToken) {
            lock (_startLock) {
                // Cancel and cleanly tear down any previous mixing task
                if (_mixingCts != null) {
                    _mixingCts.Cancel();
                    _mixingCts.Dispose();
                }
                FlushOutput();
                _mixingCts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
                var token = _mixingCts.Token;

                Task.Run(async () => {
                    // Standard keepalive chunk at 44.1kHz 16-bit stereo (882 samples * 4 bytes = 3528 bytes = 20ms)
                    const int KeepaliveBytes = 3528;
                    byte[] silentFrame = new byte[KeepaliveBytes];
                    var micQueue = new System.Collections.Generic.Queue<byte>();
                    var localMusicQueue = new System.Collections.Generic.Queue<byte>();
                    var relayQueue = new System.Collections.Generic.Queue<byte>();
                    long lastAppPacketTicks = 0;
                    bool wasSilent = false;

                    while (!token.IsCancellationRequested) {
                        byte[]? appBuffer = null;

                        try {
                            // Buffer incoming local music packets if stream is available
                            if (LocalMusicAudioStream != null) {
                                while (LocalMusicAudioStream.TryRead(out var lmBuf)) {
                                    for (int b = 0; b < lmBuf.Length; b++) {
                                        localMusicQueue.Enqueue(lmBuf[b]);
                                    }
                                }
                            }

                            // Buffer incoming stream relay packets if stream is available
                            if (RelayAudioStream != null) {
                                while (RelayAudioStream.TryRead(out var rBuf)) {
                                    for (int b = 0; b < rBuf.Length; b++) {
                                        relayQueue.Enqueue(rBuf[b]);
                                    }
                                }
                            }

                            // Keep local music queue bounded to max 150ms (26460 bytes)
                            const int MaxLocalQueueBytes = 26460;
                            if (localMusicQueue.Count > MaxLocalQueueBytes) {
                                int excess = localMusicQueue.Count - MaxLocalQueueBytes;
                                excess -= excess % 4;
                                for (int b = 0; b < excess; b++) {
                                    localMusicQueue.Dequeue();
                                }
                            }

                            // Keep relay queue bounded to max 150ms (26460 bytes)
                            if (relayQueue.Count > MaxLocalQueueBytes) {
                                int excess = relayQueue.Count - MaxLocalQueueBytes;
                                excess -= excess % 4;
                                for (int b = 0; b < excess; b++) {
                                    relayQueue.Dequeue();
                                }
                            }

                            // Try non-blocking read from app audio first
                            if (appAudio.TryRead(out var directBuf)) {
                                appBuffer = directBuf;
                                lastAppPacketTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                                wasSilent = false;
                            } else {
                                // If in active playback mode (or initial startup), wait up to 350ms to accommodate
                                // NAudio's 50-100ms buffer delivery intervals without injecting silence gaps.
                                // If idle/silent, pace keepalive silence frames at 20ms (10ms if sfx/mic/local/relay queued)
                                // so soundboard effects and VU decay ballistics run smoothly in real time.
                                bool isRecentPlayback = !wasSilent && (lastAppPacketTicks == 0 || System.Diagnostics.Stopwatch.GetElapsedTime(lastAppPacketTicks).TotalMilliseconds < 350);
                                int waitTimeoutMs;
                                if (isRecentPlayback) {
                                    waitTimeoutMs = 350;
                                } else {
                                    waitTimeoutMs = (_sfxQueue.Count > 0 || _activeSfxQueue.Count >= 4 || micQueue.Count >= KeepaliveBytes || localMusicQueue.Count >= KeepaliveBytes || relayQueue.Count >= KeepaliveBytes) ? 10 : 20;
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

                            // Again drain any new local music and relay packets that arrived during wait
                            if (LocalMusicAudioStream != null) {
                                while (LocalMusicAudioStream.TryRead(out var lmBuf)) {
                                    for (int b = 0; b < lmBuf.Length; b++) {
                                        localMusicQueue.Enqueue(lmBuf[b]);
                                    }
                                }
                            }
                            if (RelayAudioStream != null) {
                                while (RelayAudioStream.TryRead(out var rBuf)) {
                                    for (int b = 0; b < rBuf.Length; b++) {
                                        relayQueue.Enqueue(rBuf[b]);
                                    }
                                }
                            }

                            // Merge or inject local music into appBuffer
                            if (localMusicQueue.Count >= 4 && appBuffer != null) {
                                int lmBytesToRead = Math.Min(localMusicQueue.Count, appBuffer.Length);
                                lmBytesToRead -= lmBytesToRead % 4;

                                if (appBuffer == silentFrame) {
                                    // Replace silent frame with local music frame
                                    byte[] lmBuffer = new byte[appBuffer.Length];
                                    for (int b = 0; b < lmBytesToRead; b++) {
                                        lmBuffer[b] = localMusicQueue.Dequeue();
                                    }
                                    appBuffer = lmBuffer;
                                    wasSilent = false;
                                    lastAppPacketTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                                } else {
                                    // Sum external app audio with local music
                                    for (int b = 0; b < lmBytesToRead; b += 2) {
                                        byte lm0 = localMusicQueue.Dequeue();
                                        byte lm1 = localMusicQueue.Dequeue();
                                        short lmSample = (short)(lm0 | (lm1 << 8));
                                        short appSample = BitConverter.ToInt16(appBuffer, b);
                                        short mixed = (short)Math.Clamp(appSample + lmSample, short.MinValue, short.MaxValue);
                                        byte[] sumBytes = BitConverter.GetBytes(mixed);
                                        appBuffer[b] = sumBytes[0];
                                        appBuffer[b + 1] = sumBytes[1];
                                    }
                                }
                            }

                            // Merge or inject stream relay audio into appBuffer
                            if (relayQueue.Count >= 4 && appBuffer != null) {
                                int rBytesToRead = Math.Min(relayQueue.Count, appBuffer.Length);
                                rBytesToRead -= rBytesToRead % 4;

                                if (appBuffer == silentFrame) {
                                    // Replace silent frame with relay audio frame
                                    byte[] rBuffer = new byte[appBuffer.Length];
                                    for (int b = 0; b < rBytesToRead; b += 2) {
                                        byte r0 = relayQueue.Dequeue();
                                        byte r1 = relayQueue.Dequeue();
                                        short rSample = (short)(r0 | (r1 << 8));
                                        short scaled = (short)Math.Clamp(rSample * RelayVolume, short.MinValue, short.MaxValue);
                                        byte[] sBytes = BitConverter.GetBytes(scaled);
                                        rBuffer[b] = sBytes[0];
                                        rBuffer[b + 1] = sBytes[1];
                                    }
                                    appBuffer = rBuffer;
                                    wasSilent = false;
                                    lastAppPacketTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                                } else {
                                    // Sum external/local music with relay audio
                                    for (int b = 0; b < rBytesToRead; b += 2) {
                                        byte r0 = relayQueue.Dequeue();
                                        byte r1 = relayQueue.Dequeue();
                                        short rSample = (short)(r0 | (r1 << 8));
                                        short scaled = (short)(rSample * RelayVolume);
                                        short appSample = BitConverter.ToInt16(appBuffer, b);
                                        short mixed = (short)Math.Clamp(appSample + scaled, short.MinValue, short.MaxValue);
                                        byte[] sumBytes = BitConverter.GetBytes(mixed);
                                        appBuffer[b] = sumBytes[0];
                                        appBuffer[b + 1] = sumBytes[1];
                                    }
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

        public void PlayAudioFile(string filePath) {
            if (string.IsNullOrWhiteSpace(filePath) || !System.IO.File.Exists(filePath)) {
                return;
            }

            try {
                using var reader = new NAudio.Wave.WaveFileReader(filePath);
                byte[] data = new byte[reader.Length];
                int read = reader.Read(data, 0, data.Length);
                if (read > 0) {
                    if (read < data.Length) {
                        Array.Resize(ref data, read);
                    }
                    PlaySoundEffect(data);
                }
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[AudioDuckingMixer] Failed to play audio file {filePath}: {ex.Message}");
            }
        }

        private void ProcessMix(byte[] appBuffer, byte[] micBuffer) {
            byte[] outBuffer = new byte[appBuffer.Length];
            bool micActive = IsMicLive;
            double targetGain = micActive ? _duckingGain : 1.0;

            // Process Music Deck through native channel strip (Gain, Pan, Stereo Width, EQ, and DJ Color Filter)
            DjEq.ProcessMusicBuffer(appBuffer, appBuffer.Length);

            // Process Mic Deck through native channel strip (Gain, Pan, 80Hz low-cut)
            if (micActive) {
                DjEq.ProcessMicBuffer(micBuffer, micBuffer.Length);
            }

            while (_sfxQueue.TryDequeue(out var sfxBytes)) {
                for (int b = 0; b < sfxBytes.Length; b++) {
                    _activeSfxQueue.Enqueue(sfxBytes[b]);
                }
            }

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

                // Apply smooth ducking and stream volume to app audio
                double mixedAppL = appSampleL * _currentGain * AppVolume;
                double mixedAppR = appSampleR * _currentGain * AppVolume;
                
                // Sum 2-bus
                double sumL = mixedAppL + micSampleL + sfxL;
                double sumR = mixedAppR + micSampleR + sfxR;

                // Process through Master Bus (Master Gain, Balance, and Soft-Clip Limiter)
                (short finalL, short finalR) = DjEq.ProcessMasterSample(sumL, sumR);

                byte[] bytesL = BitConverter.GetBytes(finalL);
                byte[] bytesR = BitConverter.GetBytes(finalR);

                outBuffer[i] = bytesL[0];
                outBuffer[i + 1] = bytesL[1];
                if (i + 2 < appBuffer.Length) {
                    outBuffer[i + 2] = bytesR[0];
                    outBuffer[i + 3] = bytesR[1];
                }
            }

            // Analog VU decay ballistics
            LeftLevel = Math.Max(DjEq.MasterPeakL, LeftLevel * 0.85f);
            RightLevel = Math.Max(DjEq.MasterPeakR, RightLevel * 0.85f);
            DjEq.DecayMeters();

            if (LeftLevel < 0.001f) LeftLevel = 0f;
            if (RightLevel < 0.001f) RightLevel = 0f;

            _mixedOutput.Writer.TryWrite(outBuffer);
        }
    }
}
