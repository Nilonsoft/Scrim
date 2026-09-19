using System;

namespace Scrim.Metadata {
    public class MediaMetadata {
        public string Title { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public string Album { get; set; } = string.Empty;
        public byte[]? AlbumArt { get; set; }
    }

    public interface IMetadataService : IDisposable {
        MediaMetadata CurrentMetadata { get; }
        event EventHandler<MediaMetadata>? MetadataChanged;
        void StartMonitoring(uint targetProcessId);
        void StopMonitoring();
    }
}
