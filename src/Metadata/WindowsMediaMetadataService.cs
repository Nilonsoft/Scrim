using System;
using System.Threading;
using System.Threading.Tasks;

namespace Scrim.Metadata {
    public class WindowsMediaMetadataService : IMetadataService {
        private uint _targetProcessId;
        private CancellationTokenSource? _cts;

        public MediaMetadata CurrentMetadata { get; private set; } = new MediaMetadata();

        public event EventHandler<MediaMetadata>? MetadataChanged;

        public void StartMonitoring(uint targetProcessId) {
            _targetProcessId = targetProcessId;
            _cts = new CancellationTokenSource();
            
            Task.Run(() => MonitorLoop(_cts.Token));
        }

        private async Task MonitorLoop(CancellationToken token) {
            // TODO: Hook into Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager
            // to fetch real-time playing media based on process ID.
            
            while (!token.IsCancellationRequested) {
                // Mock metadata update
                await Task.Delay(10000, token);
            }
        }

        protected virtual void OnMetadataChanged(MediaMetadata metadata) {
            CurrentMetadata = metadata;
            MetadataChanged?.Invoke(this, metadata);
        }

        public void StopMonitoring() {
            _cts?.Cancel();
        }

        public void Dispose() {
            StopMonitoring();
        }
    }
}
