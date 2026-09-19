using System.Linq;
using Xunit;
using Scrim.Server;

namespace Scrim.Tests {
    public class PreRollBufferTests {
        [Fact]
        public void PushFrame_WithinLimit_KeepsAllFrames() {
            var buffer = new PreRollBuffer(maxBytes: 100);
            
            buffer.PushFrame(new byte[30]);
            buffer.PushFrame(new byte[40]);
            
            var burst = buffer.GetBurst().ToList();
            Assert.Equal(2, burst.Count);
            Assert.Equal(30, burst[0].Length);
            Assert.Equal(40, burst[1].Length);
        }

        [Fact]
        public void PushFrame_ExceedsLimit_EvictsOldestFrames() {
            var buffer = new PreRollBuffer(maxBytes: 100);
            
            buffer.PushFrame(new byte[60]); // Kept initially
            buffer.PushFrame(new byte[50]); // Total 110 > 100, drops the 60 byte frame
            
            var burst = buffer.GetBurst().ToList();
            Assert.Single(burst);
            Assert.Equal(50, burst[0].Length);
        }

        [Fact]
        public void PushFrame_ExactLimit_KeepsAllFrames() {
            var buffer = new PreRollBuffer(maxBytes: 100);
            
            buffer.PushFrame(new byte[50]);
            buffer.PushFrame(new byte[50]);
            
            var burst = buffer.GetBurst().ToList();
            Assert.Equal(2, burst.Count);
            Assert.Equal(50, burst[0].Length);
            Assert.Equal(50, burst[1].Length);
        }
    }
}
