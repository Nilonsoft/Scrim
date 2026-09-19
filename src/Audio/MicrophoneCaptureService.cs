using System;
using System.Threading.Channels;

namespace Scrim.Audio {
    public class MicrophoneCaptureService : IMicrophoneCaptureService {
        private readonly Channel<byte[]> _channel;
        
        public ChannelReader<byte[]> MicrophoneStream => _channel.Reader;

        public MicrophoneCaptureService() {
            _channel = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(100) {
                FullMode = BoundedChannelFullMode.DropOldest
            });
        }

        public void StartCapture() {
            // TODO: Implement WASAPI capture from default recording device
            // using IMMDeviceEnumerator and ActivateAudioInterfaceAsync
        }

        public void StopCapture() {
            // Stop the microphone capture loop
        }

        public void Dispose() {
            StopCapture();
        }
    }
}
