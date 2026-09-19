using System;
using Scrim.Metadata;
using Xunit;

namespace Scrim.Tests {
    public class MediaMetadataTests {
        [Fact]
        public void MediaMetadata_AlbumArtBase64_FormatsCorrectDataUriWhenBytesPresent() {
            var sampleBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 };
            var meta = new MediaMetadata {
                Title = "Test Song",
                Artist = "Test Artist",
                Album = "Test Album",
                AlbumArt = sampleBytes
            };

            var base64 = meta.AlbumArtBase64;

            Assert.NotNull(base64);
            Assert.StartsWith("data:image/jpeg;base64,", base64);
            Assert.Contains(Convert.ToBase64String(sampleBytes), base64);
        }

        [Fact]
        public void MediaMetadata_AlbumArtBase64_ReturnsNullWhenBytesAreNullOrEmpty() {
            var metaNull = new MediaMetadata {
                Title = "Song Without Art",
                AlbumArt = null
            };

            var metaEmpty = new MediaMetadata {
                Title = "Song With Empty Art",
                AlbumArt = Array.Empty<byte>()
            };

            Assert.Null(metaNull.AlbumArtBase64);
            Assert.Null(metaEmpty.AlbumArtBase64);
        }

        [Fact]
        public void MediaMetadata_AlbumArtUrl_StoresAndRetrievesValue() {
            string expectedUrl = "https://is1-ssl.mzstatic.com/image/thumb/Music115/test.jpg";
            var meta = new MediaMetadata {
                Title = "Song",
                AlbumArtUrl = expectedUrl
            };

            Assert.Equal(expectedUrl, meta.AlbumArtUrl);
        }
    }
}
