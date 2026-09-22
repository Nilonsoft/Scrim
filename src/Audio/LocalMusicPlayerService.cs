using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using NAudio.Wave;

namespace Scrim.Audio {
    public enum LocalPlayerRepeatMode {
        Off,
        All,
        One
    }

    public class LocalPlaylistTrack {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string FilePath { get; set; } = "";
        public string Title { get; set; } = "Unknown Title";
        public string Artist { get; set; } = "Unknown Artist";
        public string Album { get; set; } = "";
        public TimeSpan Duration { get; set; } = TimeSpan.Zero;
        public string FormattedDuration => Duration.TotalHours >= 1 
            ? Duration.ToString(@"h\:mm\:ss") 
            : Duration.ToString(@"mm\:ss");
    }

    public class LocalMusicPlayerService : IDisposable {
        private readonly List<LocalPlaylistTrack> _playlist = new();
        private readonly object _lock = new();
        private readonly Random _random = new();

        private readonly Channel<byte[]> _musicChannel;
        private CancellationTokenSource? _playbackCts;
        private Task? _playbackTask;

        private WaveStream? _currentReader;
#pragma warning disable CS0618
        private WasapiOut? _localOut;
#pragma warning restore CS0618
        private BufferedWaveProvider? _localBuffer;

        public ChannelReader<byte[]> MusicStream => _musicChannel.Reader;

        public IReadOnlyList<LocalPlaylistTrack> Playlist {
            get {
                lock (_lock) {
                    return _playlist.ToList();
                }
            }
        }

        public int CurrentIndex { get; private set; } = -1;
        public LocalPlaylistTrack? CurrentTrack => (CurrentIndex >= 0 && CurrentIndex < _playlist.Count) ? _playlist[CurrentIndex] : null;

        public bool IsPlaying { get; private set; } = false;
        public bool IsPaused { get; private set; } = false;
        public TimeSpan Position { get; private set; } = TimeSpan.Zero;
        public TimeSpan Duration => CurrentTrack?.Duration ?? TimeSpan.Zero;
        public float Volume { get; set; } = 1.0f;
        public bool IsMuted { get; set; } = false;

        public bool IsShuffle { get; set; } = false;
        public LocalPlayerRepeatMode RepeatMode { get; set; } = LocalPlayerRepeatMode.Off;

        public event EventHandler? TrackChanged;
        public event EventHandler? PlaybackStateChanged;
        public event EventHandler? PositionChanged;
        public event EventHandler? PlaylistUpdated;

        public LocalMusicPlayerService() {
            _musicChannel = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions {
                SingleReader = false,
                SingleWriter = true
            });
            InitLocalMonitorOutput();
        }

        private void InitLocalMonitorOutput() {
            try {
                var waveFormat = new WaveFormat(44100, 16, 2);
                _localBuffer = new BufferedWaveProvider(waveFormat, TimeSpan.FromMilliseconds(800)) {
                    DiscardOnBufferOverflow = true
                };
#pragma warning disable CS0618
                _localOut = new WasapiOut(NAudio.CoreAudioApi.AudioClientShareMode.Shared, 50);
                _localOut.Init(_localBuffer);
                _localOut.Play();
#pragma warning restore CS0618
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[LocalMusicPlayer] Local output init warning: {ex.Message}");
            }
        }

        public void AddTrack(string filePath) {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) {
                return;
            }

