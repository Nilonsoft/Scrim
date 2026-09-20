using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Scrim.Metadata {
    public class SongRequest {
        public string Id { get; } = Guid.NewGuid().ToString();
        public string Query { get; set; } = string.Empty;
        public string Dedication { get; set; } = string.Empty;
        public string Status { get; set; } = "Pending";
        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    }

    public class SongRequestController {
        private readonly List<SongRequest> _queue = new();
        private readonly object _lock = new();

        public event EventHandler? QueueChanged;

        public void SubmitRequest(string query, string dedication = "") {
            var req = new SongRequest { 
                Query = query?.Trim() ?? string.Empty,
                Dedication = dedication?.Trim() ?? string.Empty
            };
            lock (_lock) {
                _queue.Add(req);
            }
            QueueChanged?.Invoke(this, EventArgs.Empty);
        }

        public IEnumerable<SongRequest> GetLiveQueue() {
            lock (_lock) {
                return _queue.ToList();
            }
        }

        public void UpdateStatus(string id, string status) {
            bool changed = false;
            lock (_lock) {
                var req = _queue.FirstOrDefault(q => q.Id == id);
                if (req != null) {
                    req.Status = status;
                    changed = true;
                }
            }
            if (changed) {
                QueueChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void RemoveRequest(string id) {
            bool removed = false;
            lock (_lock) {
                int index = _queue.FindIndex(q => q.Id == id);
                if (index >= 0) {
                    _queue.RemoveAt(index);
                    removed = true;
                }
            }
            if (removed) {
                QueueChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void ClearQueue() {
            bool changed = false;
            lock (_lock) {
                if (_queue.Count > 0) {
                    _queue.Clear();
                    changed = true;
                }
            }
            if (changed) {
                QueueChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public bool MoveUp(string id) {
            bool moved = false;
            lock (_lock) {
                int index = _queue.FindIndex(q => q.Id == id);
                if (index > 0) {
                    var item = _queue[index];
                    _queue.RemoveAt(index);
                    _queue.Insert(index - 1, item);
                    moved = true;
                }
            }
            if (moved) {
                QueueChanged?.Invoke(this, EventArgs.Empty);
            }
            return moved;
        }

        public bool MoveDown(string id) {
            bool moved = false;
            lock (_lock) {
                int index = _queue.FindIndex(q => q.Id == id);
                if (index >= 0 && index < _queue.Count - 1) {
                    var item = _queue[index];
                    _queue.RemoveAt(index);
                    _queue.Insert(index + 1, item);
                    moved = true;
                }
            }
            if (moved) {
                QueueChanged?.Invoke(this, EventArgs.Empty);
            }
            return moved;
        }
    }
}
