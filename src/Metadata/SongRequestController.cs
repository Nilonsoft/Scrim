using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Scrim.Metadata {
    public class SongRequest {
        public string Id { get; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    }

    public class SongRequestController {
        private readonly ConcurrentQueue<SongRequest> _queue = new ConcurrentQueue<SongRequest>();

        public void SubmitRequest(string title, string artist) {
            _queue.Enqueue(new SongRequest { Title = title, Artist = artist });
        }

        public IEnumerable<SongRequest> GetLiveQueue() {
            return _queue.ToArray();
        }

        public void RemoveRequest(string id) {
            // Note: ConcurrentQueue doesn't support random removal, but we would rebuild it or use a different structure in a real scenario
        }
    }
}
