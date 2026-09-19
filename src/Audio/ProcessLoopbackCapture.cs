using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Scrim.Interop;

namespace Scrim.Audio {
    public class ProcessLoopbackCapture : IAudioCaptureService, IActivateAudioInterfaceCompletionHandler {
        private readonly Channel<byte[]> _channel;
        private IAudioClient? _audioClient;
        private IAudioCaptureClient? _captureClient;
#pragma warning disable CS0618
        private NAudio.Wave.WasapiLoopbackCapture? _wasapiLoopback;
#pragma warning restore CS0618
        private readonly object _captureLock = new object();
        private CancellationTokenSource? _cts;
        private Task? _captureTask;
        private uint _processId;
        private nint _pActivationParams = nint.Zero;

        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
        private static extern uint TimeBeginPeriod(uint uMilliseconds);

        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
        private static extern uint TimeEndPeriod(uint uMilliseconds);

        public ChannelReader<byte[]> AudioStream => _channel.Reader;

        public ProcessLoopbackCapture() {
            _channel = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions {
                SingleReader = true,
                SingleWriter = true
            });
        }

        private string? _captureDeviceId;

        public static System.Collections.Generic.IEnumerable<(string Id, string Name)> GetRenderDevices() {
            var enumerator = new NAudio.CoreAudioApi.MMDeviceEnumerator();
            var devices = enumerator.EnumerateAudioEndPoints(NAudio.CoreAudioApi.DataFlow.Render, NAudio.CoreAudioApi.DeviceState.Active);
            var list = new System.Collections.Generic.List<(string Id, string Name)>();
            foreach (var d in devices) {
                list.Add((d.ID, d.FriendlyName));
            }
            return list;
        }

        public void StartCapture(uint processId) {
            StartCapture(processId, null);
        }

        public void StartCapture(uint processId, string? deviceId) {
            lock (_captureLock) {
                if (_processId == processId && _captureDeviceId == deviceId && (_wasapiLoopback != null || (_captureTask != null && !_captureTask.IsCompleted))) {
                    return;
                }
                StopCapture();
                _processId = processId;
                _captureDeviceId = deviceId;
                _cts = new CancellationTokenSource();

                if (processId == 0) {
                    // System-wide Audio or Targeted Output Device Loopback:
                    StartWasapiLoopback(deviceId);
                    return;
                }

                // Process-specific capture for targeted window/PID
                var activationParams = new AUDIOCLIENT_ACTIVATION_PARAMS {
                    ActivationType = AUDIOCLIENT_ACTIVATION_TYPE.AUDIOCLIENT_ACTIVATION_TYPE_PROCESS_LOOPBACK,
                    ProcessLoopbackParams = new AUDIOCLIENT_PROCESS_LOOPBACK_PARAMS {
                        TargetProcessId = processId,
                        ProcessLoopbackMode = PROCESS_LOOPBACK_MODE.PROCESS_LOOPBACK_MODE_INCLUDE_TARGET_PROCESS_TREE
                    }
                };

                Guid IID_IAudioClient = new Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2");

                _pActivationParams = Marshal.AllocHGlobal(Marshal.SizeOf<AUDIOCLIENT_ACTIVATION_PARAMS>());
                Marshal.StructureToPtr(activationParams, _pActivationParams, false);

                try {
                    NativeMethods.ActivateAudioInterfaceAsync(
                        @"VIRTUAL_AUDIO_DEVICE_PROCESS_LOOPBACK",
                        ref IID_IAudioClient,
                        _pActivationParams,
                        this,
                        out _);
                } catch {
                    // Fall back to system audio loopback if process activation fails
                    StartWasapiLoopback(_captureDeviceId);
                }
            }
        }

        private void StartWasapiLoopback(string? deviceId = null) {
            try {
                StopWasapiLoopback();
#pragma warning disable CS0618
                if (string.IsNullOrEmpty(deviceId)) {
                    _wasapiLoopback = new NAudio.Wave.WasapiLoopbackCapture();
                } else {
                    var enumerator = new NAudio.CoreAudioApi.MMDeviceEnumerator();
                    var device = enumerator.GetDevice(deviceId);
                    _wasapiLoopback = new NAudio.Wave.WasapiLoopbackCapture(device);
                }
#pragma warning restore CS0618
                var inFormat = _wasapiLoopback.WaveFormat;
                _wasapiLoopback.DataAvailable += (s, e) => {
                    if (e.BytesRecorded > 0) {
                        byte[] pcm = ConvertTo16Bit44100Stereo(e.Buffer, e.BytesRecorded, inFormat);
                        if (pcm.Length > 0) {
                            _channel.Writer.TryWrite(pcm);
                        }
                    }
                };
                _wasapiLoopback.RecordingStopped += (s, e) => { };
                _wasapiLoopback.StartRecording();
            } catch (Exception ex) {
                Console.WriteLine($"[ProcessLoopbackCapture] Failed to start WASAPI loopback: {ex.Message}");
            }
        }

