using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Scrim.Plugins;

namespace Scrim.Plugin.MidiController {
    public class MidiSurfacePlugin : IScrimPlugin {
        public string Name => "MIDI Controller Surface";
        public string Version => "1.0.0";
        public string Author => "Scrim Studio";

        private IScrimHost? _host;
        private MidiConfig _config = new();
        private IntPtr _hMidiIn = IntPtr.Zero;
        private MidiInProc? _callback; // Prevent GC collection

        // Windows Multimedia MIDI API
        [DllImport("winmm.dll")]
        private static extern int midiInGetNumDevs();

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MIDIINCAPS {
            public ushort wMid;
            public ushort wPid;
            public uint vDriverVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szPname;
            public uint dwSupport;
        }

        [DllImport("winmm.dll", CharSet = CharSet.Auto)]
        private static extern int midiInGetDevCaps(uint uDeviceID, out MIDIINCAPS lpMidiInCaps, uint cbMidiInCaps);

        [DllImport("winmm.dll")]
        private static extern int midiInOpen(out IntPtr lphMidiIn, uint uDeviceID, MidiInProc dwCallback, IntPtr dwInstance, uint dwFlags);

        [DllImport("winmm.dll")]
        private static extern int midiInStart(IntPtr hMidiIn);

        [DllImport("winmm.dll")]
        private static extern int midiInStop(IntPtr hMidiIn);

        [DllImport("winmm.dll")]
        private static extern int midiInClose(IntPtr hMidiIn);

        private delegate void MidiInProc(IntPtr hMidiIn, uint wMsg, IntPtr dwInstance, IntPtr dwParam1, IntPtr dwParam2);
        private const uint CALLBACK_FUNCTION = 0x00030000;
        private const uint MIM_DATA = 0x3C3;

        public void Initialize(IScrimHost host) {
            _host = host;

            try {
                string? pluginDir = Path.GetDirectoryName(typeof(MidiSurfacePlugin).Assembly.Location);
                _config = MidiConfig.Load(pluginDir);

                int devCount = midiInGetNumDevs();
                if (devCount == 0) {
                    Console.WriteLine($"[{Name}] Initialized. No MIDI hardware input devices detected. Plug in a USB/MIDI controller to enable.");
                    return;
                }

                uint targetDevId = 0;
                string connectedName = "Default MIDI Device";

                // Scan for specified or first available device
                for (uint i = 0; i < (uint)devCount; i++) {
                    if (midiInGetDevCaps(i, out var caps, (uint)Marshal.SizeOf<MIDIINCAPS>()) == 0) {
                        if (!string.IsNullOrWhiteSpace(_config.DeviceName) && 
                            caps.szPname.Contains(_config.DeviceName, StringComparison.OrdinalIgnoreCase)) {
                            targetDevId = i;
                            connectedName = caps.szPname;
                            break;
                        }
                        if (i == 0) {
                            connectedName = caps.szPname;
                        }
                    }
                }

                _callback = OnMidiMessageReceived;
                int res = midiInOpen(out _hMidiIn, targetDevId, _callback, IntPtr.Zero, CALLBACK_FUNCTION);
                if (res == 0 && _hMidiIn != IntPtr.Zero) {
                    midiInStart(_hMidiIn);
                    Console.WriteLine($"[{Name}] Connected to MIDI device: \"{connectedName}\". Ready for fader and pad events.");
                } else {
                    Console.WriteLine($"[{Name}] Failed to open MIDI device (error code: {res}).");
                }
            } catch (Exception ex) {
                Console.WriteLine($"[{Name}] Initialization warning: {ex.Message}");
            }
        }

        private void OnMidiMessageReceived(IntPtr hMidiIn, uint wMsg, IntPtr dwInstance, IntPtr dwParam1, IntPtr dwParam2) {
            if (wMsg != MIM_DATA || _host == null) {
                return;
            }

            int param = dwParam1.ToInt32();
            int status = param & 0xFF;
            int data1 = (param >> 8) & 0xFF;
            int data2 = (param >> 16) & 0xFF;

            int messageType = status & 0xF0;

            // Control Change (CC)
            if (messageType == 0xB0) {
                if (data1 == _config.VolumeCc) {
                    float volume = Math.Clamp(data2 / 127.0f, 0f, 1f);
                    _host.AudioMixer.AppVolume = volume;
                }
            }
            // Note On
            else if (messageType == 0x90 && data2 > 0) {
                if (data1 == _config.MicMuteNote) {
                    _host.AudioMixer.IsToggleMuted = !_host.AudioMixer.IsToggleMuted;
                    Console.WriteLine($"[{Name}] MIDI Mic Mute toggled: {(_host.AudioMixer.IsToggleMuted ? "MUTED" : "LIVE")}");
                } else if (data1 == _config.SkipTrackNote) {
                    Task.Run(() => _host.Metadata.SkipNextAsync());
                    Console.WriteLine($"[{Name}] MIDI Skip Track triggered.");
                } else if (data1 == _config.PlayPauseNote) {
                    Task.Run(() => _host.Metadata.TogglePlayPauseAsync());
                    Console.WriteLine($"[{Name}] MIDI Play/Pause triggered.");
                }
            }
        }

        public void Shutdown() {
            if (_hMidiIn != IntPtr.Zero) {
                try {
                    midiInStop(_hMidiIn);
                    midiInClose(_hMidiIn);
                } catch { } finally {
                    _hMidiIn = IntPtr.Zero;
                    _callback = null;
                }
            }
            Console.WriteLine($"[{Name}] Shutdown complete.");
        }
    }
}
