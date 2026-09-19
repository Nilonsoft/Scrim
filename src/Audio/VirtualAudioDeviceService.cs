using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;

namespace Scrim.Audio {
    public class VirtualAudioDeviceService {
        private static readonly string[] VirtualDeviceKeywords = new[] {
            "cable input",
            "vb-audio",
            "virtual audio",
            "virtual cable",
            "steam streaming speakers",
            "voicemeeter",
            "virtual line"
        };

        public bool IsVirtualDeviceInstalled(out string? deviceId, out string? deviceName) {
            deviceId = null;
            deviceName = null;

            try {
                var enumerator = new MMDeviceEnumerator();
                var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

                // Priority 1: Check for official VB-Audio Cable
                foreach (var d in devices) {
                    string name = d.FriendlyName;
                    if (name.Contains("cable input", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("vb-audio", StringComparison.OrdinalIgnoreCase)) {
                        deviceId = d.ID;
                        deviceName = d.FriendlyName;
                        return true;
                    }
                }

                // Priority 2: Check for other known virtual/isolated render devices (e.g. Steam Streaming Speakers)
                foreach (var d in devices) {
                    string name = d.FriendlyName;
                    foreach (var kw in VirtualDeviceKeywords) {
                        if (name.Contains(kw, StringComparison.OrdinalIgnoreCase)) {
                            deviceId = d.ID;
                            deviceName = d.FriendlyName;
                            return true;
                        }
                    }
                }
            } catch (Exception ex) {
                Console.WriteLine($"[VirtualAudioDeviceService] Error enumerating devices: {ex.Message}");
            }

            return false;
        }

        public async Task<(bool Success, string Message, string? DeviceId)> InstallVirtualCableAsync() {
            try {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string driverDir = Path.Combine(baseDir, "Audio", "Drivers", "VBCABLE");
                string installerPath = Path.Combine(driverDir, "VBCABLE_Setup_x64.exe");

                // Check alternative development path if not in publish directory
                if (!File.Exists(installerPath)) {
                    string devDriverDir = Path.Combine(baseDir, "..", "..", "..", "Audio", "Drivers", "VBCABLE");
                    if (File.Exists(Path.Combine(devDriverDir, "VBCABLE_Setup_x64.exe"))) {
                        installerPath = Path.GetFullPath(Path.Combine(devDriverDir, "VBCABLE_Setup_x64.exe"));
                        driverDir = Path.GetDirectoryName(installerPath)!;
                    }
                }

                // If unextracted zip exists, extract it
                if (!File.Exists(installerPath)) {
                    string zipPath = Path.Combine(baseDir, "Audio", "Drivers", "VBCABLE_Driver_Pack43.zip");
                    if (!File.Exists(zipPath)) {
                        zipPath = Path.Combine(baseDir, "..", "..", "..", "Audio", "Drivers", "VBCABLE_Driver_Pack43.zip");
                    }

                    if (File.Exists(zipPath)) {
                        Directory.CreateDirectory(driverDir);
                        ZipFile.ExtractToDirectory(zipPath, driverDir, true);
                        installerPath = Path.Combine(driverDir, "VBCABLE_Setup_x64.exe");
                    }
                }

                if (!File.Exists(installerPath)) {
                    return (false, "Driver installer package not found. Please verify application installation.", null);
                }

                // Launch installer with elevation and silent flags (-i -h)
                var psi = new ProcessStartInfo {
                    FileName = installerPath,
                    Arguments = "-i -h",
                    WorkingDirectory = driverDir,
                    UseShellExecute = true,
                    Verb = "runas"
                };

                var proc = Process.Start(psi);
                if (proc == null) {
                    return (false, "Failed to start driver installer process.", null);
                }

                await proc.WaitForExitAsync();

                // Give Windows Audio Engine a few moments to register the new endpoint
                for (int i = 0; i < 15; i++) {
                    await Task.Delay(500);
                    if (IsVirtualDeviceInstalled(out string? id, out _)) {
                        return (true, "Virtual Audio Cable installed and ready!", id);
                    }
                }

                return (true, "Installation completed. If device is not immediately visible, restart may be required by Windows.", null);
            } catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223) {
                // User cancelled the UAC elevation prompt
                return (false, "Administrator permissions were not granted for driver installation.", null);
            } catch (Exception ex) {
                return (false, $"Installation failed: {ex.Message}", null);
            }
        }

        public void OpenAppVolumeSettings() {
            try {
                Process.Start(new ProcessStartInfo {
                    FileName = "ms-settings:apps-volume",
                    UseShellExecute = true
                });
            } catch (Exception ex) {
                Console.WriteLine($"[VirtualAudioDeviceService] Failed to open Windows Settings: {ex.Message}");
            }
        }
    }
}
