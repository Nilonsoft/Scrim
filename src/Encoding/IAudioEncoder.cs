using System;
using System.Threading.Channels;

namespace Scrim.Encoding {
    public interface IAudioEncoder : IDisposable {
        AudioFormat Format { get; }
        int Bitrate { get; }
        ChannelReader<byte[]> EncodedStream { get; }
        void StartEncoding(ChannelReader<byte[]> pcmStream);
        void StopEncoding();
    }
}
