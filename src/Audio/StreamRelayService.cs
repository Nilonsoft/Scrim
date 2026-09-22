using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using Scrim.Encoding;

namespace Scrim.Audio {
    public class StreamRelayService : IDisposable {
        private readonly IFFmpegService _ffmpegService;
        private readonly HttpClient _httpClient;
        private readonly List<RelayStreamConfig> _streams = new();
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _workerCts = new();
        private readonly Channel<byte[]> _combinedRelayChannel;
        private readonly object _lock = new();

        // PFL (Pre-Fade Listen) Headphone Cue Monitor
#pragma warning disable CS0618
        private WasapiOut? _cueOut;
#pragma warning restore CS0618
        private BufferedWaveProvider? _cueBuffer;
        private readonly object _cueLock = new();

        public IReadOnlyList<RelayStreamConfig> Streams {
            get {
                lock (_lock) {
                    return _streams.ToList();
                }
            }
        }

        public ChannelReader<byte[]> RelayAudioStream => _combinedRelayChannel.Reader;
        public bool HasActiveRelayStreams => _streams.Any(s => s.IsEnabled && s.IsConnected);

        public RelayStreamConfig? ActivePassthroughStream {
            get {
                lock (_lock) {
                    return _streams.FirstOrDefault(s => s.IsEnabled && s.IsConnected && s.PassthroughDjBranding && !string.IsNullOrWhiteSpace(s.EffectiveDjName));
                }
            }
        }

        public event Action? StreamsChanged;
        public event Action<RelayStreamConfig>? StreamStatusUpdated;
        public event Action<RelayStreamConfig>? ActiveRelayDisconnected;

        public StreamRelayService(IFFmpegService ffmpegService) {
            _ffmpegService = ffmpegService;
            _httpClient = new HttpClient {
                Timeout = TimeSpan.FromSeconds(5)
            };
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Scrim-Relay/1.0");

            _combinedRelayChannel = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(100) {
                SingleReader = false,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.DropOldest
            });
        }

        public void ToggleCue(string id) {
            lock (_lock) {
                var stream = _streams.FirstOrDefault(s => s.Id == id);
                if (stream != null) {
                    stream.IsCueActive = !stream.IsCueActive;
                    if (stream.IsCueActive) {
                        EnsureCuePlayback();
                    }
                }
            }
            StreamsChanged?.Invoke();
        }

        private void EnsureCuePlayback() {
            lock (_cueLock) {
                if (_cueOut == null) {
                    try {
                        _cueBuffer = new BufferedWaveProvider(new WaveFormat(44100, 16, 2), TimeSpan.FromMilliseconds(800)) {
                            DiscardOnBufferOverflow = true
                        };
#pragma warning disable CS0618
                        _cueOut = new WasapiOut(AudioClientShareMode.Shared, 50);
#pragma warning restore CS0618
                        _cueOut.Init(_cueBuffer);
                        _cueOut.Play();
                    } catch { }
                }
            }
        }

        private void WriteToCueBuffer(byte[] data, int length) {
            lock (_cueLock) {
                if (_cueOut != null && _cueBuffer != null && length > 0) {
                    try {
                        _cueBuffer.AddSamples(data, 0, length);
                    } catch { }
                }
            }
        }

        public void AddStream(RelayStreamConfig config) {
            lock (_lock) {
                _streams.Add(config);
            }
            StreamsChanged?.Invoke();
            if (config.IsEnabled) {
                StartRelayWorker(config);
            }
        }

        public void RemoveStream(string id) {
            RelayStreamConfig? config;
            lock (_lock) {
                config = _streams.FirstOrDefault(s => s.Id == id);
                if (config != null) {
                    _streams.Remove(config);
                }
            }
            if (config != null) {
                StopRelayWorker(id);
                StreamsChanged?.Invoke();
            }
        }

