using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Scrim.Metadata;

namespace Scrim.Plugins {
    public class PythonPluginManifest {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = "1.0.0";
        public string Author { get; set; } = "Unknown";
        public string Entry { get; set; } = "main.py";
        public string Description { get; set; } = string.Empty;
    }

    public class PythonPlugin : IScrimPlugin, IDisposable {
        private readonly object _processLock = new();
        private Process? _process;
        private StreamWriter? _stdinWriter;
        private CancellationTokenSource? _cts;
        private IScrimHost? _host;

        public string Id { get; }
        public string Name { get; }
        public string Version { get; }
        public string Author { get; }
        public string ScriptPath { get; }
        public string WorkingDirectory { get; }
        public string Description { get; }
        public bool IsRunning => _process != null && !_process.HasExited;

        public PythonPlugin(string scriptPath, PythonPluginManifest? manifest = null) {
            ScriptPath = Path.GetFullPath(scriptPath);
            WorkingDirectory = Path.GetDirectoryName(ScriptPath) ?? AppDomain.CurrentDomain.BaseDirectory;

            if (manifest != null) {
                Id = !string.IsNullOrWhiteSpace(manifest.Id) ? manifest.Id : Path.GetFileNameWithoutExtension(ScriptPath);
                Name = !string.IsNullOrWhiteSpace(manifest.Name) ? manifest.Name : Path.GetFileNameWithoutExtension(ScriptPath);
                Version = !string.IsNullOrWhiteSpace(manifest.Version) ? manifest.Version : "1.0.0";
                Author = !string.IsNullOrWhiteSpace(manifest.Author) ? manifest.Author : "Unknown";
                Description = manifest.Description ?? string.Empty;
            } else {
                string fileName = Path.GetFileNameWithoutExtension(ScriptPath);
                Id = fileName;
                Name = fileName;
                Version = "1.0.0";
                Author = "Community";
                Description = string.Empty;
            }
        }

        public static PythonPlugin? FromDirectory(string directoryPath) {
            if (!Directory.Exists(directoryPath)) {
                return null;
            }

            string manifestPath = Path.Combine(directoryPath, "plugin.json");
            PythonPluginManifest? manifest = null;
            if (File.Exists(manifestPath)) {
                try {
                    string json = File.ReadAllText(manifestPath);
                    manifest = JsonSerializer.Deserialize<PythonPluginManifest>(json, new JsonSerializerOptions {
                        PropertyNameCaseInsensitive = true
                    });
                } catch (Exception ex) {
                    Console.WriteLine($"[PythonPlugin] Error reading manifest in {directoryPath}: {ex.Message}");
                }
            }

            string entryScript = manifest?.Entry ?? "main.py";
            string fullScriptPath = Path.Combine(directoryPath, entryScript);

            if (!File.Exists(fullScriptPath)) {
                // Fallbacks: plugin.py or first .py file found
                string altScript = Path.Combine(directoryPath, "plugin.py");
                if (File.Exists(altScript)) {
                    fullScriptPath = altScript;
                } else {
                    var pyFiles = Directory.GetFiles(directoryPath, "*.py");
                    if (pyFiles.Length > 0) {
                        fullScriptPath = pyFiles[0];
                    } else {
                        return null;
                    }
                }
            }

            return new PythonPlugin(fullScriptPath, manifest);
        }

        public static PythonPlugin FromScript(string scriptPath) {
            return new PythonPlugin(scriptPath);
        }

