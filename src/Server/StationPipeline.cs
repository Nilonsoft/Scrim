using System;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Scrim.Audio;
using Scrim.Configuration;
using Scrim.Encoding;
using Scrim.Metadata;

namespace Scrim.Server {
    public class StationPipeline : IDisposable {
        private readonly object _stateLock = new();
        private CancellationTokenSource? _broadcastCts;
        private readonly StationMetadataService? _stationMetadataService;

        public StationConfig Config { get; private set; }
        public BroadcastHub Hub { get; }
        public AudioDuckingMixer Mixer { get; }
        public MultiFormatTranscoder Transcoder { get; }
        public ProcessLoopbackCapture? Capture { get; private set; }
        public SongRequestController RequestController { get; }
        public ILiveChatService ChatService { get; }
        public ISongReactionService ReactionService { get; }
        public ISongHistoryService HistoryService { get; }
        public IMetadataService? MetadataService { get; private set; }

        public bool IsPrimary { get; }
        private bool _isLive = false;
        public bool IsLive => IsPrimary ? Hub.IsBroadcasting : _isLive;
        public bool IsDjConnected { get; private set; } = false;
        public string? ConnectedDjName { get; private set; }

        public event EventHandler<bool>? BroadcastingStateChanged;
        public event Action? StationUpdated;

        public StationPipeline(StationConfig config, IMetadataService? metadataService = null, BroadcastHub? primaryHub = null) {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            IsPrimary = (primaryHub != null);
            Hub = primaryHub ?? new BroadcastHub();
            Mixer = new AudioDuckingMixer();
            Transcoder = new MultiFormatTranscoder();
            RequestController = new SongRequestController { IsEnabled = config.EnableSongRequests };
            ChatService = new LiveChatService();
            ChatService.NotifyStatusChanged(config.EnableChat);

            if (metadataService != null) {
                _stationMetadataService = new StationMetadataService(metadataService);
                MetadataService = _stationMetadataService;
            } else {
                MetadataService = null;
            }

            ReactionService = new SongReactionService(MetadataService);
            HistoryService = new SongHistoryService(MetadataService);

            Mixer.AppVolume = (float)config.AppStreamVolume / 100.0f;

            if (IsPrimary) {
                Hub.BroadcastingStateChanged += (sender, isLive) => {
                    BroadcastingStateChanged?.Invoke(this, isLive);
                };
            }
        }

        public void ApplyConfig(StationConfig newConfig) {
            lock (_stateLock) {
                Config = newConfig;
                RequestController.IsEnabled = newConfig.EnableSongRequests;
                ChatService.NotifyStatusChanged(newConfig.EnableChat);
                Mixer.AppVolume = (float)newConfig.AppStreamVolume / 100.0f;

                if (IsLive && Config.SourceType != AudioSourceType.IcecastIngest) {
                    var format = Enum.TryParse<AudioFormat>(newConfig.AudioFormat, true, out var parsed)
                        ? parsed
                        : AudioFormat.Mp3;
                    Transcoder.SetFormat(format, newConfig.Bitrate);
                }

                StationUpdated?.Invoke();
            }
        }

        public void StartBroadcast(MicrophoneCaptureService? sharedMic = null, LocalMusicPlayerService? localPlayer = null) {
            lock (_stateLock) {
                if (IsLive) return;
                if (IsPrimary) {
                    return;
                }

                if (Config.SourceType == AudioSourceType.IcecastIngest) {
                    // For external DJ ingest, broadcast is driven by incoming DJ connection
                    return;
                }

                _broadcastCts = new CancellationTokenSource();
                var token = _broadcastCts.Token;

                try {
                    ChannelReader<byte[]>? appAudioStream = null;

                    if (Config.SourceType == AudioSourceType.BuiltInPlayer) {
                        if (localPlayer != null) {
                            Mixer.LocalMusicAudioStream = localPlayer.MusicStream;
                        }
                    } else {
                        Capture = new ProcessLoopbackCapture();
                        if (Config.SourceType == AudioSourceType.ProcessLoopback) {
                            uint targetPid = (uint)Config.TargetProcessId;
                            if (targetPid == 0 && !string.IsNullOrWhiteSpace(Config.TargetProcessName)) {
                                string cleanName = Config.TargetProcessName.Replace(".exe", "").Trim();
                                var matchProc = System.Diagnostics.Process.GetProcessesByName(cleanName).FirstOrDefault();
                                if (matchProc != null) {
                                    targetPid = (uint)matchProc.Id;
                                }
                            }
                            Capture.StartCapture(targetPid, null);
                        } else if (Config.SourceType == AudioSourceType.AudioDevice && !string.IsNullOrEmpty(Config.CaptureDeviceId)) {
                            Capture.StartCapture(0, Config.CaptureDeviceId);
                        } else {
                            // System mix loopback
                            Capture.StartCapture(0, null);
                        }
                        appAudioStream = Capture.AudioStream;
                    }

                    var emptyChannel = Channel.CreateBounded<byte[]>(1);
                    emptyChannel.Writer.Complete();

                    ChannelReader<byte[]> finalAppStream = appAudioStream ?? emptyChannel.Reader;
                    ChannelReader<byte[]> finalMicStream = sharedMic?.MicrophoneStream ?? emptyChannel.Reader;

                    Mixer.StartMixing(finalAppStream, finalMicStream, token);

                    var format = Enum.TryParse<AudioFormat>(Config.AudioFormat, true, out var parsed)
                        ? parsed
                        : AudioFormat.Mp3;
                    Transcoder.SetFormat(format, Config.Bitrate);
                    Transcoder.StartTranscoding(Mixer.MixedStream);

                    Hub.StartBroadcasting(Transcoder.OutputStream);
                    _isLive = true;
                    BroadcastingStateChanged?.Invoke(this, true);
                } catch (Exception) {
                    StopBroadcast();
                    throw;
                }
            }
        }

