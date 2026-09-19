using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;

namespace Scrim.Metadata {
    public class ReactionCounts {
        public int ThumbsUp { get; set; }
        public int ThumbsDown { get; set; }
        public int Heart { get; set; }
    }

    public class SongReactionRecord {
        public int ThumbsUp { get; set; }
        public int ThumbsDown { get; set; }
        public int Heart { get; set; }
        public Dictionary<string, string> UserVotes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public interface ISongReactionService {
        string CurrentSongKey { get; }
        ReactionCounts CurrentCounts { get; }
        ReactionCounts AddReaction(string reactionType);
        ReactionCounts AddOrSwitchReaction(string userId, string reactionType, out string? activeReaction);
        string? GetUserReaction(string userId);
        void Reset();
        void ResetSong(string songKey);
        event Action<string, ReactionCounts>? ReactionReceived;
        event Action<ReactionCounts>? CountsReset;
    }

    public class SongReactionService : ISongReactionService {
        private readonly object _lock = new();
        private readonly string _storagePath;
        private readonly ConcurrentDictionary<string, SongReactionRecord> _records = new(StringComparer.OrdinalIgnoreCase);
        private readonly IMetadataService? _metadataService;
        private string _currentSongKey = "Default";

        public event Action<string, ReactionCounts>? ReactionReceived;
        public event Action<ReactionCounts>? CountsReset;

        public string CurrentSongKey => _currentSongKey;

        public ReactionCounts CurrentCounts {
            get {
                lock (_lock) {
                    var rec = GetOrCreateRecord(_currentSongKey);
                    return new ReactionCounts {
                        ThumbsUp = rec.ThumbsUp,
                        ThumbsDown = rec.ThumbsDown,
                        Heart = rec.Heart
                    };
                }
            }
        }

        public SongReactionService(IMetadataService? metadataService = null, string? storagePath = null) {
            _metadataService = metadataService;
            if (!string.IsNullOrEmpty(storagePath)) {
                _storagePath = storagePath;
            } else {
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string scrimDir = Path.Combine(userProfile, ".scrim");
                if (!Directory.Exists(scrimDir)) {
                    try {
                        Directory.CreateDirectory(scrimDir);
                    } catch { }
                }
                _storagePath = Path.Combine(scrimDir, "song_reactions.json");
            }

            LoadFromDisk();

            if (_metadataService != null) {
                _metadataService.MetadataChanged += OnMetadataChanged;
                string initKey = NormalizeSongKey(_metadataService.CurrentMetadata.Title, _metadataService.CurrentMetadata.Artist);
                if (!string.IsNullOrEmpty(initKey)) {
                    _currentSongKey = initKey;
                }
            }
        }

        private static string NormalizeSongKey(string? title, string? artist) {
            string t = title?.Trim() ?? "";
            string a = artist?.Trim() ?? "";
            if (string.IsNullOrEmpty(t) && string.IsNullOrEmpty(a)) {
                return "Default";
            }
            if (t.StartsWith("Awaiting", StringComparison.OrdinalIgnoreCase)) {
                return "Default";
            }
            if (string.IsNullOrEmpty(a)) {
                return t.ToLowerInvariant();
            }
            return $"{a} - {t}".ToLowerInvariant();
        }

        private SongReactionRecord GetOrCreateRecord(string songKey) {
            string key = string.IsNullOrWhiteSpace(songKey) ? "Default" : songKey;
            return _records.GetOrAdd(key, _ => new SongReactionRecord());
        }

        private void OnMetadataChanged(object? sender, MediaMetadata meta) {
            string newKey = NormalizeSongKey(meta.Title, meta.Artist);
            bool changed = false;
            lock (_lock) {
                if (!string.Equals(_currentSongKey, newKey, StringComparison.OrdinalIgnoreCase)) {
                    _currentSongKey = newKey;
                    changed = true;
                }
            }

            if (changed) {
                CountsReset?.Invoke(CurrentCounts);
            }
        }

        public ReactionCounts AddReaction(string reactionType) {
            return AddOrSwitchReaction("anon_global", reactionType, out _);
        }