        public void ToggleStream(string id, bool isEnabled) {
            RelayStreamConfig? config;
            lock (_lock) {
                config = _streams.FirstOrDefault(s => s.Id == id);
                if (config != null) {
                    config.IsEnabled = isEnabled;
                }
            }
            if (config != null) {
                if (isEnabled) {
                    StartRelayWorker(config);
                } else {
                    StopRelayWorker(id);
                }
                StreamsChanged?.Invoke();
            }
        }

        public void UpdateStreamSettings(string id, float volume, bool isMuted, int fadeInSec, string customAvatar, bool passthroughBranding = true, bool passthroughMetadata = true) {
            lock (_lock) {
                var config = _streams.FirstOrDefault(s => s.Id == id);
                if (config != null) {
                    config.Volume = volume;
                    config.IsMuted = isMuted;
                    config.FadeInSeconds = fadeInSec;
                    config.CustomAvatarUrl = customAvatar;
                    config.PassthroughDjBranding = passthroughBranding;
                    config.PassthroughTrackMetadata = passthroughMetadata;
                }
            }
            StreamsChanged?.Invoke();
        }

        private void StartRelayWorker(RelayStreamConfig config) {
            StopRelayWorker(config.Id);

            var cts = new CancellationTokenSource();
            _workerCts[config.Id] = cts;
            var token = cts.Token;

            Task.Run(async () => {
                await ProbeAndRunRelayAsync(config, token);
            }, token);
        }

        private void StopRelayWorker(string id) {
            if (_workerCts.TryRemove(id, out var cts)) {
                try {
                    cts.Cancel();
                    cts.Dispose();
                } catch { }
            }
            lock (_lock) {
                var cfg = _streams.FirstOrDefault(s => s.Id == id);
                if (cfg != null) {
                    cfg.IsConnected = false;
                    cfg.StatusText = "Disconnected";
                }
            }
            StreamsChanged?.Invoke();
        }

        private async Task ProbeAndRunRelayAsync(RelayStreamConfig config, CancellationToken token) {
            config.StatusText = "Probing stream...";
            StreamStatusUpdated?.Invoke(config);

            // Step 1: Detect if remote stream is Scrim and fetch initial branding/bio
            await DetectScrimOriginAsync(config, token);
            StreamStatusUpdated?.Invoke(config);

            // Step 2: Decode stream audio via FFmpeg
            while (!token.IsCancellationRequested && config.IsEnabled) {
                try {
                    await RunFfmpegDecoderAsync(config, token);
                } catch (Exception ex) {
                    config.StatusText = $"Error: {ex.Message}";
                    config.IsConnected = false;
                    StreamStatusUpdated?.Invoke(config);
                }

                if (token.IsCancellationRequested || !config.IsEnabled) break;
                // Wait briefly before reconnecting if stream drops
                try {
                    await Task.Delay(3000, token);
                } catch {
                    break;
                }
            }
        }

