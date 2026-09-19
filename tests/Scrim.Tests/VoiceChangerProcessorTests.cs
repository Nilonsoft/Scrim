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
    }
}
