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

        public ChannelReader<byte[]> AudioStream => _channel.Reader;

        public ProcessLoopbackCapture() {
            _channel = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(100) {
                FullMode = BoundedChannelFullMode.DropOldest
            });
        }

        public void StartCapture(uint processId) {
            _processId = processId;
            _cts = new CancellationTokenSource();

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
            if (hr < 0 || activatedInterface == null) {
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
            uint streamFlags = 0x00020000; // AUDCLNT_STREAMFLAGS_LOOPBACK

            _audioClient.Initialize(
                0, // AUDCLNT_SHAREMODE_SHARED
                streamFlags,
                0, 
                0,
                ref format,
                ref sessionGuid);

            Guid IID_IAudioCaptureClient = new Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317");
            _audioClient.GetService(ref IID_IAudioCaptureClient, out nint pCaptureClient);
            _captureClient = (IAudioCaptureClient)Marshal.GetObjectForIUnknown(pCaptureClient);

            _audioClient.Start();

            _captureTask = Task.Run(() => CaptureLoop(_cts!.Token));

            return 0;
        }

        private void CaptureLoop(CancellationToken token) {
            while (!token.IsCancellationRequested) {
                Thread.Sleep(10);
                
                if (_captureClient == null) continue;

                _captureClient.GetNextPacketSize(out uint packetLength);
                while (packetLength > 0 && !token.IsCancellationRequested) {
                    _captureClient.GetBuffer(out nint pData, out uint numFramesToRead, out uint flags, out ulong devicePosition, out ulong qpcPosition);

                    if (numFramesToRead > 0) {
                        int bytesToRead = (int)(numFramesToRead * 4);
                        byte[] buffer = new byte[bytesToRead];
                        Marshal.Copy(pData, buffer, 0, bytesToRead);
                        _channel.Writer.TryWrite(buffer);
                    }

                    _captureClient.ReleaseBuffer(numFramesToRead);
                    _captureClient.GetNextPacketSize(out packetLength);
                }
            }
        }

        public void StopCapture() {
            _cts?.Cancel();
            _captureTask?.Wait();
            _audioClient?.Stop();
        }

        public void Dispose() {
            StopCapture();
            if (_pActivationParams != nint.Zero) {
                Marshal.FreeHGlobal(_pActivationParams);
                _pActivationParams = nint.Zero;
            }
            if (_audioClient != null && Marshal.IsComObject(_audioClient)) {
                Marshal.ReleaseComObject(_audioClient);
            }
            if (_captureClient != null && Marshal.IsComObject(_captureClient)) {
                Marshal.ReleaseComObject(_captureClient);
            }
        }
    }
}
