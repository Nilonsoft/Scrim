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
    }
}
