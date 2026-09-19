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

        [Fact]
        public void AddMessage_WithUserId_StoresUserId() {
            var service = new LiveChatService();
            var msg = service.AddMessage("Listener", "Hey!", isHost: false, userId: "hash_abc_123");

            Assert.Equal("hash_abc_123", msg.UserId);
        }

        [Fact]
        public void BanUser_BansUser_PurgesTheirMessagesAndFiresEvent() {
            var service = new LiveChatService();
            service.AddMessage("GoodUser", "Nice music!", userId: "user_1");
            service.AddMessage("BadUser", "Spam message 1", userId: "user_2");
            service.AddMessage("BadUser", "Spam message 2", userId: "user_2");
            service.AddMessage("GoodUser", "Loving the vibes", userId: "user_1");

            Assert.Equal(4, service.GetRecentMessages().Count);
            Assert.False(service.IsUserBanned("user_2"));

            string? bannedIdReceived = null;
            service.UserBanned += uid => bannedIdReceived = uid;

            service.BanUser("user_2", "BadUser");

            Assert.True(service.IsUserBanned("user_2"));
            Assert.Equal("user_2", bannedIdReceived);

            var remaining = service.GetRecentMessages();
            Assert.Equal(2, remaining.Count);
            Assert.All(remaining, m => Assert.Equal("user_1", m.UserId));
            Assert.Single(service.GetBannedUsers());
            Assert.Equal("user_2", service.GetBannedUsers().First().UserId);
            Assert.Equal("BadUser", service.GetBannedUsers().First().Nickname);
        }

        [Fact]
        public void UnbanUser_RemovesBanAndFiresEvent() {
            var service = new LiveChatService();
            service.BanUser("user_xyz", "Troll");
            Assert.True(service.IsUserBanned("user_xyz"));

            string? unbannedIdReceived = null;
            service.UserUnbanned += uid => unbannedIdReceived = uid;

            service.UnbanUser("user_xyz");

            Assert.False(service.IsUserBanned("user_xyz"));
            Assert.Equal("user_xyz", unbannedIdReceived);
            Assert.Empty(service.GetBannedUsers());
        }

        [Fact]
        public void SyncBannedUsers_PopulatesBannedUsersDictionary() {
            var service = new LiveChatService();
            var bans = new System.Collections.Generic.List<Scrim.Configuration.BannedChatUser> {
                new() { UserId = "u1", Nickname = "Nick1" },
                new() { UserId = "u2", Nickname = "Nick2" }
            };

            service.SyncBannedUsers(bans);

            Assert.True(service.IsUserBanned("u1"));
            Assert.True(service.IsUserBanned("u2"));
            Assert.False(service.IsUserBanned("u3"));
            Assert.Equal(2, service.GetBannedUsers().Count);
        }

        [Fact]
        public void RemoveMessage_DeletesSpecificMessageAndFiresEvent() {
            var service = new LiveChatService();
            var m1 = service.AddMessage("Listener1", "First message");
            var m2 = service.AddMessage("DJ Host", "Host message", isHost: true);
            var m3 = service.AddMessage("Listener2", "Third message");

            Assert.Equal(3, service.GetRecentMessages().Count);

            string? removedId = null;
            service.MessageRemoved += id => removedId = id;

            // Remove host message
            bool removedHost = service.RemoveMessage(m2.Id);
            Assert.True(removedHost);
            Assert.Equal(m2.Id, removedId);
            Assert.Equal(2, service.GetRecentMessages().Count);
            Assert.DoesNotContain(service.GetRecentMessages(), m => m.Id == m2.Id);

            // Remove listener message
            bool removedListener = service.RemoveMessage(m1.Id);
            Assert.True(removedListener);
            Assert.Equal(m1.Id, removedId);
            Assert.Single(service.GetRecentMessages());
            Assert.Equal(m3.Id, service.GetRecentMessages().First().Id);

            // Removing non-existent message returns false
            bool removedNonExistent = service.RemoveMessage("non-existent-id");
            Assert.False(removedNonExistent);
        }

        [Theory]
        [InlineData(":O", "😮")]
        [InlineData(":o", "😮")]
        [InlineData(":-O", "😮")]
        [InlineData("Whoa :O that is awesome!", "Whoa 😮 that is awesome!")]
        [InlineData(":)", "😊")]
        [InlineData(":D", "😀")]
        [InlineData(";)", "😉")]
        [InlineData(":(", "😢")]
        [InlineData("<3", "❤️")]
        [InlineData(":fire:", "🔥")]
        [InlineData(":thumbsup:", "👍")]
        [InlineData("http://localhost:8080/test", "http://localhost:8080/test")]
        public void ConvertEmoticons_ConvertsExpectedEmoticonsSafely(string input, string expected) {
            string result = LiveChatService.ConvertEmoticons(input);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void AddMessage_ConvertsEmoticonsInChatMessage() {
            var service = new LiveChatService();
            var msg = service.AddMessage("User", "Check this out :O <3 :fire:");
            Assert.Equal("Check this out 😮 ❤️ 🔥", msg.Text);
        }
    }
}