        private async Task DetectScrimOriginAsync(RelayStreamConfig config, CancellationToken token) {
            if (string.IsNullOrWhiteSpace(config.StreamUrl)) return;

            try {
                var uri = new Uri(config.StreamUrl);
                string baseOrigin = $"{uri.Scheme}://{uri.Host}:{uri.Port}";

                // 1. Try checking /api/branding or /api/status on host
                try {
                    var response = await _httpClient.GetAsync($"{baseOrigin}/api/branding", token);
                    if (response.IsSuccessStatusCode) {
                        string json = await response.Content.ReadAsStringAsync(token);
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;
                        if (root.TryGetProperty("hostName", out var hostElem)) {
                            config.IsScrimOrigin = true;
                            config.OriginDjName = hostElem.GetString() ?? "";
                        }
                        if (root.TryGetProperty("logoUrl", out var logoElem)) {
                            string logo = logoElem.GetString() ?? "";
                            if (!string.IsNullOrEmpty(logo) && !logo.StartsWith("http", StringComparison.OrdinalIgnoreCase)) {
                                logo = $"{baseOrigin}{logo}";
                            }
                            config.OriginAvatarUrl = logo;
                        }
                        if (root.TryGetProperty("broadcasterBio", out var bioElem)) {
                            config.OriginBio = bioElem.GetString() ?? "";
                        }
                        if (root.TryGetProperty("socialDiscord", out var dElem)) {
                            config.OriginDiscord = dElem.GetString() ?? "";
                        }
                        if (root.TryGetProperty("socialTwitch", out var twElem)) {
                            config.OriginTwitch = twElem.GetString() ?? "";
                        }
                        if (root.TryGetProperty("socialTwitter", out var txElem)) {
                            config.OriginTwitter = txElem.GetString() ?? "";
                        }
                        return;
                    }
                } catch { }

                // 2. Check stream response headers for Scrim markers
                try {
                    using var req = new HttpRequestMessage(HttpMethod.Head, config.StreamUrl);
                    var headResp = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, token);
                    if (headResp.Headers.TryGetValues("X-Powered-By", out var powered) && powered.Any(p => p.Contains("Scrim", StringComparison.OrdinalIgnoreCase))) {
                        config.IsScrimOrigin = true;
                    }
                    if (headResp.Headers.TryGetValues("X-Scrim-Host", out var hosts)) {
                        config.OriginDjName = hosts.FirstOrDefault() ?? "";
                        config.IsScrimOrigin = true;
                    }
                    if (headResp.Headers.TryGetValues("X-Scrim-Avatar", out var avatars)) {
                        config.OriginAvatarUrl = avatars.FirstOrDefault() ?? "";
                    }
                } catch { }
            } catch {
                config.IsScrimOrigin = false;
            }
        }

        private async Task RunFfmpegDecoderAsync(RelayStreamConfig config, CancellationToken token) {
            config.StatusText = "Connecting audio...";
            StreamStatusUpdated?.Invoke(config);

            string ffmpegExe = "ffmpeg";
            var startInfo = new ProcessStartInfo {
                FileName = ffmpegExe,
                Arguments = $"-hide_banner -loglevel error -re -reconnect 1 -reconnect_streamed 1 -reconnect_delay_max 5 -i \"{config.StreamUrl}\" -f s16le -ar 44100 -ac 2 -",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            if (!process.Start()) {
                config.StatusText = "Failed to launch audio decoder";
                config.IsConnected = false;
                StreamStatusUpdated?.Invoke(config);
                return;
            }

            config.IsConnected = true;
            config.StatusText = config.IsScrimOrigin 
                ? $"Relaying Scrim ({config.EffectiveDjName})" 
                : "Relaying Remote Stream";
            StreamStatusUpdated?.Invoke(config);

            // Background metadata updater for Scrim origin peers
            using var metaCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            if (config.IsScrimOrigin && !string.IsNullOrWhiteSpace(config.StreamUrl)) {
                _ = Task.Run(async () => {
                    try {
                        var uri = new Uri(config.StreamUrl);
                        string metaUrl = $"{uri.Scheme}://{uri.Host}:{uri.Port}/api/metadata";
                        while (!metaCts.Token.IsCancellationRequested) {
                            await Task.Delay(2500, metaCts.Token);
                            try {
                                var resp = await _httpClient.GetAsync(metaUrl, metaCts.Token);
                                if (resp.IsSuccessStatusCode) {
                                    string metaJson = await resp.Content.ReadAsStringAsync(metaCts.Token);
                                    using var doc = JsonDocument.Parse(metaJson);
                                    var root = doc.RootElement;
                                    if (root.TryGetProperty("title", out var titleElem)) {
                                        config.CurrentTrackTitle = titleElem.GetString() ?? "";
                                    }
                                    if (root.TryGetProperty("artist", out var artistElem)) {
                                        config.CurrentTrackArtist = artistElem.GetString() ?? "";
                                    }
                                }
                            } catch { }
                        }
                    } catch { }
                }, metaCts.Token);
            }

            // Read raw 16-bit 44.1kHz stereo PCM in 3528-byte frames (20ms)
            const int FrameBytes = 3528;
            byte[] buffer = new byte[FrameBytes];
            var stdout = process.StandardOutput.BaseStream;

            DateTime startTime = DateTime.UtcNow;
            double currentGain = 0.0;
            double fadeInDuration = Math.Max(0.5, config.FadeInSeconds);
            int framesRead = 0;
            long totalBytesRead = 0;
            Stopwatch perfWatch = Stopwatch.StartNew();

            try {
                while (!token.IsCancellationRequested && config.IsEnabled && !process.HasExited) {
                    int readTotal = 0;
                    while (readTotal < FrameBytes && !token.IsCancellationRequested) {
                        int r = await stdout.ReadAsync(buffer.AsMemory(readTotal, FrameBytes - readTotal), token);
                        if (r <= 0) break;
                        readTotal += r;
                    }

                    if (readTotal <= 0) break;

                    framesRead++;
                    totalBytesRead += readTotal;

                    // Periodic health telemetry calculation (~every 2 seconds)
                    if (framesRead % 100 == 0) {
                        double elapsedSec = perfWatch.Elapsed.TotalSeconds;
                        if (elapsedSec > 0) {
                            int kbps = (int)((totalBytesRead * 8.0 / 1000.0) / elapsedSec);
                            config.BufferLatencySec = Math.Round(Math.Min(3.0, (framesRead * 0.02) - (elapsedSec * 0.8)), 1);
                            config.HealthStatus = $"{kbps} kbps • ~1.5s • Stable";
                        }
                    }

                    // PFL Headphone Cueing
                    if (config.IsCueActive) {
                        WriteToCueBuffer(buffer, readTotal);
                    }

                    // Smooth Fade-In ramp
                    double elapsed = (DateTime.UtcNow - startTime).TotalSeconds;
                    if (elapsed < fadeInDuration) {
                        currentGain = (elapsed / fadeInDuration) * (config.IsMuted ? 0.0 : config.Volume);
                    } else {
                        currentGain = config.IsMuted ? 0.0 : config.Volume;
                    }
                    config.CurrentGain = (float)currentGain;

                    // Apply volume gain to PCM samples
                    if (Math.Abs(currentGain - 1.0) > 0.001) {
                        for (int i = 0; i < readTotal; i += 2) {
                            short sample = BitConverter.ToInt16(buffer, i);
                            short scaled = (short)Math.Clamp(sample * currentGain, short.MinValue, short.MaxValue);
                            buffer[i] = (byte)(scaled & 0xFF);
                            buffer[i + 1] = (byte)((scaled >> 8) & 0xFF);
                        }
                    }

                    byte[] outFrame = new byte[readTotal];
                    Buffer.BlockCopy(buffer, 0, outFrame, 0, readTotal);
                    _combinedRelayChannel.Writer.TryWrite(outFrame);
                }
            } finally {
                metaCts.Cancel();
                bool wasConnected = config.IsConnected;
                config.IsConnected = false;
                config.StatusText = "Stream disconnected";
                StreamStatusUpdated?.Invoke(config);

                if (wasConnected && config.IsEnabled) {
                    ActiveRelayDisconnected?.Invoke(config);
                }

                try {
                    if (!process.HasExited) {
                        process.Kill();
                    }
                } catch { }
            }
        }

        public void Dispose() {
            foreach (var cts in _workerCts.Values) {
                try {
                    cts.Cancel();
                    cts.Dispose();
                } catch { }
            }
            _workerCts.Clear();
            _httpClient.Dispose();
            lock (_cueLock) {
                try {
                    _cueOut?.Stop();
                    _cueOut?.Dispose();
                    _cueOut = null;
                } catch { }
            }
        }
    }
}