        public void StartIngestBroadcast(ChannelReader<byte[]> incomingAudioStream, string? djName = null) {
            lock (_stateLock) {
                StopBroadcast();
                IsDjConnected = true;
                ConnectedDjName = string.IsNullOrWhiteSpace(djName) ? "Guest DJ" : djName;
                _isLive = true;

                if (!string.IsNullOrWhiteSpace(djName) && _stationMetadataService != null) {
                    _stationMetadataService.UpdateMetadata("Live Guest Set", djName);
                }

                Hub.StartBroadcasting(incomingAudioStream);
                BroadcastingStateChanged?.Invoke(this, true);
            }
        }

        public void StopIngestBroadcast() {
            lock (_stateLock) {
                if (!IsDjConnected && !_isLive) return;
                IsDjConnected = false;
                ConnectedDjName = null;
                _isLive = false;

                try {
                    Hub.StopBroadcasting();
                } catch { }

                _stationMetadataService?.ResetToFallback();
                BroadcastingStateChanged?.Invoke(this, false);
            }
        }

        public void UpdateIngestMetadata(string songTitle, string? artist = null) {
            if (_stationMetadataService != null) {
                _stationMetadataService.UpdateMetadata(songTitle, artist ?? ConnectedDjName ?? "Guest DJ");
            }
        }

        public void StopBroadcast() {
            lock (_stateLock) {
                if (IsPrimary) {
                    return;
                }
                if (IsDjConnected) {
                    StopIngestBroadcast();
                    return;
                }
                if (!_isLive && !Hub.IsBroadcasting) return;

                _isLive = false;
                try {
                    _broadcastCts?.Cancel();
                    _broadcastCts?.Dispose();
                    _broadcastCts = null;
                } catch { }

                try {
                    Hub.StopBroadcasting();
                    Transcoder.StopTranscoding();
                    Mixer.StopMixing();
                    Capture?.StopCapture();
                    Capture = null;
                } catch { }

                BroadcastingStateChanged?.Invoke(this, false);
            }
        }

        public void Dispose() {
            StopBroadcast();
            StopIngestBroadcast();
            try {
                _stationMetadataService?.Dispose();
                HistoryService.Dispose();
            } catch { }
        }
    }

    public class StationMetadataService : IMetadataService {
        private readonly IMetadataService? _fallbackService;
        public MediaMetadata CurrentMetadata { get; private set; } = new MediaMetadata();
        public event EventHandler<MediaMetadata>? MetadataChanged;
        public bool HasExplicitMetadata { get; private set; } = false;

        public StationMetadataService(IMetadataService? fallbackService = null) {
            _fallbackService = fallbackService;
            if (_fallbackService != null) {
                _fallbackService.MetadataChanged += OnFallbackMetadataChanged;
                CurrentMetadata = _fallbackService.CurrentMetadata;
            }
        }

        private void OnFallbackMetadataChanged(object? sender, MediaMetadata meta) {
            if (!HasExplicitMetadata) {
                CurrentMetadata = meta;
                MetadataChanged?.Invoke(this, meta);
            }
        }

        public void UpdateMetadata(string title, string artist, string album = "") {
            HasExplicitMetadata = true;
            var meta = new MediaMetadata {
                Title = title,
                Artist = artist,
                Album = album,
                IsPlaying = true
            };
            CurrentMetadata = meta;
            MetadataChanged?.Invoke(this, meta);
        }

        public void ResetToFallback() {
            HasExplicitMetadata = false;
            if (_fallbackService != null) {
                CurrentMetadata = _fallbackService.CurrentMetadata;
                MetadataChanged?.Invoke(this, CurrentMetadata);
            }
        }

        public void StartMonitoring(uint targetProcessId) => _fallbackService?.StartMonitoring(targetProcessId);
        public void StopMonitoring() => _fallbackService?.StopMonitoring();
        public Task<bool> TogglePlayPauseAsync() => _fallbackService?.TogglePlayPauseAsync() ?? Task.FromResult(false);
        public Task<bool> SkipNextAsync() => _fallbackService?.SkipNextAsync() ?? Task.FromResult(false);
        public Task<bool> SkipPreviousAsync() => _fallbackService?.SkipPreviousAsync() ?? Task.FromResult(false);
        public Task<bool> SeekAsync(TimeSpan position) => _fallbackService?.SeekAsync(position) ?? Task.FromResult(false);

        public void Dispose() {
            if (_fallbackService != null) {
                _fallbackService.MetadataChanged -= OnFallbackMetadataChanged;
            }
        }
    }
}
