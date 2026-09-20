using System;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using System.Runtime.InteropServices;
using Windows.Media.Control;

namespace Scrim.Metadata {
    public class WindowsMediaMetadataService : IMetadataService {
        private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(4) };
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (string? ArtUrl, TimeSpan Duration, DateTime CheckedAt)> _itunesCache = new();
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (byte[]? ArtBytes, string? ArtUrl)> _artCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly SemaphoreSlim _updateSemaphore = new(1, 1);
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
                _sessionManager = null;
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
                            _currentSession.TimelinePropertiesChanged -= CurrentSession_TimelinePropertiesChanged;
                            _currentSession.PlaybackInfoChanged -= CurrentSession_PlaybackInfoChanged;
                        } catch { }
                    }
                    _currentSession = session;
                    if (_currentSession != null) {
                        try {
                            _currentSession.MediaPropertiesChanged += CurrentSession_MediaPropertiesChanged;
                            _currentSession.TimelinePropertiesChanged += CurrentSession_TimelinePropertiesChanged;
                            _currentSession.PlaybackInfoChanged += CurrentSession_PlaybackInfoChanged;
                        } catch { }
                    }
                }
            } catch {
                _sessionManager = null;
            }
        }

        private void CurrentSession_MediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args) {
            _ = UpdateMetadataAsync();
        }

        private void CurrentSession_TimelinePropertiesChanged(GlobalSystemMediaTransportControlsSession sender, TimelinePropertiesChangedEventArgs args) {
            _ = UpdateMetadataAsync();
        }

        private void CurrentSession_PlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args) {
            _ = UpdateMetadataAsync();
        }

        private async Task UpdateMetadataAsync() {
            if (!await _updateSemaphore.WaitAsync(0)) {
                return;
            }

            try {
                if (_sessionManager == null) {
                    try {
                        _sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                        if (_sessionManager != null) {
                            _sessionManager.SessionsChanged += SessionManager_SessionsChanged;
                            _sessionManager.CurrentSessionChanged += SessionManager_CurrentSessionChanged;
                            HookCurrentSession();
                        }
                    } catch {
                        _sessionManager = null;
                        return;
                    }
                }

                var session = await GetBestMediaSessionAsync();
                if (session == null) return;

                bool isBrowser = session.SourceAppUserModelId != null &&
                    (session.SourceAppUserModelId.Contains("chrome", StringComparison.OrdinalIgnoreCase) ||
                     session.SourceAppUserModelId.Contains("msedge", StringComparison.OrdinalIgnoreCase) ||
                     session.SourceAppUserModelId.Contains("firefox", StringComparison.OrdinalIgnoreCase) ||
                     session.SourceAppUserModelId.Contains("brave", StringComparison.OrdinalIgnoreCase));

                var props = await session.TryGetMediaPropertiesAsync();
                if (props != null) {
                    string title = props.Title?.Trim() ?? string.Empty;
                    string artist = props.Artist?.Trim() ?? string.Empty;
                    string album = props.AlbumTitle?.Trim() ?? string.Empty;

                    // Never overwrite a playing track's info or artwork with a browser playing Scrim's own stream
                    if (IsStreamArtifactOrEmpty(title, artist)) {
                        if (!string.IsNullOrEmpty(CurrentMetadata.Title) && CurrentMetadata.Title != "Awaiting Audio Source...") {
                            return;
                        }
                    }

                    TimeSpan duration = TimeSpan.Zero;
                    TimeSpan position = TimeSpan.Zero;
                    bool isPlaying = false;

                    try {
                        var playbackInfo = session.GetPlaybackInfo();
                        if (playbackInfo != null) {
                            isPlaying = playbackInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
                        }
                    } catch { }

                    try {
                        var timeline = session.GetTimelineProperties();
                        if (timeline != null) {
                            duration = timeline.EndTime;
                            position = timeline.Position;
                            if (isPlaying && timeline.LastUpdatedTime > DateTimeOffset.MinValue) {
                                var elapsed = DateTimeOffset.UtcNow - timeline.LastUpdatedTime;
                                if (elapsed > TimeSpan.Zero && elapsed < TimeSpan.FromHours(2)) {
                                    position += elapsed;
                                    if (duration > TimeSpan.Zero && position > duration) {
                                        position = duration;
                                    }
                                }
                            }
                        }
                    } catch { }

                    bool trackChanged = !string.IsNullOrEmpty(title) && 
                        !IsStreamArtifactOrEmpty(title, artist) &&
                        (!string.Equals(title, CurrentMetadata.Title, StringComparison.OrdinalIgnoreCase) ||
                         !string.Equals(artist, CurrentMetadata.Artist, StringComparison.OrdinalIgnoreCase));

                    // Retain known duration if session temporarily drops timeline properties (common in Spotify/browsers)
                    if (!trackChanged && duration <= TimeSpan.Zero && CurrentMetadata.Duration > TimeSpan.Zero) {
                        duration = CurrentMetadata.Duration;
                    }

                    if (!trackChanged && isPlaying && CurrentMetadata.Position > TimeSpan.Zero) {
                        var diff = (position - CurrentMetadata.Position).TotalSeconds;
                        if (diff < 0 && Math.Abs(diff) <= 2.5) {
                            position = CurrentMetadata.Position;
                        }
                    }

                    string artCacheKey = string.IsNullOrWhiteSpace(artist) ? title.Trim() : $"{artist.Trim()} - {title.Trim()}";
                    byte[]? artBytes = CurrentMetadata.AlbumArt;
                    string? artUrl = CurrentMetadata.AlbumArtUrl;

                    bool needsArtFetch = trackChanged || ((artBytes == null || artBytes.Length == 0) && string.IsNullOrEmpty(artUrl));

                    if (trackChanged) {
                        if (_artCache.TryGetValue(artCacheKey, out var cachedArt) && (cachedArt.ArtBytes != null || !string.IsNullOrEmpty(cachedArt.ArtUrl))) {
                            artBytes = cachedArt.ArtBytes;
                            artUrl = cachedArt.ArtUrl;
                            needsArtFetch = false;
                        } else {
                            artBytes = null;
                            artUrl = null;
                        }
                    }

                    if (needsArtFetch) {
                        // 1. Try extracting local thumbnail from Windows Media Session (skip generic browser icons)
                        if (props.Thumbnail != null && !isBrowser) {
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

                        // 2. Fallback to iTunes Search API if local thumbnail is absent or from a browser
                        if ((artBytes == null || artBytes.Length == 0) && !string.IsNullOrEmpty(title)) {
                            try {
                                var (foundArtUrl, itunesDuration) = await FetchItunesInfoAsync(artist, title);
                                if (!string.IsNullOrEmpty(foundArtUrl)) {
                                    artUrl = foundArtUrl;
                                    artBytes = await _httpClient.GetByteArrayAsync(artUrl);
                                }
                                if (duration <= TimeSpan.Zero && itunesDuration > TimeSpan.Zero) {
                                    duration = itunesDuration;
                                }
                            } catch { }
                        }

                        if (artBytes != null || !string.IsNullOrEmpty(artUrl)) {
                            _artCache[artCacheKey] = (artBytes, artUrl);
                        }
                    }

                    // Preserve duration if previously resolved
                    if (duration <= TimeSpan.Zero && !trackChanged && CurrentMetadata.Duration > TimeSpan.Zero) {
                        duration = CurrentMetadata.Duration;
                    }

                    byte[]? finalArt = artBytes ?? (!trackChanged ? CurrentMetadata.AlbumArt : null);
                    string? finalArtUrl = artUrl ?? (!trackChanged ? CurrentMetadata.AlbumArtUrl : null);

                    if (finalArt == null && string.IsNullOrEmpty(finalArtUrl) && _artCache.TryGetValue(artCacheKey, out var fallbackCached)) {
                        finalArt = fallbackCached.ArtBytes;
                        finalArtUrl = fallbackCached.ArtUrl;
                    }

                    bool artRestored = (finalArt != null && CurrentMetadata.AlbumArt == null) || 
                                       (!string.IsNullOrEmpty(finalArtUrl) && string.IsNullOrEmpty(CurrentMetadata.AlbumArtUrl));
                    bool timelineChanged = Math.Abs((position - CurrentMetadata.Position).TotalSeconds) >= 0.8 || 
                        duration != CurrentMetadata.Duration || 
                        isPlaying != CurrentMetadata.IsPlaying;

                    if (trackChanged || timelineChanged || artRestored) {
                        var newMeta = new MediaMetadata {
                            Title = string.IsNullOrEmpty(title) ? CurrentMetadata.Title : title,
                            Artist = string.IsNullOrEmpty(artist) ? CurrentMetadata.Artist : artist,
                            Album = string.IsNullOrEmpty(album) ? CurrentMetadata.Album : album,
                            AlbumArt = finalArt,
                            AlbumArtUrl = finalArtUrl,
                            Duration = duration > TimeSpan.Zero ? duration : CurrentMetadata.Duration,
                            Position = position,
                            IsPlaying = isPlaying
                        };
                        CurrentMetadata = newMeta;
                        MetadataChanged?.Invoke(this, newMeta);
                    }
                }
            } catch { } finally {
                _updateSemaphore.Release();
            }
        }

        private static bool IsStreamArtifactOrEmpty(string? title, string? artist) {
            if (string.IsNullOrWhiteSpace(title)) return true;
            string t = title.Trim().ToLowerInvariant();
            if (t == "stream" || t == "stream.mp3" || t == "live" || t == "listen" || 
                t == "localhost" || t.StartsWith("localhost:") || t.StartsWith("127.0.0.1:") || 
                t.StartsWith("http://") || t.StartsWith("https://") ||
                t == "scrim" || t.StartsWith("scrim") || t.Contains("scrim broadcast") ||
                t.Contains("scrim •") || t.Contains("scrim -") || t.Contains("live broadcast player") ||
                t == "awaiting audio source..." || t == "awaiting track info..." || t == "unknown track") {
                return true;
            }
            return false;
        }

        private async Task<GlobalSystemMediaTransportControlsSession?> GetBestMediaSessionAsync() {
            if (_sessionManager == null) return null;

            try {
                IReadOnlyList<GlobalSystemMediaTransportControlsSession>? sessions = null;
                try {
                    sessions = _sessionManager.GetSessions();
                } catch {
                    _sessionManager = null;
                    return null;
                }

                if (sessions == null || sessions.Count == 0) {
                    GlobalSystemMediaTransportControlsSession? curr = null;
                    try {
                        curr = _sessionManager.GetCurrentSession();
                    } catch {
                        _sessionManager = null;
                        return null;
                    }

                    if (curr != null) {
                        try {
                            var cp = await curr.TryGetMediaPropertiesAsync();
                            if (cp != null && IsStreamArtifactOrEmpty(cp.Title, cp.Artist)) {
                                return null;
                            }
                        } catch { }
                    }
                    return curr;
                }

                string? targetProcessName = null;
                if (_targetProcessId != 0) {
                    try {
                        targetProcessName = System.Diagnostics.Process.GetProcessById((int)_targetProcessId).ProcessName;
                    } catch { }
                }

                GlobalSystemMediaTransportControlsSession? bestSession = null;
                int bestScore = -1;

                foreach (var s in sessions) {
                    try {
                        var props = await s.TryGetMediaPropertiesAsync();
                        if (props == null) continue;
                        string t = props.Title?.Trim() ?? "";
                        if (IsStreamArtifactOrEmpty(t, props.Artist)) continue;

                        string sourceApp = s.SourceAppUserModelId ?? "";
                        bool isTargetApp = !string.IsNullOrEmpty(targetProcessName) && 
                            sourceApp.Contains(targetProcessName, StringComparison.OrdinalIgnoreCase);

                        bool isBrowser = sourceApp.Contains("chrome", StringComparison.OrdinalIgnoreCase) ||
                                         sourceApp.Contains("msedge", StringComparison.OrdinalIgnoreCase) ||
                                         sourceApp.Contains("firefox", StringComparison.OrdinalIgnoreCase) ||
                                         sourceApp.Contains("brave", StringComparison.OrdinalIgnoreCase);

                        var pb = s.GetPlaybackInfo();
                        bool isPlaying = pb != null && pb.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
                        bool hasThumb = props.Thumbnail != null && !isBrowser;
                        bool hasArtist = !string.IsNullOrWhiteSpace(props.Artist);

                        int score = 0;
                        if (isTargetApp) score += 100; // Prioritize explicitly selected app (e.g. Spotify)
                        if (isPlaying) score += 10;
                        if (hasThumb) score += 4;
                        if (hasArtist) score += 2;
                        if (isBrowser && !isTargetApp) score -= 5; // Deprioritize background browser noise

                        if (score > bestScore) {
                            bestScore = score;
                            bestSession = s;
                        }
                    } catch { }
                }

                if (bestSession != null) {
                    return bestSession;
                }

                try {
                    var fallback = _sessionManager.GetCurrentSession();
                    if (fallback != null) {
                        try {
                            var fp = await fallback.TryGetMediaPropertiesAsync();
                            if (fp != null && IsStreamArtifactOrEmpty(fp.Title, fp.Artist)) {
                                return null;
                            }
                        } catch { }
                    }
                    return fallback;
                } catch {
                    _sessionManager = null;
                    return null;
                }
            } catch {
                _sessionManager = null;
                return null;
            }
        }

        private static async Task<(string? ArtUrl, TimeSpan Duration)> FetchItunesInfoAsync(string artist, string title) {
            try {
                string query = string.IsNullOrWhiteSpace(artist) ? title.Trim() : $"{artist.Trim()} {title.Trim()}";
                string cacheKey = query.ToLowerInvariant();

                // Check cache to avoid hammering iTunes API every second (prevents HTTP 429 rate limits)
                if (_itunesCache.TryGetValue(cacheKey, out var entry)) {
                    if (!string.IsNullOrEmpty(entry.ArtUrl) || entry.Duration > TimeSpan.Zero) {
                        return (entry.ArtUrl, entry.Duration);
                    }
                    // Negative cache: avoid retrying failed lookups for 10 minutes
                    if ((DateTime.UtcNow - entry.CheckedAt) < TimeSpan.FromMinutes(10)) {
                        return (null, TimeSpan.Zero);
                    }
                }

                string url = $"https://itunes.apple.com/search?term={Uri.EscapeDataString(query)}&entity=song&limit=1";
                string json = await _httpClient.GetStringAsync(url);

                string? art = null;
                TimeSpan dur = TimeSpan.Zero;

                int idx = json.IndexOf("\"artworkUrl100\":\"", StringComparison.OrdinalIgnoreCase);
                if (idx >= 0) {
                    int start = idx + 17;
                    int end = json.IndexOf("\"", start, StringComparison.Ordinal);
                    if (end > start) {
                        string art100 = json.Substring(start, end - start);
                        art = art100.Replace("100x100bb.jpg", "600x600bb.jpg")
                                    .Replace("100x100bb.png", "600x600bb.png");
                    }
                }

                int durIdx = json.IndexOf("\"trackTimeMillis\":", StringComparison.OrdinalIgnoreCase);
                if (durIdx >= 0) {
                    int start = durIdx + 18;
                    int end = start;
                    while (end < json.Length && char.IsDigit(json[end])) {
                        end++;
                    }
                    if (end > start && long.TryParse(json.Substring(start, end - start), out long ms)) {
                        dur = TimeSpan.FromMilliseconds(ms);
                    }
                }

                _itunesCache[cacheKey] = (art, dur, DateTime.UtcNow);
                return (art, dur);
            } catch {
                try {
                    string query = string.IsNullOrWhiteSpace(artist) ? title.Trim() : $"{artist.Trim()} {title.Trim()}";
                    _itunesCache[query.ToLowerInvariant()] = (null, TimeSpan.Zero, DateTime.UtcNow);
                } catch { }
            }
            return (null, TimeSpan.Zero);
        }

        public void StopMonitoring() {
            lock (_lock) {
                if (_currentSession != null) {
                    try {
                        _currentSession.MediaPropertiesChanged -= CurrentSession_MediaPropertiesChanged;
                        _currentSession.TimelinePropertiesChanged -= CurrentSession_TimelinePropertiesChanged;
                        _currentSession.PlaybackInfoChanged -= CurrentSession_PlaybackInfoChanged;
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

        [DllImport("user32.dll", SetLastError = true)]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, nuint dwExtraInfo);

        private const byte VK_MEDIA_NEXT_TRACK = 0xB0;
        private const byte VK_MEDIA_PREV_TRACK = 0xB1;
        private const byte VK_MEDIA_PLAY_PAUSE = 0xB3;
        private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        private static void SendMediaKey(byte vk) {
            try {
                keybd_event(vk, 0, KEYEVENTF_EXTENDEDKEY, 0);
                keybd_event(vk, 0, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, 0);
            } catch { }
        }

        public async Task<bool> TogglePlayPauseAsync() {
            try {
                if (_sessionManager == null) {
                    _sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                }
                var session = await GetBestMediaSessionAsync();
                if (session != null) {
                    bool ok = await session.TryTogglePlayPauseAsync();
                    if (ok) {
                        _ = Task.Delay(300).ContinueWith(_ => UpdateMetadataAsync());
                        return true;
                    }
                }
            } catch { }

            SendMediaKey(VK_MEDIA_PLAY_PAUSE);
            _ = Task.Delay(300).ContinueWith(_ => UpdateMetadataAsync());
            return true;
        }

        public async Task<bool> SkipNextAsync() {
            try {
                if (_sessionManager == null) {
                    _sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                }
                var session = await GetBestMediaSessionAsync();
                if (session != null) {
                    bool ok = await session.TrySkipNextAsync();
                    if (ok) {
                        _ = Task.Delay(300).ContinueWith(_ => UpdateMetadataAsync());
                        return true;
                    }
                }
            } catch { }

            SendMediaKey(VK_MEDIA_NEXT_TRACK);
            _ = Task.Delay(300).ContinueWith(_ => UpdateMetadataAsync());
            return true;
        }

        public async Task<bool> SkipPreviousAsync() {
            try {
                if (_sessionManager == null) {
                    _sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                }
                var session = await GetBestMediaSessionAsync();
                if (session != null) {
                    bool ok = await session.TrySkipPreviousAsync();
                    if (ok) {
                        _ = Task.Delay(300).ContinueWith(_ => UpdateMetadataAsync());
                        return true;
                    }
                }
            } catch { }

            SendMediaKey(VK_MEDIA_PREV_TRACK);
            _ = Task.Delay(300).ContinueWith(_ => UpdateMetadataAsync());
            return true;
        }

        public async Task<bool> SeekAsync(TimeSpan position) {
            try {
                if (_sessionManager == null) {
                    _sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                }
                var session = await GetBestMediaSessionAsync();
                if (session != null) {
                    bool ok = await session.TryChangePlaybackPositionAsync((long)position.Ticks);
                    if (ok) {
                        _ = Task.Delay(300).ContinueWith(_ => UpdateMetadataAsync());
                        return true;
                    }
                }
            } catch { }
            return false;
        }

        public void Dispose() {
            StopMonitoring();
        }
    }
}
