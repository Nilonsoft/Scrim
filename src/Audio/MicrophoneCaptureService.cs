using System;
using System.Threading.Channels;
using NAudio.Wave;
using NAudio.CoreAudioApi;

namespace Scrim.Audio {
    public class MicrophoneCaptureService : IMicrophoneCaptureService {
        private readonly Channel<byte[]> _channel;
#pragma warning disable CS0618 // Type or member is obsolete
        private WasapiCapture? _capture;
        private WasapiOut? _monitorOut;
        private WasapiOut? _virtualOut;
#pragma warning restore CS0618 // Type or member is obsolete
        private readonly VoiceEffectLoader _effectLoader;
        private BufferedWaveProvider? _monitorBuffer;
        private BufferedWaveProvider? _virtualBuffer;
        private readonly object _monitorLock = new();
        private readonly object _virtualLock = new();
        private string? _virtualDeviceId;
        
        public ChannelReader<byte[]> MicrophoneStream => _channel.Reader;
        public string CurrentEffect { get; set; } = "normal";
        public bool IsMonitoring { get; set; } = false;
        public bool IsMicTestMode { get; set; } = false;
        public float InputLevel { get; private set; } = 0f;
        public string? VirtualOutputDeviceId => _virtualDeviceId;
        public bool IsVirtualOutputActive => _virtualOut != null;

        public MicrophoneCaptureService(VoiceEffectLoader effectLoader) {
            _effectLoader = effectLoader;
            _channel = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(100) {
                FullMode = BoundedChannelFullMode.DropOldest
            });
        }

        public IEnumerable<MicrophoneDevice> GetDevices() {
            var enumerator = new MMDeviceEnumerator();
            var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
            var list = new List<MicrophoneDevice>();
            foreach (var d in devices) {
                list.Add(new MicrophoneDevice { Id = d.ID, Name = d.FriendlyName });
            }
            return list;
        }

        public void SetVirtualOutputDevice(string? deviceId) {
            lock (_virtualLock) {
                if (_virtualDeviceId == deviceId && _virtualOut != null) return;
                StopVirtualOutput();
                _virtualDeviceId = deviceId;
                if (!string.IsNullOrEmpty(deviceId)) {
                    EnsureVirtualPlayback();
                }
            }
        }

