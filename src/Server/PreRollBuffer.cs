using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Scrim.Server {
    public class PreRollBuffer {
        private readonly ConcurrentQueue<byte[]> _bufferQueue = new ConcurrentQueue<byte[]>();
        private int _totalBytes = 0;
        private readonly int _maxBytes;

        public PreRollBuffer(int maxBytes = 32000) {
            _maxBytes = maxBytes;
        }

        public void PushFrame(byte[] frame) {
            _bufferQueue.Enqueue(frame);
            _totalBytes += frame.Length;

            while (_totalBytes > _maxBytes && _bufferQueue.TryDequeue(out byte[]? oldFrame)) {
                _totalBytes -= oldFrame.Length;
            }
        }

        public IEnumerable<byte[]> GetBurst() {
            return _bufferQueue.ToArray();
        }
    }
}
