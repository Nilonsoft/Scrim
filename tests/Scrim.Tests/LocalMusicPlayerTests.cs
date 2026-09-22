using System;
using System.IO;
using System.Linq;
using Scrim.Audio;
using Xunit;

namespace Scrim.Tests {
    public class LocalMusicPlayerTests {
        [Fact]
        public void ExtractTrackMetadata_DerivesArtistAndTitleFromHyphenatedFilename() {
            string fakePath = Path.Combine(Path.GetTempPath(), "Daft Punk - Around the World.mp3");
            var track = LocalMusicPlayerService.ExtractTrackMetadata(fakePath);

            Assert.Equal("Daft Punk", track.Artist);
            Assert.Equal("Around the World", track.Title);
            Assert.Equal(fakePath, track.FilePath);
        }

        [Fact]
        public void ExtractTrackMetadata_HandlesPlainFilename() {
            string fakePath = Path.Combine(Path.GetTempPath(), "MidnightGroove.wav");
            var track = LocalMusicPlayerService.ExtractTrackMetadata(fakePath);

            Assert.Equal("MidnightGroove", track.Title);
            Assert.Equal("Local Artist", track.Artist);
        }

        [Fact]
        public void PlaylistManagement_AddRemoveAndClear_UpdatesPlaylistCorrectly() {
            using var player = new LocalMusicPlayerService();

            // Create temporary test files
            string temp1 = Path.Combine(Path.GetTempPath(), $"test_track_1_{Guid.NewGuid()}.mp3");
            string temp2 = Path.Combine(Path.GetTempPath(), $"test_track_2_{Guid.NewGuid()}.mp3");
            File.WriteAllText(temp1, "dummy");
            File.WriteAllText(temp2, "dummy");

            try {
                bool updatedFired = false;
                player.PlaylistUpdated += (s, e) => updatedFired = true;

                player.AddTrack(temp1);
                player.AddTrack(temp2);

                Assert.True(updatedFired);
                Assert.Equal(2, player.Playlist.Count);
                Assert.Equal(temp1, player.Playlist[0].FilePath);
                Assert.Equal(temp2, player.Playlist[1].FilePath);

                // Remove first track
                player.RemoveTrack(player.Playlist[0]);
                Assert.Single(player.Playlist);
                Assert.Equal(temp2, player.Playlist[0].FilePath);

                // Clear playlist
                player.ClearPlaylist();
                Assert.Empty(player.Playlist);
            } finally {
                if (File.Exists(temp1)) File.Delete(temp1);
                if (File.Exists(temp2)) File.Delete(temp2);
            }
        }

        [Fact]
        public void PlaylistManagement_MoveTrack_ReordersCorrectly() {
            using var player = new LocalMusicPlayerService();

            string temp1 = Path.Combine(Path.GetTempPath(), $"track_a_{Guid.NewGuid()}.mp3");
            string temp2 = Path.Combine(Path.GetTempPath(), $"track_b_{Guid.NewGuid()}.mp3");
            string temp3 = Path.Combine(Path.GetTempPath(), $"track_c_{Guid.NewGuid()}.mp3");
            File.WriteAllText(temp1, "dummy");
            File.WriteAllText(temp2, "dummy");
            File.WriteAllText(temp3, "dummy");

            try {
                player.AddTracks(new[] { temp1, temp2, temp3 });
                Assert.Equal(3, player.Playlist.Count);

                // Move track 0 to index 2
                player.MoveTrack(0, 2);
                Assert.Equal(temp2, player.Playlist[0].FilePath);
                Assert.Equal(temp3, player.Playlist[1].FilePath);
                Assert.Equal(temp1, player.Playlist[2].FilePath);
            } finally {
                if (File.Exists(temp1)) File.Delete(temp1);
                if (File.Exists(temp2)) File.Delete(temp2);
                if (File.Exists(temp3)) File.Delete(temp3);
            }
        }

        [Fact]
        public void M3uPlaylist_SaveAndLoad_PreservesTracksAndMetadata() {
            using var player = new LocalMusicPlayerService();

            string tempTrack1 = Path.Combine(Path.GetTempPath(), $"Artist One - Song Alpha {Guid.NewGuid()}.mp3");
            string tempTrack2 = Path.Combine(Path.GetTempPath(), $"Artist Two - Song Beta {Guid.NewGuid()}.mp3");
            string m3uPath = Path.Combine(Path.GetTempPath(), $"playlist_{Guid.NewGuid()}.m3u");

            File.WriteAllText(tempTrack1, "dummy");
            File.WriteAllText(tempTrack2, "dummy");

            try {
                player.AddTrack(tempTrack1);
                player.AddTrack(tempTrack2);

                player.SavePlaylist(m3uPath);
                Assert.True(File.Exists(m3uPath));

                // Load in new player instance
                using var player2 = new LocalMusicPlayerService();
                player2.LoadPlaylist(m3uPath);

                Assert.Equal(2, player2.Playlist.Count);
                Assert.Equal(tempTrack1, player2.Playlist[0].FilePath);
                Assert.Equal("Artist One", player2.Playlist[0].Artist);
                Assert.Equal("Song Alpha", player2.Playlist[0].Title.Split(' ')[0] + " " + player2.Playlist[0].Title.Split(' ')[1]);
                Assert.Equal(tempTrack2, player2.Playlist[1].FilePath);
            } finally {
                if (File.Exists(tempTrack1)) File.Delete(tempTrack1);
                if (File.Exists(tempTrack2)) File.Delete(tempTrack2);
                if (File.Exists(m3uPath)) File.Delete(m3uPath);
            }
        }

        [Fact]
        public void JsonPlaylist_SaveAndLoad_PreservesTracks() {
            using var player = new LocalMusicPlayerService();

            string tempTrack1 = Path.Combine(Path.GetTempPath(), $"Beatmaker - Groove {Guid.NewGuid()}.wav");
            string jsonPath = Path.Combine(Path.GetTempPath(), $"playlist_{Guid.NewGuid()}.json");

            File.WriteAllText(tempTrack1, "dummy");

            try {
                player.AddTrack(tempTrack1);
                player.SavePlaylist(jsonPath);
                Assert.True(File.Exists(jsonPath));

                using var player2 = new LocalMusicPlayerService();
                player2.LoadPlaylist(jsonPath);

                Assert.Single(player2.Playlist);
                Assert.Equal(tempTrack1, player2.Playlist[0].FilePath);
            } finally {
                if (File.Exists(tempTrack1)) File.Delete(tempTrack1);
                if (File.Exists(jsonPath)) File.Delete(jsonPath);
            }
        }

        [Fact]
        public void RepeatModeToggle_CyclesThroughOffAllOne() {
            using var player = new LocalMusicPlayerService();
            Assert.Equal(LocalPlayerRepeatMode.Off, player.RepeatMode);

            player.RepeatMode = LocalPlayerRepeatMode.All;
            Assert.Equal(LocalPlayerRepeatMode.All, player.RepeatMode);

            player.RepeatMode = LocalPlayerRepeatMode.One;
            Assert.Equal(LocalPlayerRepeatMode.One, player.RepeatMode);
        }
    }
}
