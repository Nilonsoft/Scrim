using System.Linq;
using Xunit;
using Scrim.Metadata;

namespace Scrim.Tests {
    public class SongRequestControllerTests {
        [Fact]
        public void SubmitRequest_AddsRequestToQueue() {
            var controller = new SongRequestController();
            controller.SubmitRequest("Free Bird");
            
            var queue = controller.GetLiveQueue().ToList();
            Assert.Single(queue);
            Assert.Equal("Free Bird", queue[0].Query);
            Assert.Equal("Pending", queue[0].Status);
        }

        [Fact]
        public void UpdateStatus_ChangesRequestStatus() {
            var controller = new SongRequestController();
            controller.SubmitRequest("Never Gonna Give You Up");
            var request = controller.GetLiveQueue().First();
            
            controller.UpdateStatus(request.Id, "Playing");
            
            var updatedQueue = controller.GetLiveQueue().ToList();
            Assert.Single(updatedQueue);
            Assert.Equal("Playing", updatedQueue[0].Status);
        }

        [Fact]
        public void RemoveRequest_RemovesFromQueue() {
            var controller = new SongRequestController();
            controller.SubmitRequest("Song 1");
            var request = controller.GetLiveQueue().First();
            
            controller.RemoveRequest(request.Id);
            
            var queue = controller.GetLiveQueue().ToList();
            Assert.Empty(queue);
        }
        
        [Fact]
        public void GetLiveQueue_ReturnsOrderedByRequestedAt() {
            var controller = new SongRequestController();
            controller.SubmitRequest("Song A");
            System.Threading.Thread.Sleep(10); // Ensure different timestamps
            controller.SubmitRequest("Song B");
            
            var queue = controller.GetLiveQueue().ToList();
            Assert.Equal(2, queue.Count);
            Assert.Equal("Song A", queue[0].Query);
            Assert.Equal("Song B", queue[1].Query);
        }
    }
}
