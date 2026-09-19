using System;
using System.Threading.Tasks;
using Scrim.Metadata;
using Xunit;

namespace Scrim.Tests {
    public class SongReactionServiceTests {
        [Fact]
        public void AddReaction_ValidReactions_IncrementsCountsAndFiresEvent() {
            var service = new SongReactionService();
            string? receivedType = null;
            ReactionCounts? receivedCounts = null;

            service.ReactionReceived += (type, counts) => {
                receivedType = type;
                receivedCounts = counts;
            };

            var counts1 = service.AddReaction("thumbs_up");
            Assert.Equal(1, counts1.ThumbsUp);
            Assert.Equal(0, counts1.Heart);
            Assert.Equal(0, counts1.ThumbsDown);
            Assert.Equal("thumbs_up", receivedType);
            Assert.Equal(1, receivedCounts?.ThumbsUp);

            var counts2 = service.AddReaction("heart");
            Assert.Equal(1, counts2.ThumbsUp);
            Assert.Equal(1, counts2.Heart);
            Assert.Equal(0, counts2.ThumbsDown);
            Assert.Equal("heart", receivedType);
            Assert.Equal(1, receivedCounts?.Heart);

            var counts3 = service.AddReaction("love");
            Assert.Equal(2, counts3.Heart);
            Assert.Equal("heart", receivedType);

            var counts4 = service.AddReaction("thumbs_down");
            Assert.Equal(1, counts4.ThumbsDown);
            Assert.Equal("thumbs_down", receivedType);

            var counts5 = service.AddReaction("dislike");
            Assert.Equal(2, counts5.ThumbsDown);
            Assert.Equal("thumbs_down", receivedType);
        }

        [Fact]
        public void AddReaction_InvalidReaction_IgnoresAndDoesNotIncrement() {
            var service = new SongReactionService();
            bool eventFired = false;
            service.ReactionReceived += (_, _) => eventFired = true;

            var counts = service.AddReaction("invalid_reaction_type");
            Assert.Equal(0, counts.ThumbsUp);
            Assert.Equal(0, counts.Heart);
            Assert.Equal(0, counts.ThumbsDown);
            Assert.False(eventFired);

            var countsEmpty = service.AddReaction("");
            Assert.Equal(0, countsEmpty.ThumbsUp);
            Assert.False(eventFired);
        }

        [Fact]
        public void Reset_ClearsAllCountsAndFiresCountsResetEvent() {
            var service = new SongReactionService();
            service.AddReaction("thumbs_up");
            service.AddReaction("heart");
            service.AddReaction("thumbs_down");

            ReactionCounts? resetCounts = null;
            service.CountsReset += counts => resetCounts = counts;

            service.Reset();

            Assert.NotNull(resetCounts);
            Assert.Equal(0, service.CurrentCounts.ThumbsUp);
            Assert.Equal(0, service.CurrentCounts.Heart);
            Assert.Equal(0, service.CurrentCounts.ThumbsDown);
            Assert.Equal(0, resetCounts.ThumbsUp);
            Assert.Equal(0, resetCounts.Heart);
            Assert.Equal(0, resetCounts.ThumbsDown);
        }

        [Fact]
        public void ThreadSafety_ConcurrentReactions_AccuratelyIncrements() {
            var service = new SongReactionService();
            const int totalPerType = 1000;

            Parallel.Invoke(
                () => {
                    for (int i = 0; i < totalPerType; i++) {
                        service.AddReaction("thumbs_up");
                    }
                },
                () => {
                    for (int i = 0; i < totalPerType; i++) {
                        service.AddReaction("heart");
                    }
                },
                () => {
                    for (int i = 0; i < totalPerType; i++) {
                        service.AddReaction("thumbs_down");
                    }
                }
            );

            var counts = service.CurrentCounts;
            Assert.Equal(totalPerType, counts.ThumbsUp);
            Assert.Equal(totalPerType, counts.Heart);
            Assert.Equal(totalPerType, counts.ThumbsDown);
        }

        [Fact]
        public void MetadataChanged_NewTrack_AutomaticallyResetsCounts() {
            var mockMetaService = new MockMetadataService();
            var service = new SongReactionService(mockMetaService);

            service.AddReaction("thumbs_up");
            service.AddReaction("heart");
            Assert.Equal(1, service.CurrentCounts.ThumbsUp);
            Assert.Equal(1, service.CurrentCounts.Heart);

            // Change track
            mockMetaService.TriggerTrackChange("Brand New Song", "Cool Artist");

            Assert.Equal(0, service.CurrentCounts.ThumbsUp);
            Assert.Equal(0, service.CurrentCounts.Heart);
            Assert.Equal(0, service.CurrentCounts.ThumbsDown);
        }

        private class MockMetadataService : IMetadataService {
            public MediaMetadata CurrentMetadata { get; private set; } = new MediaMetadata { Title = "Initial Song", Artist = "Artist 1" };
            public event EventHandler<MediaMetadata>? MetadataChanged;

            public void TriggerTrackChange(string title, string artist) {
                CurrentMetadata = new MediaMetadata { Title = title, Artist = artist };
                MetadataChanged?.Invoke(this, CurrentMetadata);
            }

            public void StartMonitoring(uint targetProcessId) {}
            public void StopMonitoring() {}
            public Task<bool> TogglePlayPauseAsync() => Task.FromResult(true);
            public Task<bool> SkipNextAsync() => Task.FromResult(true);
            public Task<bool> SkipPreviousAsync() => Task.FromResult(true);
            public Task<bool> SeekAsync(TimeSpan position) => Task.FromResult(true);
            public void Dispose() {}
        }
    }
}
