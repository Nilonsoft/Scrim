using System;
using System.Threading.Channels;

namespace Scrim.Audio {
    public interface IMicrophoneCaptureService : IDisposable {
        ChannelReader<byte[]> MicrophoneStream { get; }
        void StartCapture();
        void StopCapture();
    }
}
