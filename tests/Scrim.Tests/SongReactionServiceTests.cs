using System;
using System.IO;
using System.Threading.Tasks;
using Scrim.Metadata;
using Xunit;

namespace Scrim.Tests {
    public class SongReactionServiceTests {
        [Fact]
        public void AddReaction_ValidReactions_IncrementsCountsAndFiresEvent() {
            var tempStorage = Path.Combine(Path.GetTempPath(), $"scrim_test_{Guid.NewGuid():N}.json");
            var service = new SongReactionService(storagePath: tempStorage);
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

            var counts2 = service.AddOrSwitchReaction("user_1", "heart", out string? active1);
            Assert.Equal(1, counts2.ThumbsUp);
            Assert.Equal(1, counts2.Heart);
            Assert.Equal(0, counts2.ThumbsDown);
            Assert.Equal("heart", active1);

            try { File.Delete(tempStorage); } catch { }
        }

        [Fact]
        public void AddOrSwitchReaction_SingleUser_SwitchesVotesAccurately() {
            var tempStorage = Path.Combine(Path.GetTempPath(), $"scrim_test_{Guid.NewGuid():N}.json");
            var service = new SongReactionService(storagePath: tempStorage);

            // 1. Initial vote: thumbs_up
            var c1 = service.AddOrSwitchReaction("listener_alpha", "thumbs_up", out string? a1);
            Assert.Equal(1, c1.ThumbsUp);
            Assert.Equal(0, c1.Heart);
            Assert.Equal(0, c1.ThumbsDown);
            Assert.Equal("thumbs_up", a1);

            // 2. Switch vote to heart
            var c2 = service.AddOrSwitchReaction("listener_alpha", "heart", out string? a2);
            Assert.Equal(0, c2.ThumbsUp); // old vote decremented
            Assert.Equal(1, c2.Heart);    // new vote incremented
            Assert.Equal(0, c2.ThumbsDown);
            Assert.Equal("heart", a2);

            // 3. Switch vote to thumbs_down
            var c3 = service.AddOrSwitchReaction("listener_alpha", "thumbs_down", out string? a3);
            Assert.Equal(0, c3.ThumbsUp);
            Assert.Equal(0, c3.Heart);    // heart decremented
            Assert.Equal(1, c3.ThumbsDown);
            Assert.Equal("thumbs_down", a3);

            try { File.Delete(tempStorage); } catch { }
        }

        [Fact]
        public void AddOrSwitchReaction_Undo_SameReactionRemovesVote() {
            var tempStorage = Path.Combine(Path.GetTempPath(), $"scrim_test_{Guid.NewGuid():N}.json");
            var service = new SongReactionService(storagePath: tempStorage);

            // Vote heart
            var c1 = service.AddOrSwitchReaction("listener_beta", "heart", out string? a1);
            Assert.Equal(1, c1.Heart);
            Assert.Equal("heart", a1);

            // Click heart again to undo
            var c2 = service.AddOrSwitchReaction("listener_beta", "heart", out string? a2);
            Assert.Equal(0, c2.Heart);
            Assert.Null(a2);
            Assert.Null(service.GetUserReaction("listener_beta"));

            try { File.Delete(tempStorage); } catch { }
        }

        [Fact]
        public void PerSongPersistence_SameSongReloadsPreviousReactions() {
            var tempStorage = Path.Combine(Path.GetTempPath(), $"scrim_test_{Guid.NewGuid():N}.json");
            var mockMeta = new MockMetadataService();
            var service = new SongReactionService(mockMeta, storagePath: tempStorage);

            // Song 1 starts
            mockMeta.TriggerTrackChange("Bohemian Rhapsody", "Queen");
            service.AddOrSwitchReaction("u1", "thumbs_up", out _);
            service.AddOrSwitchReaction("u2", "heart", out _);

            Assert.Equal(1, service.CurrentCounts.ThumbsUp);
            Assert.Equal(1, service.CurrentCounts.Heart);
            Assert.Equal("thumbs_up", service.GetUserReaction("u1"));

            // Song 2 starts
            mockMeta.TriggerTrackChange("Under Pressure", "Queen & David Bowie");
            Assert.Equal(0, service.CurrentCounts.ThumbsUp);
            Assert.Equal(0, service.CurrentCounts.Heart);
            Assert.Null(service.GetUserReaction("u1"));

            // Song 1 replays!
            mockMeta.TriggerTrackChange("Bohemian Rhapsody", "Queen");
            Assert.Equal(1, service.CurrentCounts.ThumbsUp);
            Assert.Equal(1, service.CurrentCounts.Heart);
            Assert.Equal("thumbs_up", service.GetUserReaction("u1"));

            try { File.Delete(tempStorage); } catch { }
        }

