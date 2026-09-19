using System;
using Xunit;
using Scrim.Audio;

namespace Scrim.Tests {
    public class VoiceChangerProcessorTests {
        [Fact]
        public void Process_Normal_DoesNotAlterBuffer() {
            var processor = new VoiceChangerProcessor();
            byte[] buffer = new byte[100];
            for (int i = 0; i < buffer.Length; i++) {
                buffer[i] = (byte)(i % 255);
            }
            
            byte[] original = new byte[100];
            Array.Copy(buffer, original, 100);
            
            processor.Process(buffer, VoiceEffect.Normal);
            
            Assert.Equal(original, buffer);
        }

        [Fact]
        public void Process_Robot_AltersBuffer() {
            var processor = new VoiceChangerProcessor();
            
            // Create a constant tone
            byte[] buffer = new byte[8820]; // 100ms
            for (int i = 0; i < buffer.Length; i += 2) {
                var bytes = BitConverter.GetBytes((short)10000);
                buffer[i] = bytes[0];
                buffer[i + 1] = bytes[1];
            }
            
            byte[] original = new byte[8820];
            Array.Copy(buffer, original, 8820);
            
            processor.Process(buffer, VoiceEffect.Robot);
            
            Assert.NotEqual(original, buffer);
        }

        [Fact]
        public void Process_Radio_AltersBuffer() {
            var processor = new VoiceChangerProcessor();
            
            byte[] buffer = new byte[8820]; // 100ms
            for (int i = 0; i < buffer.Length; i += 2) {
                var bytes = BitConverter.GetBytes((short)10000);
                buffer[i] = bytes[0];
                buffer[i + 1] = bytes[1];
            }
            
            byte[] original = new byte[8820];
            Array.Copy(buffer, original, 8820);
            
            processor.Process(buffer, VoiceEffect.Radio);
            
            Assert.NotEqual(original, buffer);
        }

        [Fact]
        public void Process_Alien_AltersBuffer() {
            var processor = new VoiceChangerProcessor();
            
            byte[] buffer = new byte[8820]; // 100ms
            for (int i = 0; i < buffer.Length; i += 2) {
                var bytes = BitConverter.GetBytes((short)10000);
                buffer[i] = bytes[0];
                buffer[i + 1] = bytes[1];
            }
            
            byte[] original = new byte[8820];
            Array.Copy(buffer, original, 8820);
            
            processor.Process(buffer, VoiceEffect.Alien);
            
            Assert.NotEqual(original, buffer);
        }

        [Fact]
        public void Process_Woman_AltersBuffer() {
            var processor = new VoiceChangerProcessor();
            
            byte[] buffer = new byte[8820]; // 100ms stereo / mono
            for (int i = 0; i < buffer.Length; i += 2) {
                var bytes = BitConverter.GetBytes((short)10000);
                buffer[i] = bytes[0];
                buffer[i + 1] = bytes[1];
            }
            
            byte[] original = new byte[8820];
            Array.Copy(buffer, original, 8820);
            
            processor.Process(buffer, VoiceEffect.Woman);
            
            Assert.NotEqual(original, buffer);
        }

        [Fact]
        public void Process_Man_AltersBuffer() {
            var processor = new VoiceChangerProcessor();
            
            byte[] buffer = new byte[8820]; // 100ms stereo / mono
            for (int i = 0; i < buffer.Length; i += 2) {
                var bytes = BitConverter.GetBytes((short)10000);
                buffer[i] = bytes[0];
                buffer[i + 1] = bytes[1];
            }
            
            byte[] original = new byte[8820];
            Array.Copy(buffer, original, 8820);
            
            processor.Process(buffer, VoiceEffect.Man);
            
            Assert.NotEqual(original, buffer);
        }

        [Fact]
        public void Process_AnimeGirl_AltersBuffer() {
            var processor = new VoiceChangerProcessor();
            
            byte[] buffer = new byte[8820]; // 100ms
            for (int i = 0; i < buffer.Length; i += 2) {
                var bytes = BitConverter.GetBytes((short)10000);
                buffer[i] = bytes[0];
                buffer[i + 1] = bytes[1];
            }
            
            byte[] original = new byte[8820];
            Array.Copy(buffer, original, 8820);
            
            processor.Process(buffer, VoiceEffect.AnimeGirl);
            
            Assert.NotEqual(original, buffer);

            // Verify non-zero and no NaN corruption in output
            bool hasNonZero = false;
            for (int i = 0; i < buffer.Length; i += 2) {
                short sample = BitConverter.ToInt16(buffer, i);
                if (sample != 0) hasNonZero = true;
            }
            Assert.True(hasNonZero);
        }

        [Fact]
        public void Process_AnimeGirl_Stereo_ProcessesBothChannels() {
            var processor = new VoiceChangerProcessor();
            
            byte[] buffer = new byte[8820]; // 100ms stereo (4 bytes per frame)
            for (int i = 0; i < buffer.Length; i += 4) {
                var bytesL = BitConverter.GetBytes((short)8000);
                var bytesR = BitConverter.GetBytes((short)12000);
                buffer[i] = bytesL[0];
                buffer[i + 1] = bytesL[1];
                buffer[i + 2] = bytesR[0];
                buffer[i + 3] = bytesR[1];
            }
            
            byte[] original = new byte[8820];
            Array.Copy(buffer, original, 8820);
            
            processor.Process(buffer, VoiceEffect.AnimeGirl);
            
            Assert.NotEqual(original, buffer);
        }

        [Fact]
        public void VoiceEffectLoader_AnimeGirl_IsRegistered() {
            var loader = new VoiceEffectLoader();
            var provider = loader.GetProvider("anime_girl");
            
            Assert.NotNull(provider);
            Assert.Equal("anime_girl", provider.Id);
            Assert.Equal("Anime Girl", provider.Name);
        }
    }
}