        private void StopWasapiLoopback() {
            if (_wasapiLoopback != null) {
                try {
                    _wasapiLoopback.StopRecording();
                    _wasapiLoopback.Dispose();
                } catch { }
                _wasapiLoopback = null;
            }
        }

        public int ActivateCompleted(IActivateAudioInterfaceAsyncOperation activateOperation) {
            if (_pActivationParams != nint.Zero) {
                Marshal.FreeHGlobal(_pActivationParams);
                _pActivationParams = nint.Zero;
            }

            activateOperation.GetActivateResult(out int hr, out object activatedInterface);
            if (hr < 0 || activatedInterface == null) {
                // Fall back to system-wide loopback
                StartWasapiLoopback(_captureDeviceId);
                return hr;
            }

            _audioClient = (IAudioClient)activatedInterface;

            var format = new WAVEFORMATEX {
                wFormatTag = 1,
                nChannels = 2,
                nSamplesPerSec = 44100,
                nAvgBytesPerSec = 44100 * 2 * 2,
                nBlockAlign = 4,
                wBitsPerSample = 16,
                cbSize = 0
            };

            Guid sessionGuid = Guid.Empty;
            // AUDCLNT_STREAMFLAGS_LOOPBACK (0x00020000) | AUDCLNT_STREAMFLAGS_AUTOCONVERTPCM (0x80000000) | AUDCLNT_STREAMFLAGS_SRC_DEFAULT_QUALITY (0x08000000)
            uint streamFlags = 0x00020000 | 0x80000000 | 0x08000000;

            // 100ms buffer in 100-nanosecond units (100 * 10000 = 1,000,000 hns) prevents WASAPI buffer overruns under system load
            const long BufferDuration100Ms = 1000000L;

            int initResult = _audioClient.Initialize(
                0, // AUDCLNT_SHAREMODE_SHARED
                streamFlags,
                BufferDuration100Ms, 
                0,
                ref format,
                ref sessionGuid);

            if (initResult < 0) {
                initResult = _audioClient.Initialize(
                    0,
                    0x00020000,
                    BufferDuration100Ms,
                    0,
                    ref format,
                    ref sessionGuid);
            }

            if (initResult < 0) {
                StartWasapiLoopback(_captureDeviceId);
                return initResult;
            }

            Guid IID_IAudioCaptureClient = new Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317");
            int svcResult = _audioClient.GetService(ref IID_IAudioCaptureClient, out nint pCaptureClient);
            if (svcResult >= 0 && pCaptureClient != nint.Zero) {
                _captureClient = (IAudioCaptureClient)Marshal.GetObjectForIUnknown(pCaptureClient);
                _audioClient.Start();
                
                var tcs = new TaskCompletionSource();
                var thread = new Thread(() => {
                    try {
                        CaptureLoop(_cts!.Token);
                    } finally {
                        tcs.TrySetResult();
                    }
                }) {
                    IsBackground = true,
                    Priority = ThreadPriority.AboveNormal,
                    Name = "ScrimLoopbackCaptureThread"
                };
                thread.Start();
                _captureTask = tcs.Task;
            }

            return 0;
        }

        private void CaptureLoop(CancellationToken token) {
            TimeBeginPeriod(1);
            try {
                while (!token.IsCancellationRequested) {
                    if (_captureClient == null) {
                        Thread.Sleep(2);
                        continue;
                    }

                    _captureClient.GetNextPacketSize(out uint packetLength);
                    if (packetLength == 0) {
                        Thread.Sleep(1);
                        continue;
                    }

                    while (packetLength > 0 && !token.IsCancellationRequested) {
                        _captureClient.GetBuffer(out nint pData, out uint numFramesToRead, out uint flags, out ulong devicePosition, out ulong qpcPosition);

                        if (numFramesToRead > 0) {
                            int bytesToRead = (int)(numFramesToRead * 4);
                            byte[] buffer = new byte[bytesToRead];

                            // Flag 0x2 is AUDCLNT_BUFFERFLAGS_SILENT
                            if ((flags & 2) != 0 || pData == nint.Zero) {
                                Array.Clear(buffer, 0, bytesToRead);
                            } else {
                                Marshal.Copy(pData, buffer, 0, bytesToRead);
                            }

                            _channel.Writer.TryWrite(buffer);
                        }

                        _captureClient.ReleaseBuffer(numFramesToRead);
                        _captureClient.GetNextPacketSize(out packetLength);
                    }
                }
            } finally {
                TimeEndPeriod(1);
            }
        }

