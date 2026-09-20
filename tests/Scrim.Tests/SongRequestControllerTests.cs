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

        [Fact]
        public void SubmitRequest_WithDedication_StoresDedication() {
            var controller = new SongRequestController();
            controller.SubmitRequest("Bohemian Rhapsody", "For Sarah on her birthday!");

            var queue = controller.GetLiveQueue().ToList();
            Assert.Single(queue);
            Assert.Equal("Bohemian Rhapsody", queue[0].Query);
            Assert.Equal("For Sarah on her birthday!", queue[0].Dedication);
        }

        [Fact]
        public void ClearQueue_EmptiesAllRequests() {
            var controller = new SongRequestController();
            controller.SubmitRequest("Song 1");
            controller.SubmitRequest("Song 2");
            Assert.Equal(2, controller.GetLiveQueue().Count());

            controller.ClearQueue();
            Assert.Empty(controller.GetLiveQueue());
        }

        [Fact]
        public void MoveUp_MovesRequestUpInQueue() {
            var controller = new SongRequestController();
            controller.SubmitRequest("First");
            controller.SubmitRequest("Second");

            var second = controller.GetLiveQueue().Last();
            bool moved = controller.MoveUp(second.Id);

            Assert.True(moved);
            var queue = controller.GetLiveQueue().ToList();
            Assert.Equal("Second", queue[0].Query);
            Assert.Equal("First", queue[1].Query);
        }

        [Fact]
        public void MoveDown_MovesRequestDownInQueue() {
            var controller = new SongRequestController();
            controller.SubmitRequest("First");
            controller.SubmitRequest("Second");

            var first = controller.GetLiveQueue().First();
            bool moved = controller.MoveDown(first.Id);

            Assert.True(moved);
            var queue = controller.GetLiveQueue().ToList();
            Assert.Equal("Second", queue[0].Query);
            Assert.Equal("First", queue[1].Query);
        }
    }
}
