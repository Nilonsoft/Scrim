using System;

namespace Scrim.Metadata {
    public class MediaMetadata {
        public string Title { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public string Album { get; set; } = string.Empty;
        public byte[]? AlbumArt { get; set; }
        public string? AlbumArtUrl { get; set; }

        public string? AlbumArtBase64 => AlbumArt != null && AlbumArt.Length > 0 
            ? $"data:image/jpeg;base64,{Convert.ToBase64String(AlbumArt)}" 
            : null;
    }

    public interface IMetadataService : IDisposable {
        MediaMetadata CurrentMetadata { get; }
        event EventHandler<MediaMetadata>? MetadataChanged;
        void StartMonitoring(uint targetProcessId);
        void StopMonitoring();
        System.Threading.Tasks.Task<bool> TogglePlayPauseAsync();
        System.Threading.Tasks.Task<bool> SkipNextAsync();
        System.Threading.Tasks.Task<bool> SkipPreviousAsync();
    }
}
