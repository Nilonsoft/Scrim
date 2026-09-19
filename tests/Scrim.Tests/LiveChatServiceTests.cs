using System;
using System.Linq;
using Scrim.Metadata;
using Xunit;

namespace Scrim.Tests {
    public class LiveChatServiceTests {
        [Fact]
        public void AddMessage_AddsMessageAndInvokesEvent() {
            var service = new LiveChatService();
            ChatMessage? posted = null;
            service.MessagePosted += msg => posted = msg;

            var result = service.AddMessage("TestUser", "Hello Scrim listeners!");

            Assert.NotNull(posted);
            Assert.Equal("TestUser", result.Sender);
            Assert.Equal("Hello Scrim listeners!", result.Text);
            Assert.False(result.IsHost);
            Assert.Single(service.GetRecentMessages());
            Assert.Equal(result.Id, posted.Id);
        }

        [Fact]
        public void AddMessage_AsHost_MarksHostAndUsesHostColor() {
            var service = new LiveChatService();
            var result = service.AddMessage("HostDJ", "Welcome to the show!", isHost: true);

            Assert.True(result.IsHost);
            Assert.Equal("#ef4444", result.Color);
            Assert.Equal("HostDJ", result.Sender);
        }

        [Fact]
        public void AddMessage_TruncatesLongSenderAndText() {
            var service = new LiveChatService();
            string longSender = new string('A', 50);
            string longText = new string('B', 400);

            var result = service.AddMessage(longSender, longText);

            Assert.Equal(32, result.Sender.Length);
            Assert.Equal(300, result.Text.Length);
        }

        [Fact]
        public void AddMessage_LimitsHistoryToMax100Messages() {
            var service = new LiveChatService();
            for (int i = 0; i < 110; i++) {
                service.AddMessage($"User{i}", $"Message {i}");
            }

            var messages = service.GetRecentMessages();
            Assert.Equal(100, messages.Count);
            Assert.Equal("Message 10", messages.First().Text);
            Assert.Equal("Message 109", messages.Last().Text);
        }

        [Fact]
        public void Clear_EmptiesMessagesAndFiresEvent() {
            var service = new LiveChatService();
            service.AddMessage("User", "Msg 1");
            service.AddMessage("User", "Msg 2");

            bool clearedFired = false;
            service.ChatCleared += () => clearedFired = true;

            service.Clear();

            Assert.True(clearedFired);
            Assert.Empty(service.GetRecentMessages());
        }

        [Fact]
        public void NotifyStatusChanged_InvokesEvent() {
            var service = new LiveChatService();
            bool? statusReceived = null;
            service.ChatStatusChanged += s => statusReceived = s;

            service.NotifyStatusChanged(false);
            Assert.False(statusReceived);

            service.NotifyStatusChanged(true);
            Assert.True(statusReceived);
        }
    }
}