        public ReactionCounts AddOrSwitchReaction(string userId, string reactionType, out string? activeReaction) {
            string cleanUser = string.IsNullOrWhiteSpace(userId) ? "anon" : userId.Trim();
            string cleanType = reactionType?.Trim().ToLowerInvariant() ?? string.Empty;

            switch (cleanType) {
                case "thumbs_up":
                case "thumbsup":
                case "like":
                    cleanType = "thumbs_up";
                    break;
                case "thumbs_down":
                case "thumbsdown":
                case "dislike":
                    cleanType = "thumbs_down";
                    break;
                case "heart":
                case "love":
                    cleanType = "heart";
                    break;
                default:
                    activeReaction = GetUserReaction(cleanUser);
                    return CurrentCounts;
            }

            ReactionCounts counts;
            lock (_lock) {
                var record = GetOrCreateRecord(_currentSongKey);
                record.UserVotes.TryGetValue(cleanUser, out string? prevVote);

                if (string.Equals(prevVote, cleanType, StringComparison.OrdinalIgnoreCase)) {
                    // Clicked same reaction: Undo vote (toggle off)
                    Decrement(record, cleanType);
                    record.UserVotes.Remove(cleanUser);
                    activeReaction = null;
                } else {
                    // Switching vote from a previous reaction
                    if (!string.IsNullOrEmpty(prevVote)) {
                        Decrement(record, prevVote);
                    }
                    // Add new vote
                    Increment(record, cleanType);
                    record.UserVotes[cleanUser] = cleanType;
                    activeReaction = cleanType;
                }

                counts = new ReactionCounts {
                    ThumbsUp = record.ThumbsUp,
                    ThumbsDown = record.ThumbsDown,
                    Heart = record.Heart
                };

                SaveToDisk();
            }

            ReactionReceived?.Invoke(cleanType, counts);
            return counts;
        }

        private static void Increment(SongReactionRecord rec, string type) {
            if (type == "thumbs_up") rec.ThumbsUp++;
            else if (type == "thumbs_down") rec.ThumbsDown++;
            else if (type == "heart") rec.Heart++;
        }

        private static void Decrement(SongReactionRecord rec, string type) {
            if (type == "thumbs_up" && rec.ThumbsUp > 0) rec.ThumbsUp--;
            else if (type == "thumbs_down" && rec.ThumbsDown > 0) rec.ThumbsDown--;
            else if (type == "heart" && rec.Heart > 0) rec.Heart--;
        }

        public string? GetUserReaction(string userId) {
            if (string.IsNullOrWhiteSpace(userId)) return null;
            lock (_lock) {
                var record = GetOrCreateRecord(_currentSongKey);
                record.UserVotes.TryGetValue(userId.Trim(), out string? vote);
                return vote;
            }
        }

        public void Reset() {
            lock (_lock) {
                var record = GetOrCreateRecord(_currentSongKey);
                record.ThumbsUp = 0;
                record.ThumbsDown = 0;
                record.Heart = 0;
                record.UserVotes.Clear();
                SaveToDisk();
            }
            CountsReset?.Invoke(CurrentCounts);
        }

        public void ResetSong(string songKey) {
            string key = NormalizeSongKey(songKey, null);
            lock (_lock) {
                if (_records.TryGetValue(key, out var record)) {
                    record.ThumbsUp = 0;
                    record.ThumbsDown = 0;
                    record.Heart = 0;
                    record.UserVotes.Clear();
                    SaveToDisk();
                }
            }
            if (string.Equals(_currentSongKey, key, StringComparison.OrdinalIgnoreCase)) {
                CountsReset?.Invoke(CurrentCounts);
            }
        }

        private void LoadFromDisk() {
            if (!File.Exists(_storagePath)) return;
            try {
                string json = File.ReadAllText(_storagePath);
                var loaded = JsonSerializer.Deserialize<Dictionary<string, SongReactionRecord>>(json);
                if (loaded != null) {
                    foreach (var kvp in loaded) {
                        _records[kvp.Key] = kvp.Value;
                    }
                }
            } catch { }
        }

        private void SaveToDisk() {
            try {
                var dict = new Dictionary<string, SongReactionRecord>(_records, StringComparer.OrdinalIgnoreCase);
                string json = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_storagePath, json);
            } catch { }
        }
    }
}
