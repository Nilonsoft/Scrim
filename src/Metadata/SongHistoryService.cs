using System;
using System.Collections.Generic;
using System.Linq;

namespace Scrim.Metadata {
    public class SongHistoryItem {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Title { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public string Album { get; set; } = string.Empty;
        public DateTime PlayedAt { get; set; } = DateTime.UtcNow;
        public string? AlbumArtUrl { get; set; }
        public byte[]? AlbumArt { get; set; }

        public string PlayedAtFormatted => PlayedAt.ToLocalTime().ToString("h:mm tt");
    }

    public interface ISongHistoryService : IDisposable {
        IReadOnlyList<SongHistoryItem> GetHistory(int limit = 10);
        void AddTrack(string title, string artist, string album, string? albumArtUrl = null, byte[]? albumArt = null);
        void RemoveTrack(string id);
        void Clear();
        event Action<IReadOnlyList<SongHistoryItem>>? HistoryChanged;
    }

    public class SongHistoryService : ISongHistoryService {
        private readonly object _lock = new();
        private readonly List<SongHistoryItem> _history = new();
        private readonly IMetadataService? _metadataService;
        private string _lastTitle = "";
        private string _lastArtist = "";

        public event Action<IReadOnlyList<SongHistoryItem>>? HistoryChanged;

        public SongHistoryService(IMetadataService? metadataService = null) {
            _metadataService = metadataService;
            if (_metadataService != null) {
                _metadataService.MetadataChanged += OnMetadataChanged;
                var initial = _metadataService.CurrentMetadata;
                if (IsValidSong(initial.Title, initial.Artist)) {
                    AddTrack(initial.Title, initial.Artist, initial.Album, initial.AlbumArtUrl, initial.AlbumArt);
                }
            }
        }

        private void OnMetadataChanged(object? sender, MediaMetadata meta) {
            if (!IsValidSong(meta.Title, meta.Artist)) {
                return;
            }

            lock (_lock) {
                if (string.Equals(_lastTitle, meta.Title, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(_lastArtist, meta.Artist, StringComparison.OrdinalIgnoreCase)) {
                    return;
                }
            }

            AddTrack(meta.Title, meta.Artist, meta.Album, meta.AlbumArtUrl, meta.AlbumArt);
        }

        private static bool IsValidSong(string? title, string? artist) {
            if (string.IsNullOrWhiteSpace(title)) return false;
            string t = title.Trim();
            if (t.StartsWith("Awaiting", StringComparison.OrdinalIgnoreCase)) return false;
            if (string.Equals(t, "LIVE BROADCAST", StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        }

        public void AddTrack(string title, string artist, string album, string? albumArtUrl = null, byte[]? albumArt = null) {
            if (string.IsNullOrWhiteSpace(title)) return;

            IReadOnlyList<SongHistoryItem> snapshot;
            lock (_lock) {
                _lastTitle = title.Trim();
                _lastArtist = (artist ?? "").Trim();

                var item = new SongHistoryItem {
                    Title = _lastTitle,
                    Artist = _lastArtist,
                    Album = (album ?? "").Trim(),
                    PlayedAt = DateTime.UtcNow,
                    AlbumArtUrl = albumArtUrl,
                    AlbumArt = albumArt
                };

                _history.Insert(0, item);

                // Keep maximum of 100 tracks in memory per session
                if (_history.Count > 100) {
                    _history.RemoveRange(100, _history.Count - 100);
                }

                snapshot = _history.Take(50).ToList();
            }

            HistoryChanged?.Invoke(snapshot);
        }

        public IReadOnlyList<SongHistoryItem> GetHistory(int limit = 10) {
            lock (_lock) {
                int takeCount = Math.Max(1, Math.Min(limit, 50));
                return _history.Take(takeCount).ToList();
            }
        }

        public void RemoveTrack(string id) {
            if (string.IsNullOrWhiteSpace(id)) return;
            IReadOnlyList<SongHistoryItem> snapshot;
            lock (_lock) {
                _history.RemoveAll(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
                snapshot = _history.Take(50).ToList();
            }

            HistoryChanged?.Invoke(snapshot);
        }

        public void Clear() {
            lock (_lock) {
                _history.Clear();
                _lastTitle = "";
                _lastArtist = "";
            }

            HistoryChanged?.Invoke(Array.Empty<SongHistoryItem>());
        }

        public void Dispose() {
            if (_metadataService != null) {
                _metadataService.MetadataChanged -= OnMetadataChanged;
            }
        }
    }
}
