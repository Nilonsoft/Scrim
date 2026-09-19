using System;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using Windows.Media.Control;

namespace Scrim.Metadata {
    public class WindowsMediaMetadataService : IMetadataService {
        private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(4) };
        private uint _targetProcessId;
        private CancellationTokenSource? _cts;
        private GlobalSystemMediaTransportControlsSessionManager? _sessionManager;
        private GlobalSystemMediaTransportControlsSession? _currentSession;
        private readonly object _lock = new();

        public MediaMetadata CurrentMetadata { get; private set; } = new MediaMetadata();

        public event EventHandler<MediaMetadata>? MetadataChanged;

        public WindowsMediaMetadataService() {
            StartMonitoring(0);
        }

        public void StartMonitoring(uint targetProcessId) {
            lock (_lock) {
                _targetProcessId = targetProcessId;
                if (_cts != null) {
                    return;
                }
                _cts = new CancellationTokenSource();
            }

            Task.Run(() => MonitorLoop(_cts.Token));
        }

        private async Task MonitorLoop(CancellationToken token) {
            try {
                _sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                if (_sessionManager != null) {
                    _sessionManager.SessionsChanged += SessionManager_SessionsChanged;
                    _sessionManager.CurrentSessionChanged += SessionManager_CurrentSessionChanged;
                    HookCurrentSession();
                    await UpdateMetadataAsync();
                }
            } catch {
                // Ignore errors related to UWP/WinRT initialization if unsupported
            }

            while (!token.IsCancellationRequested) {
                try {
                    await Task.Delay(1000, token);
                    await UpdateMetadataAsync();
                } catch (TaskCanceledException) {
                    break;
                } catch {
                    // Safe catch to ensure loop keeps running
                }
            }
        }

        private void SessionManager_SessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args) {
            HookCurrentSession();
            _ = UpdateMetadataAsync();
        }

        private void SessionManager_CurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args) {
            HookCurrentSession();
            _ = UpdateMetadataAsync();
        }

        private void HookCurrentSession() {
            if (_sessionManager == null) return;
            try {
                var session = _sessionManager.GetCurrentSession();
                if (session != _currentSession) {
                    if (_currentSession != null) {
                        try {
                            _currentSession.MediaPropertiesChanged -= CurrentSession_MediaPropertiesChanged;
                        } catch { }
                    }
                    _currentSession = session;
                    if (_currentSession != null) {
                        try {
                            _currentSession.MediaPropertiesChanged += CurrentSession_MediaPropertiesChanged;
                        } catch { }
                    }
                }
            } catch { }
        }

        private void CurrentSession_MediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args) {
            _ = UpdateMetadataAsync();
        }

        private async Task UpdateMetadataAsync() {
            if (_sessionManager == null) {
                try {
                    _sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                    if (_sessionManager != null) {
                        HookCurrentSession();
                    }
                } catch {
                    return;
                }
            }

            try {
                var session = _sessionManager?.GetCurrentSession();
                if (session == null) return;

                var props = await session.TryGetMediaPropertiesAsync();
                if (props != null) {
                    string title = props.Title?.Trim() ?? string.Empty;
                    string artist = props.Artist?.Trim() ?? string.Empty;
                    string album = props.AlbumTitle?.Trim() ?? string.Empty;

                    bool trackChanged = !string.IsNullOrEmpty(title) && 
                        (title != CurrentMetadata.Title || artist != CurrentMetadata.Artist || album != CurrentMetadata.Album);

                    if (trackChanged) {
                        byte[]? artBytes = null;
                        string? artUrl = null;

                        // 1. Try extracting local thumbnail from Windows Media Session
                        if (props.Thumbnail != null) {
                            try {
                                using var stream = await props.Thumbnail.OpenReadAsync();
                                if (stream.Size > 0) {
                                    artBytes = new byte[stream.Size];
                                    using var reader = new global::Windows.Storage.Streams.DataReader(stream);
                                    await reader.LoadAsync((uint)stream.Size);
                                    reader.ReadBytes(artBytes);
                                }
                            } catch { }
                        }

                        // 2. Fallback to iTunes Search API if local thumbnail is absent
                        if ((artBytes == null || artBytes.Length == 0) && !string.IsNullOrEmpty(title)) {
                            try {
                                artUrl = await FetchItunesArtworkUrlAsync(artist, title);
                                if (!string.IsNullOrEmpty(artUrl)) {
                                    artBytes = await _httpClient.GetByteArrayAsync(artUrl);
                                }
                            } catch { }
                        }

                        var newMeta = new MediaMetadata {
                            Title = title,
                            Artist = artist,
                            Album = album,
                            AlbumArt = artBytes,
                            AlbumArtUrl = artUrl
                        };
                        CurrentMetadata = newMeta;
                        MetadataChanged?.Invoke(this, newMeta);
                    }
                }
            } catch { }
        }

        private static async Task<string?> FetchItunesArtworkUrlAsync(string artist, string title) {
            try {
                string query = string.IsNullOrWhiteSpace(artist) ? title : $"{artist} {title}";
                string url = $"https://itunes.apple.com/search?term={Uri.EscapeDataString(query)}&entity=song&limit=1";
                string json = await _httpClient.GetStringAsync(url);

                int idx = json.IndexOf("\"artworkUrl100\":\"", StringComparison.OrdinalIgnoreCase);
                if (idx >= 0) {
                    int start = idx + 17;
                    int end = json.IndexOf("\"", start, StringComparison.Ordinal);
                    if (end > start) {
                        string art100 = json.Substring(start, end - start);
                        return art100.Replace("100x100bb.jpg", "600x600bb.jpg")
                                     .Replace("100x100bb.png", "600x600bb.png");
                    }
                }
            } catch { }
            return null;
        }

        public void StopMonitoring() {
            lock (_lock) {
                if (_currentSession != null) {
                    try {
                        _currentSession.MediaPropertiesChanged -= CurrentSession_MediaPropertiesChanged;
                    } catch { }
                    _currentSession = null;
                }
                if (_sessionManager != null) {
                    try {
                        _sessionManager.SessionsChanged -= SessionManager_SessionsChanged;
                        _sessionManager.CurrentSessionChanged -= SessionManager_CurrentSessionChanged;
                    } catch { }
                }
                _cts?.Cancel();
                _cts = null;
            }
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
