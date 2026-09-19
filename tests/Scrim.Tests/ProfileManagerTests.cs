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

        [Fact]
        public void BannerUrl_DefaultsToEmpty_AndPersistsProperly() {
            var defaultProfile = new ScrimProfile();
            Assert.Equal("", defaultProfile.BannerUrl);

            var manager = new ProfileManager();
            var profile = new ScrimProfile {
                ProfileName = _testProfileName + "_Banner",
                BannerUrl = "https://example.com/stream-banner.png"
            };

            manager.SaveProfile(profile);

            var manager2 = new ProfileManager();
            manager2.LoadProfile(profile.ProfileName);

            Assert.Equal("https://example.com/stream-banner.png", manager2.CurrentProfile.BannerUrl);

            // Clear banner
            manager2.CurrentProfile.BannerUrl = "";
            manager2.SaveProfile(manager2.CurrentProfile);

            var manager3 = new ProfileManager();
            manager3.LoadProfile(profile.ProfileName);
            Assert.Equal("", manager3.CurrentProfile.BannerUrl);

            // Cleanup
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var path = Path.Combine(appData, ".scrim", $"{profile.ProfileName}.json");
            if (File.Exists(path)) {
                File.Delete(path);
            }
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
