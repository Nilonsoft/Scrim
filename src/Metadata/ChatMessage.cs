using System;

namespace Scrim.Metadata {
    public class ChatMessage {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Sender { get; set; } = "Anonymous";
        public string Text { get; set; } = "";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public bool IsHost { get; set; } = false;
        public string Color { get; set; } = "#00d2ff";
        public string? UserId { get; set; }
    }
}
