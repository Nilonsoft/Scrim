using System;
using System.Threading.Channels;

namespace Scrim.Audio {
    public interface IAudioCaptureService : IDisposable {
        ChannelReader<byte[]> AudioStream { get; }
        void StartCapture(uint processId);
        void StopCapture();
    }
}
