using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Control;

namespace Scrim.Metadata {
    public class WindowsMediaMetadataService : IMetadataService {
        private uint _targetProcessId;
        private CancellationTokenSource? _cts;
        private GlobalSystemMediaTransportControlsSessionManager? _sessionManager;

        public MediaMetadata CurrentMetadata { get; private set; } = new MediaMetadata();

        public event EventHandler<MediaMetadata>? MetadataChanged;

        public void StartMonitoring(uint targetProcessId) {
            _targetProcessId = targetProcessId;
            _cts = new CancellationTokenSource();
            
            Task.Run(() => MonitorLoop(_cts.Token));
        }

        private async Task MonitorLoop(CancellationToken token) {
            try {
                _sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                if (_sessionManager != null) {
                    _sessionManager.SessionsChanged += SessionManager_SessionsChanged;
                    UpdateMetadata();
                }
            } catch {
                // Ignore errors related to UWP/WinRT initialization if unsupported
            }

            try {
                await Task.Delay(-1, token);
            } catch (TaskCanceledException) { }
        }

        private void SessionManager_SessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args) {
            UpdateMetadata();
        }

        private void UpdateMetadata() {
            if (_sessionManager == null) return;
            var session = _sessionManager.GetCurrentSession();
            if (session != null) {
                var timeline = session.GetTimelineProperties();
                session.TryGetMediaPropertiesAsync().AsTask().ContinueWith(t => {
                    if (t.IsCompletedSuccessfully && t.Result != null) {
                        var props = t.Result;
                        var newMeta = new MediaMetadata {
                            Title = props.Title,
                            Artist = props.Artist,
                            Album = props.AlbumTitle
                        };

                        if (newMeta.Title != CurrentMetadata.Title || newMeta.Artist != CurrentMetadata.Artist) {
                            OnMetadataChanged(newMeta);
                        }
                    }
                });
            }
        }

        protected virtual void OnMetadataChanged(MediaMetadata metadata) {
            CurrentMetadata = metadata;
            MetadataChanged?.Invoke(this, metadata);
        }

        public void StopMonitoring() {
            if (_sessionManager != null) {
                _sessionManager.SessionsChanged -= SessionManager_SessionsChanged;
            }
            _cts?.Cancel();
        }

        public async Task<bool> TogglePlayPauseAsync() {
            try {
                if (_sessionManager == null) {
                    _sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                }
                var session = _sessionManager?.GetCurrentSession();
                if (session != null) {
                    return await session.TryTogglePlayPauseAsync();
                }
            } catch { }
            return false;
        }

        public async Task<bool> SkipNextAsync() {
            try {
                if (_sessionManager == null) {
                    _sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                }
                var session = _sessionManager?.GetCurrentSession();
                if (session != null) {
                    return await session.TrySkipNextAsync();
                }
            } catch { }
            return false;
        }

        public async Task<bool> SkipPreviousAsync() {
            try {
                if (_sessionManager == null) {
                    _sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                }
                var session = _sessionManager?.GetCurrentSession();
                if (session != null) {
                    return await session.TrySkipPreviousAsync();
                }
            } catch { }
            return false;
        }

        public void Dispose() {
            StopMonitoring();
        }
    }
}
