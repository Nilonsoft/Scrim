using System;
using System.IO;
using Scrim.Configuration;
using Scrim.Server;
using Xunit;

namespace Scrim.Tests {
    public class NetworkDiscoveryServiceTests {
        [Fact]
        public void NetworkDiscoveryService_LocalIp_ReturnsValidIPv4OrLocalhost() {
            var service = new NetworkDiscoveryService();
            var localIp = service.PrimaryLocalIp;

            Assert.False(string.IsNullOrWhiteSpace(localIp));
            Assert.Contains(".", localIp);
        }

        [Fact]
        public void NetworkDiscoveryService_GetLocalShareUrl_FormatsProperly() {
            var service = new NetworkDiscoveryService();
            var url = service.GetLocalShareUrl(4242);

            Assert.StartsWith("http://", url);
            Assert.EndsWith(":4242", url);
        }

        [Fact]
        public void NetworkDiscoveryService_GetPublicShareUrl_FormatsProperly() {
            var service = new NetworkDiscoveryService();
            var url = service.GetPublicShareUrl(4242);

            Assert.StartsWith("http://", url);
            Assert.EndsWith(":4242", url);
        }

        [Fact]
        public void ProfileManager_PersistsNetworkAccessSettings() {
            string testProfileName = "TestNetProfile_" + Guid.NewGuid().ToString().Substring(0, 8);
            var manager = new ProfileManager();

            try {
                var profile = new ScrimProfile {
                    ProfileName = testProfileName,
                    EnableNetworkAccess = true,
                    EnableUpnpPortForwarding = true
                };

                manager.SaveProfile(profile);

                var manager2 = new ProfileManager();
                manager2.LoadProfile(testProfileName);

                Assert.True(manager2.CurrentProfile.EnableNetworkAccess);
                Assert.True(manager2.CurrentProfile.EnableUpnpPortForwarding);
            } finally {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var path = Path.Combine(appData, ".scrim", $"{testProfileName}.json");
                if (File.Exists(path)) {
                    File.Delete(path);
                }
            }
        }
    }
}
