using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Scrim.Metadata {
    public class SongRequest {
        public string Id { get; } = Guid.NewGuid().ToString();
        public string Query { get; set; } = string.Empty;
        public string Status { get; set; } = "Pending";
        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    }

    public class SongRequestController {
        private readonly ConcurrentDictionary<string, SongRequest> _queue = new();

        public void SubmitRequest(string query) {
            var req = new SongRequest { Query = query };
            _queue.TryAdd(req.Id, req);
        }

        public IEnumerable<SongRequest> GetLiveQueue() {
            return _queue.Values.OrderBy(q => q.RequestedAt).ToList();
        }

        public void UpdateStatus(string id, string status) {
            if (_queue.TryGetValue(id, out var req)) {
                req.Status = status;
            }
        }

        public void RemoveRequest(string id) {
            _queue.TryRemove(id, out _);
        }
    }
}
