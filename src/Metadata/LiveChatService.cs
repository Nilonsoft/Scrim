using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Scrim.Configuration;

namespace Scrim.Metadata {
    public interface ILiveChatService {
        ChatMessage AddMessage(string sender, string text, bool isHost = false, string? color = null, string? userId = null);
        IReadOnlyList<ChatMessage> GetRecentMessages();
        void Clear();
        void NotifyStatusChanged(bool enabled);
        void AssignNickname(string targetName, string assignedName);
        bool IsNicknameAllowed(string nickname, IEnumerable<string> blacklist);
        void BanUser(string userId, string? nickname = null);
        void UnbanUser(string userId);
        bool IsUserBanned(string userId);
        IReadOnlyList<BannedChatUser> GetBannedUsers();
        void SyncBannedUsers(IEnumerable<BannedChatUser> bannedUsers);
        bool RemoveMessage(string messageId);
        bool IsNicknameAvailable(string nickname, string? userId, bool isHost = false);
        bool TryClaimNickname(string nickname, string? userId, bool isHost, out string error);
        IReadOnlyList<string> GetClaimedNicknames(string? excludeUserId = null);
        void ReserveHostNickname(string hostName);
        void ClearSessionNicknames();
        event Action<ChatMessage>? MessagePosted;
        event Action<string>? MessageRemoved;
        event Action? ChatCleared;
        event Action<bool>? ChatStatusChanged;
        event Action<string, string>? NicknameAssigned;
        event Action<string>? UserBanned;
        event Action<string>? UserUnbanned;
    }

    public class LiveChatService : ILiveChatService {
        private readonly object _lock = new();
        private readonly List<ChatMessage> _messages = new();
        private readonly Dictionary<string, BannedChatUser> _bannedUsers = new(StringComparer.OrdinalIgnoreCase);
        private const int MaxMessages = 100;

        private static readonly string[] ListenerColors = new[] {
            "#00d2ff", "#38bdf8", "#34d399", "#10b981", "#f59e0b",
            "#fb923c", "#f43f5e", "#ec4899", "#a855f7", "#818cf8"
        };

        private readonly Dictionary<string, string> _claimedNicknames = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _reservedHostNames = new(StringComparer.OrdinalIgnoreCase) { "DJ", "Host", "Broadcaster", "Admin" };

        public event Action<ChatMessage>? MessagePosted;
        public event Action<string>? MessageRemoved;
        public event Action? ChatCleared;
        public event Action<bool>? ChatStatusChanged;
        public event Action<string, string>? NicknameAssigned;
        public event Action<string>? UserBanned;
        public event Action<string>? UserUnbanned;

        public void ReserveHostNickname(string hostName) {
            if (string.IsNullOrWhiteSpace(hostName)) return;
            string clean = hostName.Trim();
            lock (_lock) {
                _reservedHostNames.Add(clean);
                _claimedNicknames[clean] = "host";
            }
        }

        public bool IsNicknameAvailable(string nickname, string? userId, bool isHost = false) {
            if (string.IsNullOrWhiteSpace(nickname)) return false;
            string clean = nickname.Trim();
            if (clean.Length > 32) clean = clean.Substring(0, 32);

            lock (_lock) {
                if (isHost) return true;

                if (_reservedHostNames.Contains(clean)) {
                    return false;
                }

                if (_claimedNicknames.TryGetValue(clean, out var ownerId)) {
                    if (!string.IsNullOrWhiteSpace(userId) && ownerId.Equals(userId.Trim(), StringComparison.OrdinalIgnoreCase)) {
                        return true;
                    }
                    return false;
                }

                return true;
            }
        }

        public bool TryClaimNickname(string nickname, string? userId, bool isHost, out string error) {
            if (string.IsNullOrWhiteSpace(nickname)) {
                error = "Nickname cannot be empty.";
                return false;
            }

            string clean = nickname.Trim();
            if (clean.Length > 32) clean = clean.Substring(0, 32);

            lock (_lock) {
                if (isHost) {
                    _claimedNicknames[clean] = "host";
                    _reservedHostNames.Add(clean);
                    error = string.Empty;
                    return true;
                }

                if (_reservedHostNames.Contains(clean)) {
                    error = "This nickname is reserved for the station host.";
                    return false;
                }

                if (_claimedNicknames.TryGetValue(clean, out var ownerId)) {
                    if (!string.IsNullOrWhiteSpace(userId) && ownerId.Equals(userId.Trim(), StringComparison.OrdinalIgnoreCase)) {
                        error = string.Empty;
                        return true;
                    }
                    error = "This nickname is already in use by another listener this session.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(userId)) {
                    _claimedNicknames[clean] = userId.Trim();
                }

                error = string.Empty;
                return true;
            }
        }

