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

        public void StartCapture(uint processId) {
            if (_processId == processId && _captureTask != null && !_captureTask.IsCompleted) {
                return;
            }
            StopCapture();
            _processId = processId;
            _cts = new CancellationTokenSource();

            uint targetPid = processId == 0 ? (uint)Environment.ProcessId : processId;
            var loopbackMode = processId == 0 
                ? PROCESS_LOOPBACK_MODE.PROCESS_LOOPBACK_MODE_EXCLUDE_TARGET_PROCESS_TREE 
                : PROCESS_LOOPBACK_MODE.PROCESS_LOOPBACK_MODE_INCLUDE_TARGET_PROCESS_TREE;

            var activationParams = new AUDIOCLIENT_ACTIVATION_PARAMS {
                ActivationType = AUDIOCLIENT_ACTIVATION_TYPE.AUDIOCLIENT_ACTIVATION_TYPE_PROCESS_LOOPBACK,
                ProcessLoopbackParams = new AUDIOCLIENT_PROCESS_LOOPBACK_PARAMS {
                    TargetProcessId = targetPid,
                    ProcessLoopbackMode = loopbackMode
                }
            };

            Guid IID_IAudioClient = new Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2");

            _pActivationParams = Marshal.AllocHGlobal(Marshal.SizeOf<AUDIOCLIENT_ACTIVATION_PARAMS>());
            Marshal.StructureToPtr(activationParams, _pActivationParams, false);

            NativeMethods.ActivateAudioInterfaceAsync(
                @"VIRTUAL_AUDIO_DEVICE_PROCESS_LOOPBACK",
                ref IID_IAudioClient,
                _pActivationParams,
                this,
                out _);
        }

        public int ActivateCompleted(IActivateAudioInterfaceAsyncOperation activateOperation) {
            if (_pActivationParams != nint.Zero) {
                Marshal.FreeHGlobal(_pActivationParams);
                _pActivationParams = nint.Zero;
            }

            activateOperation.GetActivateResult(out int hr, out object activatedInterface);
            Console.WriteLine($"[ProcessLoopbackCapture] ActivateResult: hr=0x{hr:X8}, itf={activatedInterface}");
            if (hr < 0 || activatedInterface == null) {
                return hr;
            }

            _audioClient = (IAudioClient)activatedInterface;
            int mixFormatHr = _audioClient.GetMixFormat(out nint pMixFormat);
            Console.WriteLine($"[ProcessLoopbackCapture] GetMixFormat: hr=0x{mixFormatHr:X8}, pMix={pMixFormat}");
            if (pMixFormat != nint.Zero) {
                var mixWf = Marshal.PtrToStructure<WAVEFORMATEX>(pMixFormat);
                Console.WriteLine($"[ProcessLoopbackCapture] MixFormat: tag={mixWf.wFormatTag}, ch={mixWf.nChannels}, rate={mixWf.nSamplesPerSec}, bits={mixWf.wBitsPerSample}");
            }

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
            Console.WriteLine($"[ProcessLoopbackCapture] Initialize (44.1k): hr=0x{initResult:X8}");

            if (initResult < 0) {
                initResult = _audioClient.Initialize(
                    0,
                    0x00020000,
                    BufferDuration100Ms,
                    0,
                    ref format,
                    ref sessionGuid);
                Console.WriteLine($"[ProcessLoopbackCapture] Initialize fallback: hr=0x{initResult:X8}");
            }

            if (initResult < 0) {
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

        public void Dispose() {
            StopCapture();
        }
    }
}