            var track = ExtractTrackMetadata(filePath);
            lock (_lock) {
                _playlist.Add(track);
            }
            PlaylistUpdated?.Invoke(this, EventArgs.Empty);
        }

        public void AddTracks(IEnumerable<string> filePaths) {
            bool added = false;
            foreach (var path in filePaths) {
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path)) {
                    var track = ExtractTrackMetadata(path);
                    lock (_lock) {
                        _playlist.Add(track);
                    }
                    added = true;
                }
            }
            if (added) {
                PlaylistUpdated?.Invoke(this, EventArgs.Empty);
            }
        }

        public void AddDirectory(string directoryPath, bool recursive = false) {
            if (!Directory.Exists(directoryPath)) {
                return;
            }

            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
                ".mp3", ".wav", ".m4a", ".aac", ".flac", ".wma", ".aiff"
            };

            var files = Directory.EnumerateFiles(directoryPath, "*.*", searchOption)
                .Where(f => extensions.Contains(Path.GetExtension(f)));

            AddTracks(files);
        }

        public void RemoveTrack(LocalPlaylistTrack track) {
            lock (_lock) {
                int index = _playlist.IndexOf(track);
                if (index < 0) return;

                if (index == CurrentIndex) {
                    Stop();
                    CurrentIndex = -1;
                } else if (index < CurrentIndex) {
                    CurrentIndex--;
                }
                _playlist.RemoveAt(index);
            }
            PlaylistUpdated?.Invoke(this, EventArgs.Empty);
        }

        public void MoveTrack(int fromIndex, int toIndex) {
            lock (_lock) {
                if (fromIndex < 0 || fromIndex >= _playlist.Count || toIndex < 0 || toIndex >= _playlist.Count || fromIndex == toIndex) {
                    return;
                }

                var track = _playlist[fromIndex];
                _playlist.RemoveAt(fromIndex);
                _playlist.Insert(toIndex, track);

                if (CurrentIndex == fromIndex) {
                    CurrentIndex = toIndex;
                } else if (fromIndex < CurrentIndex && toIndex >= CurrentIndex) {
                    CurrentIndex--;
                } else if (fromIndex > CurrentIndex && toIndex <= CurrentIndex) {
                    CurrentIndex++;
                }
            }
            PlaylistUpdated?.Invoke(this, EventArgs.Empty);
        }

        public void ClearPlaylist() {
            Stop();
            lock (_lock) {
                _playlist.Clear();
                CurrentIndex = -1;
            }
            PlaylistUpdated?.Invoke(this, EventArgs.Empty);
        }

        public void PlayTrack(int index) {
            lock (_lock) {
                if (index < 0 || index >= _playlist.Count) {
                    return;
                }
                CurrentIndex = index;
            }
            StartCurrentTrack();
        }

        public void PlayTrack(LocalPlaylistTrack track) {
            int idx;
            lock (_lock) {
                idx = _playlist.IndexOf(track);
            }
            if (idx >= 0) {
                PlayTrack(idx);
            }
        }

        public void Play() {
            if (IsPaused) {
                IsPaused = false;
                IsPlaying = true;
                PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
                return;
            }

            if (CurrentIndex >= 0 && CurrentIndex < _playlist.Count) {
                StartCurrentTrack();
            } else if (_playlist.Count > 0) {
                PlayTrack(0);
            }
        }

        public void Pause() {
            if (IsPlaying) {
                IsPlaying = false;
                IsPaused = true;
                PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void TogglePlayPause() {
            if (IsPlaying) {
                Pause();
            } else {
                Play();
            }
        }

        public void Stop() {
            _playbackCts?.Cancel();
            try {
                _playbackTask?.Wait(200);
            } catch { }
            _playbackCts?.Dispose();
            _playbackCts = null;
            _playbackTask = null;

            lock (_lock) {
                _currentReader?.Dispose();
                _currentReader = null;
            }

            _localBuffer?.ClearBuffer();
            IsPlaying = false;
            IsPaused = false;
            Position = TimeSpan.Zero;
            PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
            PositionChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Next() {
            lock (_lock) {
                if (_playlist.Count == 0) return;

                if (IsShuffle) {
                    if (_playlist.Count == 1) {
                        CurrentIndex = 0;
                    } else {
                        int next;
                        do {
                            next = _random.Next(_playlist.Count);
                        } while (next == CurrentIndex && _playlist.Count > 1);
                        CurrentIndex = next;
                    }
                } else {
                    int next = CurrentIndex + 1;
                    if (next >= _playlist.Count) {
                        if (RepeatMode == LocalPlayerRepeatMode.All) {
                            next = 0;
                        } else {
                            Stop();
                            return;
                        }
                    }
                    CurrentIndex = next;
                }
            }
            StartCurrentTrack();
        }

        public void Previous() {
            // If more than 3 seconds in, restart track
            if (Position.TotalSeconds > 3.0) {
                Seek(TimeSpan.Zero);
                return;
            }

            lock (_lock) {
                if (_playlist.Count == 0) return;

                int prev = CurrentIndex - 1;
                if (prev < 0) {
                    prev = _playlist.Count - 1;
                }
                CurrentIndex = prev;
            }
            StartCurrentTrack();
        }

        public void Seek(TimeSpan target) {
            lock (_lock) {
                if (_currentReader != null) {
                    try {
                        long targetBytes = (long)(target.TotalSeconds * _currentReader.WaveFormat.AverageBytesPerSecond);
                        targetBytes = Math.Clamp(targetBytes, 0, _currentReader.Length);
                        targetBytes -= targetBytes % _currentReader.WaveFormat.BlockAlign;
                        _currentReader.Position = targetBytes;
                        Position = target;
                        _localBuffer?.ClearBuffer();
                        PositionChanged?.Invoke(this, EventArgs.Empty);
                    } catch { }
                }
            }
        }

        private void StartCurrentTrack() {
            Stop();

            LocalPlaylistTrack? track;
            lock (_lock) {
                if (CurrentIndex < 0 || CurrentIndex >= _playlist.Count) {
                    return;
                }
                track = _playlist[CurrentIndex];
            }

            if (track == null || !File.Exists(track.FilePath)) {
                Next();
                return;
            }

            try {
                WaveStream reader;
                if (track.FilePath.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)) {
                    try {
                        reader = new WaveFileReader(track.FilePath);
                    } catch {
                        reader = new MediaFoundationReader(track.FilePath);
                    }
                } else {
                    reader = new MediaFoundationReader(track.FilePath);
                }

                lock (_lock) {
                    _currentReader = reader;
                }

                _playbackCts = new CancellationTokenSource();
                var token = _playbackCts.Token;

                IsPlaying = true;
                IsPaused = false;
                Position = TimeSpan.Zero;
                TrackChanged?.Invoke(this, EventArgs.Empty);
                PlaybackStateChanged?.Invoke(this, EventArgs.Empty);

                _playbackTask = Task.Run(() => PlaybackWorker(reader, token), token);
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[LocalMusicPlayer] Failed to play {track.FilePath}: {ex.Message}");
                Next();
            }
        }

        private void PlaybackWorker(WaveStream reader, CancellationToken token) {
            // Read in chunks roughly corresponding to 20ms of audio
            int bytesPer20ms = (int)Math.Max(1024, reader.WaveFormat.AverageBytesPerSecond * 0.02);
            bytesPer20ms -= bytesPer20ms % Math.Max(1, reader.WaveFormat.BlockAlign);
            byte[] rawBuffer = new byte[bytesPer20ms];

            var sw = System.Diagnostics.Stopwatch.StartNew();
            long positionReportTicks = 0;

            try {
                while (!token.IsCancellationRequested) {
                    if (IsPaused) {
                        Thread.Sleep(20);
                        continue;
                    }

                    int bytesRead = reader.Read(rawBuffer, 0, rawBuffer.Length);
                    if (bytesRead <= 0) {
                        // EOF reached
                        break;
                    }

                    // Convert audio frame to standard 44.1kHz 16-bit stereo PCM
                    byte[] pcm16Stereo = ConvertTo16Bit44100Stereo(rawBuffer, bytesRead, reader.WaveFormat);
                    if (pcm16Stereo.Length == 0) {
                        continue;
                    }

                    // Apply local player volume
                    float effectiveVol = IsMuted ? 0.0f : Math.Clamp(Volume, 0.0f, 1.0f);
                    if (effectiveVol < 0.999f) {
                        int samples = pcm16Stereo.Length / 2;
                        for (int s = 0; s < samples; s++) {
                            int idx = s * 2;
                            short val = (short)(BitConverter.ToInt16(pcm16Stereo, idx) * effectiveVol);
                            pcm16Stereo[idx] = (byte)(val & 0xFF);
                            pcm16Stereo[idx + 1] = (byte)((val >> 8) & 0xFF);
                        }
                    }

                    // Feed to local output monitor
                    _localBuffer?.AddSamples(pcm16Stereo, 0, pcm16Stereo.Length);

                    // Feed to broadcast audio mixer channel
                    _musicChannel.Writer.TryWrite(pcm16Stereo);

                    // Update track playback position
                    Position = reader.CurrentTime;
                    if (sw.ElapsedMilliseconds - positionReportTicks > 250) {
                        positionReportTicks = sw.ElapsedMilliseconds;
                        PositionChanged?.Invoke(this, EventArgs.Empty);
                    }

                    // Pace audio delivery smoothly (~20ms per frame)
                    Thread.Sleep(19);
                }
            } catch (OperationCanceledException) {
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[LocalMusicPlayer] Playback loop exception: {ex.Message}");
            }

            if (!token.IsCancellationRequested) {
                // Auto-advance logic
                if (RepeatMode == LocalPlayerRepeatMode.One) {
                    Seek(TimeSpan.Zero);
                    StartCurrentTrack();
                } else {
                    Next();
                }
            }
        }

        private static byte[] ConvertTo16Bit44100Stereo(byte[] inBuffer, int bytesRecorded, WaveFormat inFormat) {
            if (bytesRecorded <= 0 || inBuffer == null) return Array.Empty<byte>();

            int channels = Math.Max(1, inFormat.Channels);
            int sampleRate = inFormat.SampleRate;

            // Direct pass-through if already 16-bit 44.1kHz stereo
            if (sampleRate == 44100 && inFormat.BitsPerSample == 16 && channels == 2) {
                byte[] copy = new byte[bytesRecorded];
                Buffer.BlockCopy(inBuffer, 0, copy, 0, bytesRecorded);
                return copy;
            }

            // Convert float (32-bit) or PCM (16-bit) to 44.1kHz 16-bit stereo
            bool isFloat = inFormat.Encoding == WaveFormatEncoding.IeeeFloat || inFormat.BitsPerSample == 32;
            int bytesPerFrame = channels * (isFloat ? 4 : 2);
            if (bytesPerFrame <= 0) return Array.Empty<byte>();

            int inFrames = bytesRecorded / bytesPerFrame;
            if (inFrames <= 0) return Array.Empty<byte>();

            int outFrames = (sampleRate == 44100) ? inFrames : (int)Math.Round(inFrames * 44100.0 / sampleRate);
            if (outFrames <= 0) return Array.Empty<byte>();

            byte[] outBytes = new byte[outFrames * 4];
            double ratio = (double)sampleRate / 44100.0;

            for (int j = 0; j < outFrames; j++) {
                double inIndex = j * ratio;
                int idx0 = (int)inIndex;
                double frac = inIndex - idx0;
                int idx1 = Math.Min(idx0 + 1, inFrames - 1);

                float l0, r0, l1, r1;

                if (isFloat) {
                    int offset0 = idx0 * bytesPerFrame;
                    int offset1 = idx1 * bytesPerFrame;

                    l0 = BitConverter.ToSingle(inBuffer, offset0);
                    r0 = channels >= 2 ? BitConverter.ToSingle(inBuffer, offset0 + 4) : l0;

                    l1 = BitConverter.ToSingle(inBuffer, offset1);
                    r1 = channels >= 2 ? BitConverter.ToSingle(inBuffer, offset1 + 4) : l1;
                } else {
                    int offset0 = idx0 * bytesPerFrame;
                    int offset1 = idx1 * bytesPerFrame;

                    l0 = BitConverter.ToInt16(inBuffer, offset0) / 32768.0f;
                    r0 = channels >= 2 ? BitConverter.ToInt16(inBuffer, offset0 + 2) / 32768.0f : l0;

                    l1 = BitConverter.ToInt16(inBuffer, offset1) / 32768.0f;
                    r1 = channels >= 2 ? BitConverter.ToInt16(inBuffer, offset1 + 2) / 32768.0f : l1;
                }

                float l = (float)(l0 * (1.0 - frac) + l1 * frac);
                float r = (float)(r0 * (1.0 - frac) + r1 * frac);

                short sL = (short)Math.Clamp((int)(l * 32767.0f), short.MinValue, short.MaxValue);
                short sR = (short)Math.Clamp((int)(r * 32767.0f), short.MinValue, short.MaxValue);

                byte[] bL = BitConverter.GetBytes(sL);
                byte[] bR = BitConverter.GetBytes(sR);

                int outOffset = j * 4;
                outBytes[outOffset] = bL[0];
                outBytes[outOffset + 1] = bL[1];
                outBytes[outOffset + 2] = bR[0];
                outBytes[outOffset + 3] = bR[1];
            }

            return outBytes;
        }

        public static LocalPlaylistTrack ExtractTrackMetadata(string filePath) {
            var track = new LocalPlaylistTrack {
                FilePath = filePath
            };

            // Derive fallback artist and title from filename
            string filename = Path.GetFileNameWithoutExtension(filePath);
            if (filename.Contains(" - ")) {
                var parts = filename.Split(" - ", 2);
                track.Artist = parts[0].Trim();
                track.Title = parts[1].Trim();
            } else {
                track.Title = filename.Trim();
                track.Artist = "Local Artist";
            }

            // Inspect audio file for duration and tags
            try {
                using var reader = new MediaFoundationReader(filePath);
                track.Duration = reader.TotalTime;
            } catch {
                try {
                    using var waveReader = new WaveFileReader(filePath);
                    track.Duration = waveReader.TotalTime;
                } catch {
                    track.Duration = TimeSpan.FromMinutes(3.5);
                }
            }

            // Attempt ID3 tag read for MP3 files
            if (filePath.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)) {
                try {
                    using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    byte[] id3Header = new byte[10];
                    if (fs.Read(id3Header, 0, 10) == 10 && id3Header[0] == 'I' && id3Header[1] == 'D' && id3Header[2] == '3') {
                        int tagSize = ((id3Header[6] & 0x7F) << 21) |
                                      ((id3Header[7] & 0x7F) << 14) |
                                      ((id3Header[8] & 0x7F) << 7) |
                                      (id3Header[9] & 0x7F);

                        if (tagSize > 0 && tagSize < 5 * 1024 * 1024) {
                            byte[] tagData = new byte[tagSize];
                            if (fs.Read(tagData, 0, tagSize) == tagSize) {
                                ParseId3v2Frames(tagData, track);
                            }
                        }
                    }
                } catch { }
            }

            return track;
        }

        private static void ParseId3v2Frames(byte[] data, LocalPlaylistTrack track) {
            int pos = 0;
            while (pos + 10 <= data.Length) {
                string frameId = System.Text.Encoding.ASCII.GetString(data, pos, 4);
                if (string.IsNullOrWhiteSpace(frameId) || frameId[0] == '\0') break;

                int frameSize = (data[pos + 4] << 24) | (data[pos + 5] << 16) | (data[pos + 6] << 8) | data[pos + 7];
                pos += 10;

                if (frameSize <= 0 || pos + frameSize > data.Length) break;

                if (frameSize > 1) {
                    byte encoding = data[pos];
                    string text = DecodeId3Text(data, pos + 1, frameSize - 1, encoding);

                    if (!string.IsNullOrWhiteSpace(text)) {
                        text = text.Trim().TrimEnd('\0');
                        switch (frameId) {
                            case "TIT2": // Title
                                track.Title = text;
                                break;
                            case "TPE1": // Artist
                                track.Artist = text;
                                break;
                            case "TALB": // Album
                                track.Album = text;
                                break;
                        }
                    }
                }
                pos += frameSize;
            }
        }

        private static string DecodeId3Text(byte[] data, int offset, int length, byte encoding) {
            try {
                return encoding switch {
                    1 => System.Text.Encoding.Unicode.GetString(data, offset, length),
                    2 => System.Text.Encoding.BigEndianUnicode.GetString(data, offset, length),
                    3 => System.Text.Encoding.UTF8.GetString(data, offset, length),
                    _ => System.Text.Encoding.Latin1.GetString(data, offset, length)
                };
            } catch {
                return "";
            }
        }

        public void SavePlaylist(string filePath) {
            if (string.IsNullOrWhiteSpace(filePath)) return;

            if (filePath.EndsWith(".m3u", StringComparison.OrdinalIgnoreCase) || filePath.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase)) {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("#EXTM3U");
                lock (_lock) {
                    foreach (var track in _playlist) {
                        int seconds = (int)track.Duration.TotalSeconds;
                        sb.AppendLine($"#EXTINF:{seconds},{track.Artist} - {track.Title}");
                        sb.AppendLine(track.FilePath);
                    }
                }
                File.WriteAllText(filePath, sb.ToString(), System.Text.Encoding.UTF8);
            } else {
                // Save JSON
                string json;
                lock (_lock) {
                    json = JsonSerializer.Serialize(_playlist, new JsonSerializerOptions { WriteIndented = true });
                }
                File.WriteAllText(filePath, json, System.Text.Encoding.UTF8);
            }
        }

        public void LoadPlaylist(string filePath) {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return;

            var newTracks = new List<LocalPlaylistTrack>();

            if (filePath.EndsWith(".m3u", StringComparison.OrdinalIgnoreCase) || filePath.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase)) {
                var lines = File.ReadAllLines(filePath, System.Text.Encoding.UTF8);
                string currentExtInf = "";
                string baseDir = Path.GetDirectoryName(filePath) ?? "";

                foreach (var rawLine in lines) {
                    string line = rawLine.Trim();
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    if (line.StartsWith("#EXTINF:", StringComparison.OrdinalIgnoreCase)) {
                        currentExtInf = line.Substring(8);
                    } else if (!line.StartsWith("#")) {
                        string resolvedPath = Path.IsPathRooted(line) ? line : Path.Combine(baseDir, line);
                        if (File.Exists(resolvedPath)) {
                            var track = ExtractTrackMetadata(resolvedPath);
                            if (!string.IsNullOrEmpty(currentExtInf) && currentExtInf.Contains(",")) {
                                var titlePart = currentExtInf.Substring(currentExtInf.IndexOf(',') + 1).Trim();
                                if (titlePart.Contains(" - ")) {
                                    var parts = titlePart.Split(" - ", 2);
                                    track.Artist = parts[0].Trim();
                                    track.Title = parts[1].Trim();
                                } else {
                                    track.Title = titlePart;
                                }
                            }
                            newTracks.Add(track);
                        }
                        currentExtInf = "";
                    }
                }
            } else {
                // JSON format
                try {
                    string json = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
                    var loaded = JsonSerializer.Deserialize<List<LocalPlaylistTrack>>(json);
                    if (loaded != null) {
                        foreach (var item in loaded) {
                            if (File.Exists(item.FilePath)) {
                                newTracks.Add(item);
                            }
                        }
                    }
                } catch { }
            }

            if (newTracks.Count > 0) {
                Stop();
                lock (_lock) {
                    _playlist.Clear();
                    _playlist.AddRange(newTracks);
                    CurrentIndex = -1;
                }
                PlaylistUpdated?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Dispose() {
            Stop();
            try {
#pragma warning disable CS0618
                _localOut?.Stop();
                _localOut?.Dispose();
#pragma warning restore CS0618
            } catch { }
        }
    }
}