        public void StopCapture() {
            lock (_captureLock) {
                StopWasapiLoopback();
                _cts?.Cancel();
                try {
                    _captureTask?.Wait(200);
                } catch { }
                try {
                    _audioClient?.Stop();
                } catch { }
                if (_pActivationParams != nint.Zero) {
                    try {
                        Marshal.FreeHGlobal(_pActivationParams);
                    } catch { }
                    _pActivationParams = nint.Zero;
                }
                if (_audioClient != null && Marshal.IsComObject(_audioClient)) {
                    try {
                        Marshal.ReleaseComObject(_audioClient);
                    } catch { }
                    _audioClient = null;
                }
                if (_captureClient != null && Marshal.IsComObject(_captureClient)) {
                    try {
                        Marshal.ReleaseComObject(_captureClient);
                    } catch { }
                    _captureClient = null;
                }
            }
        }

        private static byte[] ConvertTo16Bit44100Stereo(byte[] inBuffer, int bytesRecorded, NAudio.Wave.WaveFormat inFormat) {
            if (bytesRecorded <= 0 || inBuffer == null) return Array.Empty<byte>();

            int channels = Math.Max(1, inFormat.Channels);
            int sampleRate = inFormat.SampleRate;

            // Direct pass-through if already 16-bit 44.1kHz stereo
            if (sampleRate == 44100 && inFormat.BitsPerSample == 16 && channels == 2) {
                byte[] copy = new byte[bytesRecorded];
                Buffer.BlockCopy(inBuffer, 0, copy, 0, bytesRecorded);
                return copy;
            }

            // Convert float (32-bit) or PCM (16-bit) to 44.1kHz 16-bit stereo
            bool isFloat = inFormat.Encoding == NAudio.Wave.WaveFormatEncoding.IeeeFloat || inFormat.BitsPerSample == 32;
            int bytesPerFrame = channels * (isFloat ? 4 : 2);
            if (bytesPerFrame <= 0) return Array.Empty<byte>();

            int inFrames = bytesRecorded / bytesPerFrame;
            if (inFrames <= 0) return Array.Empty<byte>();

            int outFrames = (sampleRate == 44100) ? inFrames : (int)Math.Round(inFrames * 44100.0 / sampleRate);
            if (outFrames <= 0) return Array.Empty<byte>();

            byte[] outBytes = new byte[outFrames * 4];
            double ratio = (double)sampleRate / 44100.0;

            for (int j = 0; j < outFrames; j++) {
                double inIndex = j * ratio;
                int idx0 = (int)inIndex;
                double frac = inIndex - idx0;
                int idx1 = Math.Min(idx0 + 1, inFrames - 1);

                float l0, r0, l1, r1;

                if (isFloat) {
                    int offset0 = idx0 * bytesPerFrame;
                    int offset1 = idx1 * bytesPerFrame;

                    l0 = BitConverter.ToSingle(inBuffer, offset0);
                    r0 = channels >= 2 ? BitConverter.ToSingle(inBuffer, offset0 + 4) : l0;

                    l1 = BitConverter.ToSingle(inBuffer, offset1);
                    r1 = channels >= 2 ? BitConverter.ToSingle(inBuffer, offset1 + 4) : l1;
                } else {
                    int offset0 = idx0 * bytesPerFrame;
                    int offset1 = idx1 * bytesPerFrame;

                    l0 = BitConverter.ToInt16(inBuffer, offset0) / 32768.0f;
                    r0 = channels >= 2 ? BitConverter.ToInt16(inBuffer, offset0 + 2) / 32768.0f : l0;

                    l1 = BitConverter.ToInt16(inBuffer, offset1) / 32768.0f;
                    r1 = channels >= 2 ? BitConverter.ToInt16(inBuffer, offset1 + 2) / 32768.0f : l1;
                }

                float l = (float)(l0 * (1.0 - frac) + l1 * frac);
                float r = (float)(r0 * (1.0 - frac) + r1 * frac);

                short sL = (short)Math.Clamp((int)(l * 32767.0f), short.MinValue, short.MaxValue);
                short sR = (short)Math.Clamp((int)(r * 32767.0f), short.MinValue, short.MaxValue);

                byte[] bL = BitConverter.GetBytes(sL);
                byte[] bR = BitConverter.GetBytes(sR);

                int outOffset = j * 4;
                outBytes[outOffset] = bL[0];
                outBytes[outOffset + 1] = bL[1];
                outBytes[outOffset + 2] = bR[0];
                outBytes[outOffset + 3] = bR[1];
            }

            return outBytes;
        }

        public void Dispose() {
            StopCapture();
        }
    }
}
