using System;
using System.Diagnostics;
using System.IO;

namespace Scrim.Encoding {
    public interface IFFmpegService {
        bool IsInstalled { get; }
        string? ExecutablePath { get; }
        bool CheckInstallation();
        string GetWingetCommand();
        string GetManualDownloadUrl();
    }

    public class FFmpegService : IFFmpegService {
        private static readonly object _lock = new object();

        public bool IsInstalled { get; private set; }
        public string? ExecutablePath { get; private set; }

        public FFmpegService() {
            CheckInstallation();
        }

        public string GetWingetCommand() => "winget install ffmpeg";
        public string GetManualDownloadUrl() => "https://ffmpeg.org/download.html";

        public bool CheckInstallation() {
            lock (_lock) {
                // 1. Check local application base directory
                try {
                    string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");
                    if (File.Exists(localPath)) {
                        ExecutablePath = localPath;
                        IsInstalled = true;
                        return true;
                    }
                } catch { }

                // 2. Check process PATH environment variable
                if (CheckPath(Environment.GetEnvironmentVariable("PATH"))) {
                    return true;
                }

                // 3. Check machine and user registry PATH (in case installed while app is running)
                try {
                    string? userPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User);
                    if (CheckPath(userPath)) {
                        UpdateProcessPath(userPath);
                        return true;
                    }

                    string? machinePath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine);
                    if (CheckPath(machinePath)) {
                        UpdateProcessPath(machinePath);
                        return true;
                    }
                } catch { }

                // 4. Check known default package manager install paths
                string[] knownPaths = {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WinGet", "Links", "ffmpeg.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "scoop", "shims", "ffmpeg.exe"),
                    @"C:\ProgramData\chocolatey\bin\ffmpeg.exe",
                    @"C:\tools\ffmpeg\bin\ffmpeg.exe",
                    @"C:\ffmpeg\bin\ffmpeg.exe"
                };

                foreach (string candidate in knownPaths) {
                    try {
                        if (File.Exists(candidate)) {
                            ExecutablePath = candidate;
                            IsInstalled = true;
                            UpdateProcessPath(Path.GetDirectoryName(candidate));
                            return true;
                        }
                    } catch { }
                }

                // 5. Fallback: try executing "ffmpeg -version"
                try {
                    using var proc = Process.Start(new ProcessStartInfo {
                        FileName = "ffmpeg",
                        Arguments = "-version",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    });

                    if (proc != null) {
                        bool exited = proc.WaitForExit(1500);
                        if (!exited) {
                            try { proc.Kill(); } catch { }
                        }
                        if (proc.ExitCode == 0 || exited) {
                            ExecutablePath = "ffmpeg";
                            IsInstalled = true;
                            return true;
                        }
                    }
                } catch { }

                ExecutablePath = null;
                IsInstalled = false;
                return false;
            }
        }

        private bool CheckPath(string? pathEnv) {
            if (string.IsNullOrWhiteSpace(pathEnv)) return false;
            string[] paths = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
            foreach (string raw in paths) {
                try {
                    string clean = raw.Trim('"', ' ');
                    string candidate = Path.Combine(clean, "ffmpeg.exe");
                    if (File.Exists(candidate)) {
                        ExecutablePath = candidate;
                        IsInstalled = true;
                        return true;
                    }
                } catch { }
            }
            return false;
        }

        private static void UpdateProcessPath(string? newDir) {
            if (string.IsNullOrWhiteSpace(newDir)) return;
            try {
                string current = Environment.GetEnvironmentVariable("PATH") ?? "";
                if (!current.Contains(newDir, StringComparison.OrdinalIgnoreCase)) {
                    Environment.SetEnvironmentVariable("PATH", current + Path.PathSeparator + newDir);
                }
            } catch { }
        }
    }
}
