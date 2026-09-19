using System;
using System.Threading.Channels;
using NAudio.Wave;
using NAudio.CoreAudioApi;

namespace Scrim.Audio {
    public class MicrophoneCaptureService : IMicrophoneCaptureService {
        private readonly Channel<byte[]> _channel;
        private WasapiCapture? _capture;
        private readonly VoiceEffectLoader _effectLoader;
        
        public ChannelReader<byte[]> MicrophoneStream => _channel.Reader;
        public string CurrentEffect { get; set; } = "normal";

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
            
            if (string.IsNullOrEmpty(deviceId)) {
                _capture = new WasapiCapture();
            } else {
                var enumerator = new MMDeviceEnumerator();
                var device = enumerator.GetDevice(deviceId);
                _capture = new WasapiCapture(device);
            }

            // Force 44100Hz 16-bit stereo for uniformity with Loopback
            _capture.WaveFormat = new WaveFormat(44100, 16, 2);
            
            _capture.DataAvailable += (s, a) => {
                if (a.BytesRecorded > 0) {
                    byte[] buffer = new byte[a.BytesRecorded];
                    Array.Copy(a.Buffer, buffer, a.BytesRecorded);
                    
                    var provider = _effectLoader.GetProvider(CurrentEffect);
                    provider?.Process(buffer);
                    
                    _channel.Writer.TryWrite(buffer);
                }
            };

            _capture.StartRecording();
        }

        public void StopCapture() {
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
