using System;
using NLua;
using System.IO;

namespace Scrim.Audio {
    public class LuaScriptProvider : IVoiceChangerProvider, IDisposable {
        public string Id { get; }
        public string Name { get; }
        
        private readonly Lua _lua;
        private readonly LuaFunction? _processFunc;

        public LuaScriptProvider(string id, string filePath) {
            Id = id;
            Name = Path.GetFileNameWithoutExtension(filePath);
            
            _lua = new Lua();
            _lua.LoadCLRPackage();
            _lua.DoFile(filePath);
            
            _processFunc = _lua["processBuffer"] as LuaFunction;
            
            if (_lua["name"] is string n) {
                Name = n;
            }
        }

        public void Process(byte[] buffer) {
            if (_processFunc == null) return;
            
            // To prevent massive overhead, we normally wouldn't call Lua per-sample.
            // We pass the entire byte array to Lua and let it modify it if it knows how,
            // but interop with byte[] can be tricky in NLua.
            // For this implementation, we pass the raw byte[] reference.
            try {
                _processFunc.Call(buffer);
            } catch {
                // If the script throws, we swallow it to avoid killing the audio thread.
            }
        }

        public void Dispose() {
            _lua?.Dispose();
        }
    }
}
