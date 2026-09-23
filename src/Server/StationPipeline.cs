using System;
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
            MetadataService = metadataService;
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

                if (IsLive) {
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
                        if (Config.SourceType == AudioSourceType.ProcessLoopback && Config.TargetProcessId != 0) {
                            Capture.StartCapture((uint)Config.TargetProcessId, null);
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

        public void StopBroadcast() {
            lock (_stateLock) {
                if (IsPrimary) {
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
            try {
                HistoryService.Dispose();
            } catch { }
        }
    }
}
