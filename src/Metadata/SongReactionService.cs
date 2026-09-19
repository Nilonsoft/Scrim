using System;
using System.Threading;

namespace Scrim.Metadata {
    public class ReactionCounts {
        public int ThumbsUp { get; set; }
        public int ThumbsDown { get; set; }
        public int Heart { get; set; }
    }

    public interface ISongReactionService {
        ReactionCounts CurrentCounts { get; }
        ReactionCounts AddReaction(string reactionType);
        void Reset();
        event Action<string, ReactionCounts>? ReactionReceived;
        event Action<ReactionCounts>? CountsReset;
    }

    public class SongReactionService : ISongReactionService {
        private int _thumbsUp;
        private int _thumbsDown;
        private int _heart;
        private readonly IMetadataService? _metadataService;
        private string _lastTrackKey = string.Empty;

        public event Action<string, ReactionCounts>? ReactionReceived;
        public event Action<ReactionCounts>? CountsReset;

        public ReactionCounts CurrentCounts => new ReactionCounts {
            ThumbsUp = Volatile.Read(ref _thumbsUp),
            ThumbsDown = Volatile.Read(ref _thumbsDown),
            Heart = Volatile.Read(ref _heart)
        };

        public SongReactionService(IMetadataService? metadataService = null) {
            _metadataService = metadataService;
            if (_metadataService != null) {
                _metadataService.MetadataChanged += OnMetadataChanged;
                _lastTrackKey = $"{_metadataService.CurrentMetadata.Title}-{_metadataService.CurrentMetadata.Artist}";
            }
        }

        private void OnMetadataChanged(object? sender, MediaMetadata meta) {
            string currentKey = $"{meta.Title}-{meta.Artist}";
            if (!string.IsNullOrEmpty(meta.Title) && !string.Equals(_lastTrackKey, currentKey, StringComparison.OrdinalIgnoreCase)) {
                _lastTrackKey = currentKey;
                Reset();
            }
        }

        public ReactionCounts AddReaction(string reactionType) {
            string cleanType = reactionType?.Trim().ToLowerInvariant() ?? string.Empty;
            switch (cleanType) {
                case "thumbs_up":
                case "thumbsup":
                case "like":
                    Interlocked.Increment(ref _thumbsUp);
                    cleanType = "thumbs_up";
                    break;
                case "thumbs_down":
                case "thumbsdown":
                case "dislike":
                    Interlocked.Increment(ref _thumbsDown);
                    cleanType = "thumbs_down";
                    break;
                case "heart":
                case "love":
                    Interlocked.Increment(ref _heart);
                    cleanType = "heart";
                    break;
                default:
                    return CurrentCounts;
            }

            var counts = CurrentCounts;
            ReactionReceived?.Invoke(cleanType, counts);
            return counts;
        }

        public void Reset() {
            Interlocked.Exchange(ref _thumbsUp, 0);
            Interlocked.Exchange(ref _thumbsDown, 0);
            Interlocked.Exchange(ref _heart, 0);
            var counts = CurrentCounts;
            CountsReset?.Invoke(counts);
        }
    }
}
