using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Scrim.Updates {
    public interface IUpdateService {
        string CurrentVersion { get; }
        Task<UpdateCheckResult> CheckForUpdatesAsync(string? currentVersionOverride = null);
        Task<string> DownloadUpdateAsync(string downloadUrl, string version, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
        void ApplyAutoUpdate(string msiPath);
        void EnsureDailyScheduledTask();
        void RemoveDailyScheduledTask();
    }

    public class UpdateService : IUpdateService {
        private const string DefaultBaseUrl = "https://www.nilonsoft.com";
        private const string SoftwareSlug = "scrim";
        private const string ScheduledTaskName = "Scrim Daily Update Check";
        private static readonly HttpClient _httpClient = new HttpClient {
            Timeout = TimeSpan.FromSeconds(15)
        };

        public string CurrentVersion {
            get {
                var ver = Assembly.GetExecutingAssembly().GetName().Version;
                return ver != null ? $"{ver.Major}.{ver.Minor}.{ver.Build}" : "1.0.4";
            }
        }

        public async Task<UpdateCheckResult> CheckForUpdatesAsync(string? currentVersionOverride = null) {
            string versionToCheck = !string.IsNullOrWhiteSpace(currentVersionOverride) ? currentVersionOverride : CurrentVersion;
            string endpoint = $"{DefaultBaseUrl}/api/updates/{SoftwareSlug}?currentVersion={Uri.EscapeDataString(versionToCheck)}&os=Windows";

            try {
                var response = await _httpClient.GetFromJsonAsync<UpdateResponse>(endpoint);
                if (response == null) {
                    return UpdateCheckResult.Failed("Server returned an empty update payload.");
                }

                // If downloadUrl is a relative path, resolve it against the base URL
                if (!string.IsNullOrWhiteSpace(response.DownloadUrl) && !Uri.IsWellFormedUriString(response.DownloadUrl, UriKind.Absolute)) {
                    response.DownloadUrl = new Uri(new Uri(DefaultBaseUrl), response.DownloadUrl).ToString();
                }

                if (response.IsUpdateAvailable) {
                    return UpdateCheckResult.Available(response);
                }

                return UpdateCheckResult.NotAvailable(response);
            } catch (Exception ex) {
                return UpdateCheckResult.Failed(ex.Message);
            }
        }

        public async Task<string> DownloadUpdateAsync(
            string downloadUrl,
            string version,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default) {

            var tempDir = Path.Combine(Path.GetTempPath(), "ScrimUpdates");
            if (!Directory.Exists(tempDir)) {
                Directory.CreateDirectory(tempDir);
            }

            string fileName = $"ScrimSetup-v{version}.msi";
            string destinationPath = Path.Combine(tempDir, fileName);

            using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

            var buffer = new byte[81920];
            long bytesReadTotal = 0;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0) {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                bytesReadTotal += bytesRead;

                if (totalBytes > 0 && progress != null) {
                    double percentage = (double)bytesReadTotal / totalBytes * 100.0;
                    progress.Report(percentage);
                }
            }

            progress?.Report(100.0);
            return destinationPath;
        }

        public void ApplyAutoUpdate(string msiPath) {
            if (!File.Exists(msiPath)) {
                throw new FileNotFoundException("Update installer not found.", msiPath);
            }

            var tempDir = Path.GetDirectoryName(msiPath) ?? Path.GetTempPath();
            var scriptPath = Path.Combine(tempDir, "apply_update.cmd");

            // Determine target executable path to restart after upgrade
            string targetExePath = Environment.ProcessPath ?? "";
            string installedExe = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs",
                "Scrim",
                "Scrim.exe");

            if (File.Exists(installedExe)) {
                targetExePath = installedExe;
            }

            // Create detached helper script to close instances, run MSI upgrade, and restart
            string scriptContent = $@"@echo off
setlocal
:: Give caller time to exit
timeout /t 1 /nobreak >nul

:: Gracefully request closure and force kill any hung Scrim instances
taskkill /IM Scrim.exe >nul 2>&1
timeout /t 1 /nobreak >nul
taskkill /F /IM Scrim.exe >nul 2>&1

:: Run MSI in passive mode (shows progress bar, no user interaction required)
msiexec.exe /i ""{msiPath}"" /passive /norestart
if %ERRORLEVEL% EQU 0 (
    start """" ""{targetExePath}""
)

:: Clean up updater script
del /f /q ""%~f0"" >nul 2>&1
";

            File.WriteAllText(scriptPath, scriptContent);

            var startInfo = new ProcessStartInfo {
                FileName = "cmd.exe",
                Arguments = $"/c \"\"{scriptPath}\"\"",
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            Process.Start(startInfo);

            // Shut down current application instance immediately
            Application.Current.Dispatcher.Invoke(() => {
                Application.Current.Shutdown();
            });
        }

        public void EnsureDailyScheduledTask() {
            try {
                string? exePath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath)) {
                    return;
                }

                // Avoid creating scheduled task when running in development bin/Debug folder
                if (exePath.Contains(@"\bin\Debug\", StringComparison.OrdinalIgnoreCase) ||
                    exePath.Contains(@"\bin\Release\", StringComparison.OrdinalIgnoreCase)) {
                    return;
                }

                var psi = new ProcessStartInfo {
                    FileName = "schtasks.exe",
                    Arguments = $"/create /tn \"{ScheduledTaskName}\" /tr \"\\\"{exePath}\\\" --check-updates-silent\" /sc daily /st 12:00 /f",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var proc = Process.Start(psi);
                proc?.WaitForExit(3000);
            } catch {
                // Silently ignore permission limitations for scheduled tasks
            }
        }

        public void RemoveDailyScheduledTask() {
            try {
                var psi = new ProcessStartInfo {
                    FileName = "schtasks.exe",
                    Arguments = $"/delete /tn \"{ScheduledTaskName}\" /f",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = Process.Start(psi);
                proc?.WaitForExit(3000);
            } catch { }
        }
    }
}
