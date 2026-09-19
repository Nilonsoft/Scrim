using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
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

        public bool IsVbCableInstalled(out string? deviceId, out string? deviceName) {
            deviceId = null;
            deviceName = null;

            try {
                var enumerator = new MMDeviceEnumerator();
                var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

                foreach (var d in devices) {
                    string name = d.FriendlyName;
                    if (name.Contains("cable input", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("vb-audio", StringComparison.OrdinalIgnoreCase)) {
                        deviceId = d.ID;
                        deviceName = d.FriendlyName;
                        return true;
                    }
                }
            } catch (Exception ex) {
                Console.WriteLine($"[VirtualAudioDeviceService] Error checking VB-Cable: {ex.Message}");
            }

            return false;
        }

        public bool IsVirtualDeviceInstalled(out string? deviceId, out string? deviceName) {
            if (IsVbCableInstalled(out deviceId, out deviceName)) {
                return true;
            }

            deviceId = null;
            deviceName = null;

            try {
                var enumerator = new MMDeviceEnumerator();
                var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

                // Check for other known virtual/isolated render devices (e.g. Steam Streaming Speakers)
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
                string[] candidateDirs = new[] {
                    Path.Combine(baseDir, "Audio", "Drivers", "VBCABLE"),
                    Path.Combine(baseDir, "..", "..", "..", "src", "Audio", "Drivers", "VBCABLE"),
                    Path.Combine(baseDir, "..", "..", "..", "Audio", "Drivers", "VBCABLE"),
                    Path.Combine(Directory.GetCurrentDirectory(), "src", "Audio", "Drivers", "VBCABLE"),
                    Path.Combine(Directory.GetCurrentDirectory(), "Audio", "Drivers", "VBCABLE")
                };

                string? driverDir = null;
                string? installerPath = null;

                foreach (var dir in candidateDirs) {
                    string candidate = Path.Combine(dir, "VBCABLE_Setup_x64.exe");
                    if (File.Exists(candidate)) {
                        installerPath = Path.GetFullPath(candidate);
                        driverDir = Path.GetDirectoryName(installerPath)!;
                        break;
                    }
                }

                // If unextracted zip exists, extract it
                if (string.IsNullOrEmpty(installerPath)) {
                    string[] zipCandidates = new[] {
                        Path.Combine(baseDir, "Audio", "Drivers", "VBCABLE_Driver_Pack43.zip"),
                        Path.Combine(baseDir, "..", "..", "..", "src", "Audio", "Drivers", "VBCABLE_Driver_Pack43.zip"),
                        Path.Combine(Directory.GetCurrentDirectory(), "src", "Audio", "Drivers", "VBCABLE_Driver_Pack43.zip")
                    };

                    foreach (var z in zipCandidates) {
                        if (File.Exists(z)) {
                            driverDir = Path.Combine(Path.GetDirectoryName(z)!, "VBCABLE");
                            Directory.CreateDirectory(driverDir);
                            ZipFile.ExtractToDirectory(z, driverDir, true);
                            installerPath = Path.Combine(driverDir, "VBCABLE_Setup_x64.exe");
                            break;
                        }
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

        [ComImport]
        [Guid("870a63c4-aa25-42c2-94e7-94d79222be7e")]
        private class PolicyConfigClient { }

        [Guid("f8679f50-850a-41cf-9c72-430f290290c8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPolicyConfig {
            [PreserveSig] int GetMixFormat();
            [PreserveSig] int GetDeviceFormat();
            [PreserveSig] int SetDeviceFormat();
            [PreserveSig] int GetProcessingPeriod();
            [PreserveSig] int SetProcessingPeriod();
            [PreserveSig] int GetShareMode();
            [PreserveSig] int SetShareMode();
            [PreserveSig] int GetPropertyValue();
            [PreserveSig] int SetPropertyValue();
            [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string wszDeviceId, int eRole);
            [PreserveSig] int SetEndpointVisibility();
        }

        public string? GetDefaultPlaybackDeviceId() {
            try {
                var enumerator = new MMDeviceEnumerator();
                var def = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                return def?.ID;
            } catch {
                return null;
            }
        }

        public string? GetDefaultPlaybackDeviceName() {
            try {
                var enumerator = new MMDeviceEnumerator();
                var def = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                return def?.FriendlyName;
            } catch {
                return null;
            }
        }

        public bool SetDefaultPlaybackDevice(string deviceId) {
            try {
                IPolicyConfig? policy = null;
                try {
                    policy = (IPolicyConfig)new PolicyConfigClient();
                } catch {
                    var type = Type.GetTypeFromCLSID(new Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9"));
                    if (type != null) {
                        policy = (IPolicyConfig?)Activator.CreateInstance(type);
                    }
                }

                if (policy != null) {
                    policy.SetDefaultEndpoint(deviceId, 0); // eConsole
                    policy.SetDefaultEndpoint(deviceId, 1); // eMultimedia
                    return true;
                }
            } catch (Exception ex) {
                Console.WriteLine($"[VirtualAudioDeviceService] Could not set default endpoint: {ex.Message}");
            }
            return false;
        }
    }
}
