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

        [Fact]
        public void WindowBounds_PersistProperlyAcrossProfileSaves() {
            var defaultProfile = new ScrimProfile();
            Assert.Equal(1200, defaultProfile.WindowWidth);
            Assert.Equal(800, defaultProfile.WindowHeight);
            Assert.False(defaultProfile.WindowMaximized);

            var manager = new ProfileManager();
            var profile = new ScrimProfile {
                ProfileName = _testProfileName + "_WindowBounds",
                WindowWidth = 1440,
                WindowHeight = 920,
                WindowMaximized = true
            };

            manager.SaveProfile(profile);

            var manager2 = new ProfileManager();
            manager2.LoadProfile(profile.ProfileName);

            Assert.Equal(1440, manager2.CurrentProfile.WindowWidth);
            Assert.Equal(920, manager2.CurrentProfile.WindowHeight);
            Assert.True(manager2.CurrentProfile.WindowMaximized);

            // Cleanup
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var path = Path.Combine(appData, ".scrim", $"{profile.ProfileName}.json");
            if (File.Exists(path)) {
                File.Delete(path);
            }
        }

        [Fact]
        public void ResolveAssetPath_ResolvesRelativeAndDefaultAssets() {
            var manager = new ProfileManager();
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var assetsDir = Path.Combine(appData, ".scrim", "assets");
            if (!Directory.Exists(assetsDir)) {
                Directory.CreateDirectory(assetsDir);
            }

            string testFileName = "test_banner_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".jpg";
            string testFilePath = Path.Combine(assetsDir, testFileName);
            File.WriteAllBytes(testFilePath, new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 });

            try {
                string nameWithoutExt = Path.GetFileNameWithoutExtension(testFileName);

                // 1. .\assets\filename.jpg
                var res1 = manager.ResolveAssetPath(@$".\assets\{testFileName}");
                Assert.NotNull(res1);
                Assert.True(File.Exists(res1));

                // 2. assets\filename.jpg
                var res2 = manager.ResolveAssetPath(@$"assets\{testFileName}");
                Assert.NotNull(res2);
                Assert.Equal(res1, res2);

                // 3. assets/filename.jpg (forward slash)
                var res3 = manager.ResolveAssetPath($"assets/{testFileName}");
                Assert.NotNull(res3);
                Assert.Equal(res1, res3);

                // 4. .\assets\filename (without extension)
                var res4 = manager.ResolveAssetPath(@$".\assets\{nameWithoutExt}");
                Assert.NotNull(res4);
                Assert.Equal(res1, res4);

                // 5. filename.jpg (looks in assets by default)
                var res5 = manager.ResolveAssetPath(testFileName);
                Assert.NotNull(res5);
                Assert.Equal(res1, res5);

                // 6. filename without extension (looks in assets by default)
                var res6 = manager.ResolveAssetPath(nameWithoutExt);
                Assert.NotNull(res6);
                Assert.Equal(res1, res6);

                // 7. Web URLs return null (not local asset files)
                Assert.Null(manager.ResolveAssetPath("https://example.com/banner.png"));
                Assert.Null(manager.ResolveAssetPath("http://localhost:4242/banner.png"));

                // 8. Empty / whitespace returns null
                Assert.Null(manager.ResolveAssetPath(""));
                Assert.Null(manager.ResolveAssetPath("   "));

                // 9. Non-existent file returns null
                Assert.Null(manager.ResolveAssetPath("non_existent_image_12345.png"));
            } finally {
                if (File.Exists(testFilePath)) {
                    File.Delete(testFilePath);
                }
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