        private static bool IsVirtualCableDevice(string? name) {
            if (string.IsNullOrWhiteSpace(name)) return false;
            return name.Contains("cable", StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("vb-audio", StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("virtual audio", StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("virtual line", StringComparison.OrdinalIgnoreCase);
        }

        public void StartCapture(string? deviceId = null) {
            if (_capture != null) return;

            var enumerator = new MMDeviceEnumerator();
            MMDevice? targetDevice = null;

            if (!string.IsNullOrEmpty(deviceId)) {
                try {
                    var dev = enumerator.GetDevice(deviceId);
                    if (dev != null && !IsVirtualCableDevice(dev.FriendlyName)) {
                        targetDevice = dev;
                    } else if (dev != null) {
                        Console.WriteLine($"[MicrophoneCaptureService] Ignored virtual cable device '{dev.FriendlyName}' for voice microphone. Auto-detecting physical mic.");
                    }
                } catch { }
            }

            if (targetDevice == null) {
                // Determine best physical microphone endpoint, avoiding virtual cables
                try {
                    var defaultComm = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
                    if (defaultComm != null && !IsVirtualCableDevice(defaultComm.FriendlyName)) {
                        targetDevice = defaultComm;
                    }
                } catch { }

                if (targetDevice == null) {
                    try {
                        var defaultConsole = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Console);
                        if (defaultConsole != null && !IsVirtualCableDevice(defaultConsole.FriendlyName)) {
                            targetDevice = defaultConsole;
                        }
                    } catch { }
                }

                if (targetDevice == null) {
                    try {
                        var active = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
                        targetDevice = active.FirstOrDefault(d => !IsVirtualCableDevice(d.FriendlyName))
                                    ?? active.FirstOrDefault();
                    } catch { }
                }
            }

#pragma warning disable CS0618 // Type or member is obsolete
            if (targetDevice != null) {
                _capture = new WasapiCapture(targetDevice);
            } else {
                _capture = new WasapiCapture();
            }
#pragma warning restore CS0618 // Type or member is obsolete

            var inFormat = _capture.WaveFormat;
            
            _capture.DataAvailable += (s, a) => {
                if (a.BytesRecorded > 0) {
                    byte[] buffer = ConvertTo16Bit44100Stereo(a.Buffer, a.BytesRecorded, inFormat);
                    if (buffer.Length == 0) return;
                    
                    var provider = _effectLoader.GetProvider(CurrentEffect);
                    provider?.Process(buffer);

                    // Track real-time input peak level for microphone gauge
                    float maxSample = 0f;
                    for (int i = 0; i < buffer.Length; i += 2) {
                        short sample = BitConverter.ToInt16(buffer, i);
                        float abs = Math.Abs(sample) / 32768.0f;
                        if (abs > maxSample) maxSample = abs;
                    }
                    InputLevel = Math.Max(maxSample, InputLevel * 0.82f);

                    // Sidetone / Headphone monitoring
                    if (IsMonitoring || IsMicTestMode) {
                        EnsureMonitorPlayback();
                        if (_monitorBuffer != null) {
                            // Avoid sudden DC step truncations from clock drift by maintaining healthy headroom
                            if (_monitorBuffer.BufferedBytes > _monitorBuffer.BufferLength * 0.75) {
                                _monitorBuffer.ClearBuffer();
                            }
                            _monitorBuffer.AddSamples(buffer, 0, buffer.Length);
                        }
                    } else if (_monitorOut != null) {
                        StopMonitoringPlayback();
                    }

                    // Route to virtual capture device if configured
                    if (_virtualOut != null && _virtualBuffer != null) {
                        if (_virtualBuffer.BufferedBytes > _virtualBuffer.BufferLength * 0.75) {
                            _virtualBuffer.ClearBuffer();
                        }
                        _virtualBuffer.AddSamples(buffer, 0, buffer.Length);
                    }

                    // Only send to mixer and live broadcast stream when NOT in private test mode
                    if (!IsMicTestMode) {
                        _channel.Writer.TryWrite(buffer);
                    }
                }
            };

            try {
                _capture.StartRecording();
            } catch (Exception ex) {
                Console.WriteLine($"[MicrophoneCaptureService] Failed to start recording: {ex.Message}");
                _capture.Dispose();
                _capture = null;
                return;
            }

            EnsureVirtualPlayback();
        }

        private static byte[] ConvertTo16Bit44100Stereo(byte[] inBuffer, int bytesRecorded, WaveFormat inFormat) {
            if (bytesRecorded <= 0 || inBuffer == null) return Array.Empty<byte>();

            int channels = Math.Max(1, inFormat.Channels);
            int sampleRate = inFormat.SampleRate;

            // Direct pass-through if already 16-bit 44.1kHz stereo
            if (sampleRate == 44100 && inFormat.BitsPerSample == 16 && channels == 2) {
                byte[] copy = new byte[bytesRecorded];
                Buffer.BlockCopy(inBuffer, 0, copy, 0, bytesRecorded);
                return copy;
            }

            // Convert float (32-bit) or PCM (16-bit / 24-bit) to 44.1kHz 16-bit stereo
            bool isFloat = inFormat.Encoding == WaveFormatEncoding.IeeeFloat || inFormat.BitsPerSample == 32;
            int bytesPerSample = isFloat ? 4 : (inFormat.BitsPerSample / 8);
            if (bytesPerSample <= 0) bytesPerSample = 2;
            int bytesPerFrame = channels * bytesPerSample;
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
                } else if (inFormat.BitsPerSample == 16) {
                    int offset0 = idx0 * bytesPerFrame;
                    int offset1 = idx1 * bytesPerFrame;

                    l0 = BitConverter.ToInt16(inBuffer, offset0) / 32768.0f;
                    r0 = channels >= 2 ? BitConverter.ToInt16(inBuffer, offset0 + 2) / 32768.0f : l0;

                    l1 = BitConverter.ToInt16(inBuffer, offset1) / 32768.0f;
                    r1 = channels >= 2 ? BitConverter.ToInt16(inBuffer, offset1 + 2) / 32768.0f : l1;
                } else {
                    // 24-bit or other integer PCM fallback
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

                int outOffset = j * 4;
                outBytes[outOffset] = (byte)(sL & 0xFF);
                outBytes[outOffset + 1] = (byte)((sL >> 8) & 0xFF);
                outBytes[outOffset + 2] = (byte)(sR & 0xFF);
                outBytes[outOffset + 3] = (byte)((sR >> 8) & 0xFF);
            }

            return outBytes;
        }

        private void EnsureMonitorPlayback() {
            lock (_monitorLock) {
                if (_monitorOut == null) {
                    try {
                        _monitorBuffer = new BufferedWaveProvider(new WaveFormat(44100, 16, 2), TimeSpan.FromMilliseconds(800)) {
                            DiscardOnBufferOverflow = true
                        };
#pragma warning disable CS0618 // Type or member is obsolete
                        _monitorOut = new WasapiOut(AudioClientShareMode.Shared, 50);
#pragma warning restore CS0618 // Type or member is obsolete
                        _monitorOut.Init(_monitorBuffer);
                        _monitorOut.Play();
                    } catch { }
                }
            }
        }

        private void EnsureVirtualPlayback() {
            lock (_virtualLock) {
                if (_virtualOut == null && !string.IsNullOrEmpty(_virtualDeviceId)) {
                    try {
                        var enumerator = new MMDeviceEnumerator();
                        var device = enumerator.GetDevice(_virtualDeviceId);
                        _virtualBuffer = new BufferedWaveProvider(new WaveFormat(44100, 16, 2), TimeSpan.FromMilliseconds(800)) {
                            DiscardOnBufferOverflow = true
                        };
#pragma warning disable CS0618 // Type or member is obsolete
                        _virtualOut = new WasapiOut(device, AudioClientShareMode.Shared, false, 50);
#pragma warning restore CS0618 // Type or member is obsolete
                        _virtualOut.Init(_virtualBuffer);
                        _virtualOut.Play();
                    } catch {
                        _virtualOut = null;
                        _virtualBuffer = null;
                    }
                }
            }
        }

        public void StopMonitoringPlayback() {
            lock (_monitorLock) {
                if (_monitorOut != null) {
                    try {
                        _monitorOut.Stop();
                        _monitorOut.Dispose();
                    } catch { }
                    _monitorOut = null;
                    _monitorBuffer = null;
                }
            }
        }

        public void StopVirtualOutput() {
            lock (_virtualLock) {
                if (_virtualOut != null) {
                    try {
                        _virtualOut.Stop();
                        _virtualOut.Dispose();
                    } catch { }
                    _virtualOut = null;
                    _virtualBuffer = null;
                }
            }
        }

        public void StopCapture() {
            StopMonitoringPlayback();
            StopVirtualOutput();
            if (_capture != null) {
                _capture.StopRecording();
                _capture.Dispose();
                _capture = null;
            }
        }

        public void Dispose() {
            StopCapture();
        }
    }
}
