using System;
using System.Collections.Generic;
using System.Threading.Channels;

namespace Scrim.Audio {
    public class MicrophoneDevice {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public interface IMicrophoneCaptureService : IDisposable {
        ChannelReader<byte[]> MicrophoneStream { get; }
        string CurrentEffect { get; set; }
        bool IsMonitoring { get; set; }
        bool IsMicTestMode { get; set; }
        float InputLevel { get; }
        string? VirtualOutputDeviceId { get; }
        bool IsVirtualOutputActive { get; }
        IEnumerable<MicrophoneDevice> GetDevices();
        void StartCapture(string? deviceId = null);
        void StopCapture();
        void StopMonitoringPlayback();
        void SetVirtualOutputDevice(string? deviceId);
    }
}
