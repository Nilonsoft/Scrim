using System;
using System.IO;
using System.Linq;
using Xunit;
using Scrim.Configuration;

namespace Scrim.Tests {
    public class ProfileManagerTests : IDisposable {
        private readonly string _testProfileName;

        public ProfileManagerTests() {
            _testProfileName = "TestProfile_" + Guid.NewGuid().ToString().Substring(0, 8);
        }

        [Fact]
        public void SaveAndLoadProfile_WorksCorrectly() {
            var manager = new ProfileManager();
            
            var newProfile = new ScrimProfile {
                ProfileName = _testProfileName,
                AudioFormat = "Aac",
                Bitrate = 320
            };
            
            manager.SaveProfile(newProfile);
            
            // Re-load in a fresh instance
            var manager2 = new ProfileManager();
            manager2.LoadProfile(_testProfileName);
            
            Assert.Equal(_testProfileName, manager2.CurrentProfile.ProfileName);
            Assert.Equal("Aac", manager2.CurrentProfile.AudioFormat);
            Assert.Equal(320, manager2.CurrentProfile.Bitrate);
            
            var available = manager2.GetAvailableProfiles();
            Assert.Contains(_testProfileName, available);
        }

        public void Dispose() {
            // Cleanup the file that was created in the actual user directory
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var path = Path.Combine(appData, ".scrim", $"{_testProfileName}.json");
            if (File.Exists(path)) {
                File.Delete(path);
            }
        }
    }
}