        public void Initialize(IScrimHost host) {
            _host = host;

            string? pythonExe = ResolvePythonExecutable(WorkingDirectory);
            if (string.IsNullOrEmpty(pythonExe)) {
                Console.WriteLine($"[PythonPlugin] Cannot load '{Name}': Python executable not found on system.");
                return;
            }

            lock (_processLock) {
                try {
                    _cts = new CancellationTokenSource();

                    var startInfo = new ProcessStartInfo {
                        FileName = pythonExe,
                        Arguments = $"\"{ScriptPath}\"",
                        WorkingDirectory = WorkingDirectory,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    startInfo.EnvironmentVariables["PYTHONUNBUFFERED"] = "1";

                    // Append plugin dir and Scrim Plugins dir to PYTHONPATH
                    string pluginsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
                    string existingPythonPath = Environment.GetEnvironmentVariable("PYTHONPATH") ?? "";
                    startInfo.EnvironmentVariables["PYTHONPATH"] = $"{WorkingDirectory};{pluginsDir};{existingPythonPath}";

                    _process = new Process { StartInfo = startInfo };
                    _process.Start();

                    _stdinWriter = _process.StandardInput;

                    // Start background reader tasks
                    Task.Run(() => ReadStandardOutputLoop(_process.StandardOutput, _cts.Token));
                    Task.Run(() => ReadStandardErrorLoop(_process.StandardError, _cts.Token));

                    // Send initial state
                    SendInitMessage(host);

                    // Subscribe to host events
                    host.Broadcast.BroadcastingStateChanged += OnBroadcastingStateChanged;
                    host.Metadata.MetadataChanged += OnMetadataChanged;

                    Console.WriteLine($"[PythonPlugin] Started '{Name}' (PID: {_process.Id}) using {pythonExe}");
                } catch (Exception ex) {
                    Console.WriteLine($"[PythonPlugin] Error launching '{Name}': {ex.Message}");
                }
            }
        }

        public void Shutdown() {
            lock (_processLock) {
                if (_host != null) {
                    try {
                        _host.Broadcast.BroadcastingStateChanged -= OnBroadcastingStateChanged;
                        _host.Metadata.MetadataChanged -= OnMetadataChanged;
                    } catch { }
                    _host = null;
                }

                if (_process != null && !_process.HasExited) {
                    try {
                        SendJsonMessage(new { type = "shutdown" });
                        if (!_process.WaitForExit(1000)) {
                            _process.Kill(true);
                        }
                    } catch {
                        try {
                            _process.Kill(true);
                        } catch { }
                    } finally {
                        _process.Dispose();
                        _process = null;
                        _stdinWriter = null;
                    }
                }

                _cts?.Cancel();
                _cts?.Dispose();
                _cts = null;
            }
        }

        private void OnBroadcastingStateChanged(object? sender, bool isLive) {
            SendJsonMessage(new {
                type = "event",
                @event = "broadcasting_state_changed",
                data = new { is_live = isLive }
            });
        }

        private void OnMetadataChanged(object? sender, MediaMetadata metadata) {
            SendJsonMessage(new {
                type = "event",
                @event = "metadata_changed",
                data = new {
                    title = metadata.Title,
                    artist = metadata.Artist,
                    album = metadata.Album,
                    duration = (int)metadata.Duration.TotalSeconds
                }
            });
        }

        private void SendInitMessage(IScrimHost host) {
            var profile = host.ProfileManager.CurrentProfile;
            SendJsonMessage(new {
                type = "init",
                station = new {
                    station_name = profile?.StationName ?? "Scrim Station",
                    port = profile?.Port ?? 8000
                }
            });
        }

        private void SendJsonMessage(object obj) {
            lock (_processLock) {
                if (_stdinWriter == null || _process == null || _process.HasExited) {
                    return;
                }

                try {
                    string json = JsonSerializer.Serialize(obj);
                    _stdinWriter.WriteLine(json);
                    _stdinWriter.Flush();
                } catch (Exception ex) {
                    Console.WriteLine($"[PythonPlugin] Error writing to '{Name}' stdin: {ex.Message}");
                }
            }
        }

        private async Task ReadStandardOutputLoop(StreamReader reader, CancellationToken ct) {
            try {
                while (!ct.IsCancellationRequested) {
                    string? line = await reader.ReadLineAsync(ct);
                    if (line == null) {
                        break;
                    }
                    if (string.IsNullOrWhiteSpace(line)) {
                        continue;
                    }

                    try {
                        using var doc = JsonDocument.Parse(line);
                        var root = doc.RootElement;
                        string type = root.TryGetProperty("type", out var typeProp) ? typeProp.GetString() ?? "" : "";

                        if (type == "log" && root.TryGetProperty("message", out var msgProp)) {
                            Console.WriteLine($"[{Name}] {msgProp.GetString()}");
                        }
                    } catch {
                        // Plain text output fallback
                        Console.WriteLine($"[{Name}] {line}");
                    }
                }
            } catch (OperationCanceledException) { } catch (Exception ex) {
                Console.WriteLine($"[PythonPlugin] Error reading '{Name}' stdout: {ex.Message}");
            }
        }

        private async Task ReadStandardErrorLoop(StreamReader reader, CancellationToken ct) {
            try {
                while (!ct.IsCancellationRequested) {
                    string? line = await reader.ReadLineAsync(ct);
                    if (line == null) {
                        break;
                    }
                    if (!string.IsNullOrWhiteSpace(line)) {
                        Console.WriteLine($"[{Name} STDERR] {line}");
                    }
                }
            } catch (OperationCanceledException) { } catch { }
        }

        public static string? ResolvePythonExecutable(string workingDirectory) {
            // 1. Check local virtual environment within plugin directory
            string[] venvCandidates = new[] {
                Path.Combine(workingDirectory, ".venv", "Scripts", "python.exe"),
                Path.Combine(workingDirectory, "venv", "Scripts", "python.exe")
            };

            foreach (var candidate in venvCandidates) {
                if (File.Exists(candidate)) {
                    return candidate;
                }
            }

            // 2. Check system PATH for python.exe or py.exe
            string[] systemCommands = new[] { "python.exe", "python3.exe", "py.exe" };
            foreach (var cmd in systemCommands) {
                try {
                    using var proc = new Process {
                        StartInfo = new ProcessStartInfo {
                            FileName = cmd,
                            Arguments = "--version",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };
                    if (proc.Start()) {
                        proc.WaitForExit(1000);
                        if (proc.ExitCode == 0) {
                            return cmd;
                        }
                    }
                } catch { }
            }

            return null;
        }

        public void Dispose() {
            Shutdown();
            GC.SuppressFinalize(this);
        }
    }
}