        public IReadOnlyList<string> GetClaimedNicknames(string? excludeUserId = null) {
            lock (_lock) {
                if (string.IsNullOrWhiteSpace(excludeUserId)) {
                    return _claimedNicknames.Keys.ToList();
                }
                string cleanExclude = excludeUserId.Trim();
                return _claimedNicknames
                    .Where(kv => !kv.Value.Equals(cleanExclude, StringComparison.OrdinalIgnoreCase))
                    .Select(kv => kv.Key)
                    .ToList();
            }
        }

        public void ClearSessionNicknames() {
            lock (_lock) {
                _claimedNicknames.Clear();
                foreach (var h in _reservedHostNames) {
                    _claimedNicknames[h] = "host";
                }
            }
        }

        public void AssignNickname(string targetName, string assignedName) {
            string cleanTarget = targetName?.Trim() ?? string.Empty;
            string cleanAssigned = assignedName?.Trim() ?? string.Empty;
            if (!string.IsNullOrEmpty(cleanTarget) && !string.IsNullOrEmpty(cleanAssigned)) {
                lock (_lock) {
                    var targetMsg = _messages.LastOrDefault(m => m.Sender.Equals(cleanTarget, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(m.UserId));
                    string targetId = targetMsg?.UserId ?? cleanTarget;
                    _claimedNicknames[cleanAssigned] = targetId;
                }
                NicknameAssigned?.Invoke(cleanTarget, cleanAssigned);
            }
        }

        public bool IsNicknameAllowed(string nickname, IEnumerable<string> blacklist) {
            if (string.IsNullOrWhiteSpace(nickname)) return false;
            if (blacklist == null) return true;
            string clean = nickname.Trim();
            foreach (var item in blacklist) {
                if (string.IsNullOrWhiteSpace(item)) continue;
                if (clean.IndexOf(item.Trim(), StringComparison.OrdinalIgnoreCase) >= 0) {
                    return false;
                }
            }
            return true;
        }

        public void BanUser(string userId, string? nickname = null) {
            if (string.IsNullOrWhiteSpace(userId)) return;
            string cleanId = userId.Trim();
            string cleanNick = string.IsNullOrWhiteSpace(nickname) ? "User" : nickname.Trim();

            lock (_lock) {
                _bannedUsers[cleanId] = new BannedChatUser {
                    UserId = cleanId,
                    Nickname = cleanNick,
                    BannedAt = DateTime.UtcNow
                };

                // Purge messages sent by this banned user
                _messages.RemoveAll(m => m.UserId != null && m.UserId.Equals(cleanId, StringComparison.OrdinalIgnoreCase));
            }

            UserBanned?.Invoke(cleanId);
        }

        public void UnbanUser(string userId) {
            if (string.IsNullOrWhiteSpace(userId)) return;
            string cleanId = userId.Trim();
            bool removed;

            lock (_lock) {
                removed = _bannedUsers.Remove(cleanId);
            }

            if (removed) {
                UserUnbanned?.Invoke(cleanId);
            }
        }

        public bool IsUserBanned(string userId) {
            if (string.IsNullOrWhiteSpace(userId)) return false;
            lock (_lock) {
                return _bannedUsers.ContainsKey(userId.Trim());
            }
        }

        public IReadOnlyList<BannedChatUser> GetBannedUsers() {
            lock (_lock) {
                return _bannedUsers.Values.ToList();
            }
        }

        public void SyncBannedUsers(IEnumerable<BannedChatUser> bannedUsers) {
            if (bannedUsers == null) return;
            lock (_lock) {
                _bannedUsers.Clear();
                foreach (var b in bannedUsers) {
                    if (!string.IsNullOrWhiteSpace(b?.UserId)) {
                        _bannedUsers[b.UserId.Trim()] = b;
                    }
                }
            }
        }

        public ChatMessage AddMessage(string sender, string text, bool isHost = false, string? color = null, string? userId = null) {
            string cleanSender = string.IsNullOrWhiteSpace(sender) ? "Anonymous Listener" : sender.Trim();
            if (cleanSender.Length > 32) {
                cleanSender = cleanSender.Substring(0, 32);
            }

            lock (_lock) {
                if (!isHost && !string.IsNullOrWhiteSpace(userId)) {
                    string uId = userId.Trim();
                    if (!TryClaimNickname(cleanSender, uId, isHost: false, out _)) {
                        int counter = 2;
                        string candidate = $"{cleanSender} #{counter}";
                        while (_claimedNicknames.TryGetValue(candidate, out var existingOwner) && !existingOwner.Equals(uId, StringComparison.OrdinalIgnoreCase)) {
                            counter++;
                            candidate = $"{cleanSender} #{counter}";
                        }
                        cleanSender = candidate;
                        _claimedNicknames[cleanSender] = uId;
                    }
                } else if (isHost) {
                    _claimedNicknames[cleanSender] = "host";
                    _reservedHostNames.Add(cleanSender);
                }
            }

            string cleanText = string.IsNullOrWhiteSpace(text) ? "" : text.Trim();
            cleanText = ConvertEmoticons(cleanText);
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
                Color = chosenColor,
                UserId = userId?.Trim()
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

        public static string ConvertEmoticons(string text) {
            if (string.IsNullOrEmpty(text)) {
                return text;
            }

            string result = text;
            foreach (var rule in EmoticonRules) {
                result = rule.Pattern.Replace(result, rule.Replacement);
            }
            return result;
        }

        private static readonly (Regex Pattern, string Replacement)[] EmoticonRules = new[] {
            // Shortcodes
            (new Regex(@":fire:", RegexOptions.IgnoreCase | RegexOptions.Compiled), "🔥"),
            (new Regex(@":(?:thumbsup|\+1):", RegexOptions.IgnoreCase | RegexOptions.Compiled), "👍"),
            (new Regex(@":(?:thumbsdown|-1):", RegexOptions.IgnoreCase | RegexOptions.Compiled), "👎"),
            (new Regex(@":(?:party|tada):", RegexOptions.IgnoreCase | RegexOptions.Compiled), "🎉"),
            (new Regex(@":(?:music|note):", RegexOptions.IgnoreCase | RegexOptions.Compiled), "🎵"),
            (new Regex(@":radio:", RegexOptions.IgnoreCase | RegexOptions.Compiled), "📻"),
            (new Regex(@":rocket:", RegexOptions.IgnoreCase | RegexOptions.Compiled), "🚀"),
            (new Regex(@":100:", RegexOptions.IgnoreCase | RegexOptions.Compiled), "💯"),
            (new Regex(@":skull:", RegexOptions.IgnoreCase | RegexOptions.Compiled), "💀"),
            (new Regex(@":star:", RegexOptions.IgnoreCase | RegexOptions.Compiled), "⭐"),
            (new Regex(@":eyes:", RegexOptions.IgnoreCase | RegexOptions.Compiled), "👀"),
            (new Regex(@":sparkles:", RegexOptions.IgnoreCase | RegexOptions.Compiled), "✨"),
            (new Regex(@":clap:", RegexOptions.IgnoreCase | RegexOptions.Compiled), "👏"),
            (new Regex(@":wave:", RegexOptions.IgnoreCase | RegexOptions.Compiled), "👋"),
            (new Regex(@":heart:", RegexOptions.IgnoreCase | RegexOptions.Compiled), "❤️"),

            // Hearts: <3 and </3 (safely avoiding numbers like <300)
            (new Regex(@"</3", RegexOptions.Compiled), "💔"),
            (new Regex(@"<3(?!\d)", RegexOptions.Compiled), "❤️"),

            // Surprised: :O, :o, :-O, :-o, =O, =o, :0
            (new Regex(@"(?<!\d)(?:[:=]-?[oO0])(?![a-zA-Z0-9])", RegexOptions.Compiled), "😮"),

            // Laugh / Big grin: :D, :-D, =D, =-D, xD, XD
            (new Regex(@"(?:(?<!\d)(?:[:=]-?D|=D)|(?<![a-zA-Z0-9])[xX]-?D)(?![a-zA-Z0-9])", RegexOptions.Compiled), "😀"),

            // Smile: :), :-), =), =-), :], =]
            (new Regex(@"(?:[:=]-?[\)\]])", RegexOptions.Compiled), "😊"),

            // Wink: ;), ;-)
            (new Regex(@"(?:;-?[\)\]])", RegexOptions.Compiled), "😉"),

            // Sad / Cry: :(, :-(, =(, =-(, :'(
            (new Regex(@"(?:[:=]-?[\(\[]|:'-?\()", RegexOptions.Compiled), "😢"),

            // Tongue: :P, :-P, :p, :-p, =P, =p
            (new Regex(@"(?<!\d)(?:[:=]-?[pP])(?![a-zA-Z0-9])", RegexOptions.Compiled), "😛"),

            // Neutral: :|, :-|, =|, =/
            (new Regex(@"(?<![a-zA-Z0-9])(?:[:=]-?[/\\|])(?![a-zA-Z0-9])", RegexOptions.Compiled), "😐"),

            // Sunglasses: B), 8)
            (new Regex(@"(?<![a-zA-Z0-9])(?:[B8]-?\))(?![a-zA-Z0-9])", RegexOptions.Compiled), "😎")
        };

        public IReadOnlyList<ChatMessage> GetRecentMessages() {
            lock (_lock) {
                return _messages.ToList();
            }
        }

        public bool RemoveMessage(string messageId) {
            if (string.IsNullOrWhiteSpace(messageId)) return false;
            bool removed = false;
            string cleanId = messageId.Trim();
            lock (_lock) {
                removed = _messages.RemoveAll(m => m.Id.Equals(cleanId, StringComparison.OrdinalIgnoreCase)) > 0;
            }
            if (removed) {
                MessageRemoved?.Invoke(cleanId);
            }
            return removed;
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