        [Fact]
        public void AddReaction_InvalidReaction_IgnoresAndDoesNotIncrement() {
            var tempStorage = Path.Combine(Path.GetTempPath(), $"scrim_test_{Guid.NewGuid():N}.json");
            var service = new SongReactionService(storagePath: tempStorage);
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

            try { File.Delete(tempStorage); } catch { }
        }

        [Fact]
        public void Reset_ClearsAllCountsAndFiresCountsResetEvent() {
            var tempStorage = Path.Combine(Path.GetTempPath(), $"scrim_test_{Guid.NewGuid():N}.json");
            var service = new SongReactionService(storagePath: tempStorage);
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

            try { File.Delete(tempStorage); } catch { }
        }

        [Fact]
        public void ThreadSafety_ConcurrentReactions_AccuratelyIncrements() {
            var tempStorage = Path.Combine(Path.GetTempPath(), $"scrim_test_{Guid.NewGuid():N}.json");
            var service = new SongReactionService(storagePath: tempStorage);
            const int totalUsers = 100;

            Parallel.For(0, totalUsers, i => {
                service.AddOrSwitchReaction($"user_tu_{i}", "thumbs_up", out _);
                service.AddOrSwitchReaction($"user_h_{i}", "heart", out _);
                service.AddOrSwitchReaction($"user_td_{i}", "thumbs_down", out _);
            });

            var counts = service.CurrentCounts;
            Assert.Equal(totalUsers, counts.ThumbsUp);
            Assert.Equal(totalUsers, counts.Heart);
            Assert.Equal(totalUsers, counts.ThumbsDown);

            try { File.Delete(tempStorage); } catch { }
        }

        [Fact]
        public void PerSongReactions_GetAllAndResetSong_WorksCorrectly() {
            var tempStorage = Path.Combine(Path.GetTempPath(), $"scrim_test_{Guid.NewGuid():N}.json");
            var mockMeta = new MockMetadataService();
            var service = new SongReactionService(mockMeta, storagePath: tempStorage);

            // Song 1
            mockMeta.TriggerTrackChange("Song Alpha", "Artist A");
            service.AddOrSwitchReaction("u1", "thumbs_up", out _);
            service.AddOrSwitchReaction("u2", "heart", out _);

            // Song 2
            mockMeta.TriggerTrackChange("Song Beta", "Artist B");
            service.AddOrSwitchReaction("u3", "thumbs_down", out _);

            // Verify GetAllSongReactions
            var all = service.GetAllSongReactions();
            Assert.True(all.Count >= 2);
            var alphaReactions = service.GetSongReactions("Song Alpha", "Artist A");
            Assert.Equal(1, alphaReactions.ThumbsUp);
            Assert.Equal(1, alphaReactions.Heart);
            Assert.Equal(0, alphaReactions.ThumbsDown);

            var betaReactions = service.GetSongReactions("Song Beta", "Artist B");
            Assert.Equal(0, betaReactions.ThumbsUp);
            Assert.Equal(0, betaReactions.Heart);
            Assert.Equal(1, betaReactions.ThumbsDown);

            // Clear reactions on Song Alpha specifically
            service.ResetSong("artist a - song alpha");
            var alphaAfterReset = service.GetSongReactions("Song Alpha", "Artist A");
            Assert.Equal(0, alphaAfterReset.ThumbsUp);
            Assert.Equal(0, alphaAfterReset.Heart);

            // Song Beta should be untouched
            var betaAfterReset = service.GetSongReactions("Song Beta", "Artist B");
            Assert.Equal(1, betaAfterReset.ThumbsDown);

            try { File.Delete(tempStorage); } catch { }
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
