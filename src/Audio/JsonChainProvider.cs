using System;
using System.Text.Json;

namespace Scrim.Audio {
    public class JsonChainProvider : IVoiceChangerProvider {
        public string Id { get; }
        public string Name { get; }
        private readonly JsonDocument _config;
        private double _lastSample = 0; // simple state for filters

        public JsonChainProvider(string id, string name, JsonDocument config) {
            Id = id;
            Name = name;
            _config = config;
        }

        public void Process(byte[] buffer) {
            if (!_config.RootElement.TryGetProperty("chain", out var chain)) return;

            for (int i = 0; i < buffer.Length; i += 2) {
                short sample = BitConverter.ToInt16(buffer, i);
                double normalized = sample / 32768.0;

                foreach (var effect in chain.EnumerateArray()) {
                    string type = effect.GetProperty("effect").GetString() ?? "";
                    
                    if (type == "Distortion") {
                        double gain = effect.TryGetProperty("gain", out var g) ? g.GetDouble() : 1.0;
                        normalized *= gain;
                        if (normalized > 1.0) normalized = 1.0;
                        if (normalized < -1.0) normalized = -1.0;
                    } 
                    else if (type == "LowPass") {
                        // simple 1-pole lowpass
                        double alpha = 0.3; // arbitrary hardcoded mapping for demo
                        normalized = _lastSample + alpha * (normalized - _lastSample);
                        _lastSample = normalized;
                    }
                    // Additional DSP blocks would be defined here
                }

                short processed = (short)(normalized * 32767.0);
                byte[] bytes = BitConverter.GetBytes(processed);
                buffer[i] = bytes[0];
                buffer[i + 1] = bytes[1];
            }
        }
    }
}
