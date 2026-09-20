using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Scrim.Audio;
using Scrim.Configuration;
using Scrim.Metadata;
using Scrim.Plugins;
using Scrim.Server;
using Xunit;

namespace Scrim.Tests {
    public class PythonPluginTests {
        [Fact]
        public void PythonPlugin_FromDirectory_ParsesManifestCorrectly() {
            string tempDir = Path.Combine(Path.GetTempPath(), "scrim_test_plugin_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try {
                var manifest = new {
                    id = "test-py-plugin",
                    name = "Test Python Plugin",
                    version = "2.1.0",
                    author = "Scrim DJ",
                    entry = "run.py",
                    description = "A unit test python plugin"
                };

                File.WriteAllText(Path.Combine(tempDir, "plugin.json"), JsonSerializer.Serialize(manifest));
                File.WriteAllText(Path.Combine(tempDir, "run.py"), "# sample python script\n");

                var plugin = PythonPlugin.FromDirectory(tempDir);
                Assert.NotNull(plugin);
                Assert.Equal("test-py-plugin", plugin.Id);
                Assert.Equal("Test Python Plugin", plugin.Name);
                Assert.Equal("2.1.0", plugin.Version);
                Assert.Equal("Scrim DJ", plugin.Author);
                Assert.Equal(Path.Combine(tempDir, "run.py"), plugin.ScriptPath);
            } finally {
                if (Directory.Exists(tempDir)) {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [Fact]
        public void PythonPlugin_FromScript_DerivesDefaultMetadata() {
            string tempScript = Path.Combine(Path.GetTempPath(), "cool_notifier_" + Guid.NewGuid().ToString("N") + ".py");
            File.WriteAllText(tempScript, "# standalone python script\n");

            try {
                var plugin = PythonPlugin.FromScript(tempScript);
                Assert.NotNull(plugin);
                Assert.StartsWith("cool_notifier_", plugin.Name);
                Assert.Equal("1.0.0", plugin.Version);
                Assert.Equal("Community", plugin.Author);
            } finally {
                if (File.Exists(tempScript)) {
                    File.Delete(tempScript);
                }
            }
        }

        [Fact]
        public void PythonPlugin_ResolvePythonExecutable_FindsPythonIfInstalled() {
            string? python = PythonPlugin.ResolvePythonExecutable(AppDomain.CurrentDomain.BaseDirectory);
            // Python is installed on this test environment
            Assert.NotNull(python);
            Assert.Contains("python", python.ToLowerInvariant());
        }

        [Fact]
        public async Task PythonPlugin_ExecutesAndReceivesEvents_Cleanly() {
            string tempDir = Path.Combine(Path.GetTempPath(), "scrim_e2e_py_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            string logFile = Path.Combine(tempDir, "output.log");
            string scriptFile = Path.Combine(tempDir, "main.py");

            // Simple self-contained python test script that responds to stdin JSON events
            string pythonCode = $@"
import sys
import json

log_path = r""{logFile}""

def log(msg):
    with open(log_path, ""a"", encoding=""utf-8"") as f:
        f.write(msg + ""\n"")

log(""STARTED"")
for line in sys.stdin:
    line = line.strip()
    if not line:
        continue
    try:
        data = json.loads(line)
        msg_type = data.get(""type"")
        if msg_type == ""init"":
            station = data.get(""station"", {{}}).get(""station_name"", """")
            log(f""INIT:{{station}}"")
        elif msg_type == ""event"":
            evt = data.get(""event"")
            if evt == ""metadata_changed"":
                track = data.get(""data"", {{}}).get(""title"", """")
                log(f""TRACK:{{track}}"")
            elif evt == ""broadcasting_state_changed"":
                is_live = data.get(""data"", {{}}).get(""is_live"", False)
                log(f""LIVE:{{is_live}}"")
        elif msg_type == ""shutdown"":
            log(""SHUTDOWN"")
            break
    except Exception as e:
        log(f""ERROR:{{e}}"")
";
            File.WriteAllText(scriptFile, pythonCode);

            var plugin = new PythonPlugin(scriptFile);

            var hostMock = new Mock<IScrimHost>();
            var broadcast = new BroadcastHub();
            var metaMock = new Mock<IMetadataService>();
            var profileManagerMock = new Mock<IProfileManager>();
            var profile = new ScrimProfile { StationName = "Unit Test FM", Port = 9000 };
            profileManagerMock.Setup(p => p.CurrentProfile).Returns(profile);

            hostMock.Setup(h => h.Broadcast).Returns(broadcast);
            hostMock.Setup(h => h.Metadata).Returns(metaMock.Object);
            hostMock.Setup(h => h.ProfileManager).Returns(profileManagerMock.Object);

            try {
                plugin.Initialize(hostMock.Object);

                // Allow process to start and read init message
                await Task.Delay(800);

                // Trigger metadata event
                metaMock.Raise(m => m.MetadataChanged += null, metaMock.Object, new MediaMetadata {
                    Title = "Bohemian Rhapsody",
                    Artist = "Queen",
                    Album = "A Night at the Opera",
                    Duration = TimeSpan.FromSeconds(354)
                });

                await Task.Delay(500);

                // Trigger broadcast state event
                broadcast.StartBroadcasting(System.Threading.Channels.Channel.CreateUnbounded<byte[]>().Reader);

                await Task.Delay(500);

                plugin.Shutdown();

                await Task.Delay(300);

                Assert.True(File.Exists(logFile), "Python plugin did not create output log file.");
                string logContent = File.ReadAllText(logFile);
                Assert.Contains("STARTED", logContent);
                Assert.Contains("INIT:Unit Test FM", logContent);
                Assert.Contains("TRACK:Bohemian Rhapsody", logContent);
                Assert.Contains("LIVE:True", logContent);
                Assert.Contains("SHUTDOWN", logContent);
            } finally {
                plugin.Dispose();
                if (Directory.Exists(tempDir)) {
                    try {
                        Directory.Delete(tempDir, true);
                    } catch { }
                }
            }
        }

        [Fact]
        public async Task PythonPlugin_WithScrimSdk_ExecutesAndReceivesEvents() {
            string tempDir = Path.Combine(Path.GetTempPath(), "scrim_sdk_py_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            // Locate src/Plugins/scrim.py by walking upwards from BaseDirectory
            string? currentDir = AppDomain.CurrentDomain.BaseDirectory;
            string? sourceSdk = null;
            while (currentDir != null) {
                string candidate = Path.Combine(currentDir, "src", "Plugins", "scrim.py");
                if (File.Exists(candidate)) {
                    sourceSdk = candidate;
                    break;
                }
                currentDir = Directory.GetParent(currentDir)?.FullName;
            }

            Assert.True(sourceSdk != null && File.Exists(sourceSdk), $"Could not find source scrim.py. Searched up from {AppDomain.CurrentDomain.BaseDirectory}");
            string destSdk = Path.Combine(tempDir, "scrim.py");
            File.Copy(sourceSdk!, destSdk, true);

            string logFile = Path.Combine(tempDir, "sdk_output.log");
            string scriptFile = Path.Combine(tempDir, "main.py");

            string pythonCode = $@"
import os
import sys
from scrim import ScrimPlugin, TrackInfo

log_path = r""{logFile}""

class MyPlugin(ScrimPlugin):
    def on_init(self, station_info):
        with open(log_path, ""a"", encoding=""utf-8"") as f:
            f.write(f""SDK_INIT:{{self.station_name}}\n"")

    def on_metadata_changed(self, track: TrackInfo):
        with open(log_path, ""a"", encoding=""utf-8"") as f:
            f.write(f""SDK_TRACK:{{track.artist}} - {{track.title}}\n"")

    def on_broadcast_state_changed(self, is_live: bool):
        with open(log_path, ""a"", encoding=""utf-8"") as f:
            f.write(f""SDK_LIVE:{{is_live}}\n"")

    def on_shutdown(self):
        with open(log_path, ""a"", encoding=""utf-8"") as f:
            f.write(""SDK_SHUTDOWN\n"")

if __name__ == ""__main__"":
    plugin = MyPlugin()
    plugin.run()
";
            File.WriteAllText(scriptFile, pythonCode);

            var plugin = new PythonPlugin(scriptFile);
            var hostMock = new Mock<IScrimHost>();
            var broadcast = new BroadcastHub();
            var metaMock = new Mock<IMetadataService>();
            var profileManagerMock = new Mock<IProfileManager>();
            var profile = new ScrimProfile { StationName = "Retro Synthwave Radio", Port = 8000 };
            profileManagerMock.Setup(p => p.CurrentProfile).Returns(profile);

            hostMock.Setup(h => h.Broadcast).Returns(broadcast);
            hostMock.Setup(h => h.Metadata).Returns(metaMock.Object);
            hostMock.Setup(h => h.ProfileManager).Returns(profileManagerMock.Object);

            try {
                plugin.Initialize(hostMock.Object);
                await Task.Delay(800);

                metaMock.Raise(m => m.MetadataChanged += null, metaMock.Object, new MediaMetadata {
                    Title = "Resonance",
                    Artist = "HOME",
                    Album = "Odyssey",
                    Duration = TimeSpan.FromSeconds(212)
                });
                await Task.Delay(500);

                broadcast.StartBroadcasting(System.Threading.Channels.Channel.CreateUnbounded<byte[]>().Reader);
                await Task.Delay(500);

                plugin.Shutdown();
                await Task.Delay(300);

                Assert.True(File.Exists(logFile), "SDK test log file was not generated.");
                string logContent = File.ReadAllText(logFile);
                Assert.Contains("SDK_INIT:Retro Synthwave Radio", logContent);
                Assert.Contains("SDK_TRACK:HOME - Resonance", logContent);
                Assert.Contains("SDK_LIVE:True", logContent);
                Assert.Contains("SDK_SHUTDOWN", logContent);
            } finally {
                plugin.Dispose();
                if (Directory.Exists(tempDir)) {
                    try {
                        Directory.Delete(tempDir, true);
                    } catch { }
                }
            }
        }
    }
}
