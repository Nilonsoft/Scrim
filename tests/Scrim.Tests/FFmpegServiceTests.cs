using System;
using System.IO;
using Xunit;
using Scrim.Encoding;

namespace Scrim.Tests {
    public class FFmpegServiceTests {
        [Fact]
        public void FFmpegService_ProvidesValidInstallCommands() {
            var service = new FFmpegService();
            Assert.Equal("winget install ffmpeg", service.GetWingetCommand());
            Assert.Equal("https://ffmpeg.org/download.html", service.GetManualDownloadUrl());
        }

        [Fact]
        public void CheckInstallation_DoesNotThrowAndReturnsConsistentStatus() {
            var service = new FFmpegService();
            bool installed = service.CheckInstallation();
            Assert.Equal(installed, service.IsInstalled);

            if (installed) {
                Assert.NotNull(service.ExecutablePath);
                Assert.NotEmpty(service.ExecutablePath);
            } else {
                Assert.Null(service.ExecutablePath);
            }
        }
    }
}
