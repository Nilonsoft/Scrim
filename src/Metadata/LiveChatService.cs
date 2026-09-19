using System;
using System.Collections.Generic;
using System.Linq;

namespace Scrim.Metadata {
    public interface ILiveChatService {
        ChatMessage AddMessage(string sender, string text, bool isHost = false, string? color = null);
        IReadOnlyList<ChatMessage> GetRecentMessages();
        void Clear();
        void NotifyStatusChanged(bool enabled);
        event Action<ChatMessage>? MessagePosted;
        event Action? ChatCleared;
        event Action<bool>? ChatStatusChanged;
    }

    public class LiveChatService : ILiveChatService {
        private readonly object _lock = new();
        private readonly List<ChatMessage> _messages = new();
        private const int MaxMessages = 100;

        private static readonly string[] ListenerColors = new[] {
            "#00d2ff", "#38bdf8", "#34d399", "#10b981", "#f59e0b",
            "#fb923c", "#f43f5e", "#ec4899", "#a855f7", "#818cf8"
        };

        public event Action<ChatMessage>? MessagePosted;
        public event Action? ChatCleared;
        public event Action<bool>? ChatStatusChanged;

        public ChatMessage AddMessage(string sender, string text, bool isHost = false, string? color = null) {
            string cleanSender = string.IsNullOrWhiteSpace(sender) ? "Anonymous Listener" : sender.Trim();
            if (cleanSender.Length > 32) {
                cleanSender = cleanSender.Substring(0, 32);
            }

            string cleanText = string.IsNullOrWhiteSpace(text) ? "" : text.Trim();
            if (cleanText.Length > 300) {
                cleanText = cleanText.Substring(0, 300);
            }

            string chosenColor = color ?? PickColorForSender(cleanSender, isHost);

            var message = new ChatMessage {
                Id = Guid.NewGuid().ToString(),
                Sender = cleanSender,
                Text = cleanText,
                Timestamp = DateTime.UtcNow,
                IsHost = isHost,
                Color = chosenColor
            };

            lock (_lock) {
                _messages.Add(message);
                if (_messages.Count > MaxMessages) {
                    _messages.RemoveAt(0);
                }
            }

            MessagePosted?.Invoke(message);
            return message;
        }

        public IReadOnlyList<ChatMessage> GetRecentMessages() {
            lock (_lock) {
                return _messages.ToList();
            }
        }

        public void Clear() {
            lock (_lock) {
                _messages.Clear();
            }
            ChatCleared?.Invoke();
        }

        public void NotifyStatusChanged(bool enabled) {
            ChatStatusChanged?.Invoke(enabled);
        }

        private static string PickColorForSender(string sender, bool isHost) {
            if (isHost) {
                return "#ef4444";
            }
            int hash = Math.Abs(sender.GetHashCode());
            return ListenerColors[hash % ListenerColors.Length];
        }
    }
}
