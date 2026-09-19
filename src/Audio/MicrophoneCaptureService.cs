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
#pragma warning restore CS0618 // Type or member is obsolete
        private readonly VoiceEffectLoader _effectLoader;
        private BufferedWaveProvider? _monitorBuffer;
        private readonly object _monitorLock = new();
        
        public ChannelReader<byte[]> MicrophoneStream => _channel.Reader;
        public string CurrentEffect { get; set; } = "normal";
        public bool IsMonitoring { get; set; } = false;
        public bool IsMicTestMode { get; set; } = false;
        public float InputLevel { get; private set; } = 0f;

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

        public void StartCapture(string? deviceId = null) {
            if (_capture != null) return;
            
#pragma warning disable CS0618 // Type or member is obsolete
            if (string.IsNullOrEmpty(deviceId)) {
                _capture = new WasapiCapture();
            } else {
                var enumerator = new MMDeviceEnumerator();
                var device = enumerator.GetDevice(deviceId);
                _capture = new WasapiCapture(device);
            }
#pragma warning restore CS0618 // Type or member is obsolete

            // Force 44100Hz 16-bit stereo for uniformity with Loopback
            _capture.WaveFormat = new WaveFormat(44100, 16, 2);
            
            _capture.DataAvailable += (s, a) => {
                if (a.BytesRecorded > 0) {
                    byte[] buffer = new byte[a.BytesRecorded];
                    Array.Copy(a.Buffer, buffer, a.BytesRecorded);
                    
                    var provider = _effectLoader.GetProvider(CurrentEffect);
                    provider?.Process(buffer);

                    // Track real-time input peak level for microphone gauge
                    float maxSample = 0f;
                    for (int i = 0; i < a.BytesRecorded; i += 2) {
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

                    // Only send to mixer and live broadcast stream when NOT in private test mode
                    if (!IsMicTestMode) {
                        _channel.Writer.TryWrite(buffer);
                    }
                }
            };

            _capture.StartRecording();
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

        public void StopCapture() {
            StopMonitoringPlayback();
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
