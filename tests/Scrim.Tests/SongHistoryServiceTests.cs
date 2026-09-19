using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Scrim.Metadata;
using Xunit;

namespace Scrim.Tests {
    public class SongHistoryServiceTests {
        [Fact]
        public void AddTrack_RecordsReverseChronological() {
            var service = new SongHistoryService();

            service.AddTrack("First Song", "Artist 1", "Album 1");
            service.AddTrack("Second Song", "Artist 2", "Album 2");

            var history = service.GetHistory(10);
            Assert.Equal(2, history.Count);
            Assert.Equal("Second Song", history[0].Title);
            Assert.Equal("First Song", history[1].Title);
        }

        [Fact]
        public void Deduplication_ConsecutiveDuplicateMetadataEvents_Ignored() {
            var mockMeta = new MockMetadataService();
            var service = new SongHistoryService(mockMeta);

            mockMeta.Trigger("Song Alpha", "Artist A");
            mockMeta.Trigger("Song Alpha", "Artist A"); // Duplicate tick
            mockMeta.Trigger("Song Beta", "Artist B");

            var history = service.GetHistory(10);
            Assert.Equal(2, history.Count);
            Assert.Equal("Song Beta", history[0].Title);
            Assert.Equal("Song Alpha", history[1].Title);
        }

        [Fact]
        public void PlaceholderTitles_AreIgnored() {
            var mockMeta = new MockMetadataService();
            var service = new SongHistoryService(mockMeta);

            mockMeta.Trigger("Awaiting Track Info...", "");
            mockMeta.Trigger("LIVE BROADCAST", "");
            mockMeta.Trigger("", "");

            var history = service.GetHistory(10);
            Assert.Empty(history);
        }

        [Fact]
        public void GetHistory_EnforcesLimitProperly() {
            var service = new SongHistoryService();

            for (int i = 1; i <= 20; i++) {
                service.AddTrack($"Track {i}", $"Artist {i}", "");
            }

            var top5 = service.GetHistory(5);
            Assert.Equal(5, top5.Count);
            Assert.Equal("Track 20", top5[0].Title);
            Assert.Equal("Track 16", top5[4].Title);

            var top10 = service.GetHistory(10);
            Assert.Equal(10, top10.Count);
        }

        [Fact]
        public void Clear_EmptiesListAndFiresEvent() {
            var service = new SongHistoryService();
            service.AddTrack("Track 1", "Artist 1", "");
            service.AddTrack("Track 2", "Artist 2", "");

            bool eventFired = false;
            service.HistoryChanged += (items) => {
                eventFired = true;
                Assert.Empty(items);
            };

            service.Clear();

            Assert.True(eventFired);
            Assert.Empty(service.GetHistory());
        }

        private class MockMetadataService : IMetadataService {
            public MediaMetadata CurrentMetadata { get; private set; } = new MediaMetadata();
            public event EventHandler<MediaMetadata>? MetadataChanged;

            public void Trigger(string title, string artist) {
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
