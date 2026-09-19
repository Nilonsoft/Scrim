using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using NLua;

namespace Scrim.Audio {
    public class VoiceEffectLoader {
        private readonly Dictionary<string, IVoiceChangerProvider> _providers = new();
        private readonly string _effectsDir;

        public IEnumerable<IVoiceChangerProvider> AvailableProviders => _providers.Values;

        public VoiceEffectLoader() {
            _effectsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VoiceEffects");
            if (!Directory.Exists(_effectsDir)) {
                Directory.CreateDirectory(_effectsDir);
            }

            // Load Built-Ins
            RegisterBuiltIn("normal", "Normal", VoiceEffect.Normal);
            RegisterBuiltIn("woman", "Woman", VoiceEffect.Woman);
            RegisterBuiltIn("man", "Man", VoiceEffect.Man);
            RegisterBuiltIn("robot", "Robot", VoiceEffect.Robot);
            RegisterBuiltIn("radio", "Radio", VoiceEffect.Radio);
            RegisterBuiltIn("alien", "Alien", VoiceEffect.Alien);
        }

        public void LoadEffects() {
            // Load DLLs
            foreach (var file in Directory.GetFiles(_effectsDir, "*.dll")) {
                try {
                    var asm = Assembly.LoadFrom(file);
                    foreach (var type in asm.GetTypes()) {
                        if (typeof(IVoiceChangerProvider).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract) {
                            if (Activator.CreateInstance(type) is IVoiceChangerProvider provider) {
                                _providers[provider.Id] = provider;
                            }
                        }
                    }
                } catch { /* Ignore loading errors for specific DLLs */ }
            }

            // Load JSONs
            foreach (var file in Directory.GetFiles(_effectsDir, "*.json")) {
                try {
                    var json = File.ReadAllText(file);
                    var doc = JsonDocument.Parse(json);
                    string name = doc.RootElement.GetProperty("name").GetString() ?? "Unknown";
                    string id = Path.GetFileNameWithoutExtension(file);
                    var chain = new JsonChainProvider(id, name, doc);
                    _providers[id] = chain;
                } catch { }
            }

            // Load Lua Scripts
            foreach (var file in Directory.GetFiles(_effectsDir, "*.lua")) {
                try {
                    string id = Path.GetFileNameWithoutExtension(file);
                    var luaProv = new LuaScriptProvider(id, file);
                    _providers[id] = luaProv;
                } catch { }
            }
        }

        public IVoiceChangerProvider? GetProvider(string id) {
            if (string.IsNullOrEmpty(id)) return _providers["normal"];
            return _providers.TryGetValue(id, out var p) ? p : _providers["normal"];
        }

        private void RegisterBuiltIn(string id, string name, VoiceEffect effect) {
            _providers[id] = new BuiltInVoiceChangerProvider(id, name, effect);
        }
    }

    public class BuiltInVoiceChangerProvider : IVoiceChangerProvider {
        public string Id { get; }
        public string Name { get; }
        private readonly VoiceEffect _effect;
        private readonly VoiceChangerProcessor _processor = new();

        public BuiltInVoiceChangerProvider(string id, string name, VoiceEffect effect) {
            Id = id;
            Name = name;
            _effect = effect;
        }

        public void Process(byte[] buffer) {
            _processor.Process(buffer, _effect);
        }
    }
}
