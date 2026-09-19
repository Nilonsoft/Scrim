# Native Voice Effects (C# DLL Plugins)

For developers who need zero-latency, high-performance DSP audio pipelines, Scrim allows you to compile your own `.dll` assemblies and inject them directly into the audio loop using the `IVoiceChangerProvider` interface.

## Prerequisites
- .NET 10.0 SDK
- Visual Studio 2022 or JetBrains Rider
- Basic knowledge of DSP (Digital Signal Processing) mathematics.

## 1. Setup the Project
1. Create a new **Class Library** project targeting `.NET 10.0`.
2. Name it something like `MyCustomAudioEffects`.
3. You will need a reference to the core Scrim API. Add a reference to the `Scrim.dll` assembly located in your local Scrim installation folder.

## 2. Implementing the Provider
Create a new class and implement `Scrim.Audio.IVoiceChangerProvider`.

```csharp
using System;
using Scrim.Audio;

namespace MyCustomAudioEffects {
    public class DelayEffectProvider : IVoiceChangerProvider {
        // A unique, machine-readable ID
        public string Id => "custom_delay";
        
        // The human-readable name shown in the UI dropdown
        public string Name => "Echo / Delay";
        
        // Stateful variables for your DSP
        private readonly short[] _delayBuffer = new short[44100]; // 1 second buffer
        private int _writeIndex = 0;

        public void Process(byte[] buffer) {
            // Buffer is raw 16-bit PCM bytes.
            for (int i = 0; i < buffer.Length; i += 2) {
                short currentSample = BitConverter.ToInt16(buffer, i);
                
                // Read from delay buffer
                int readIndex = (_writeIndex - 22050 + _delayBuffer.Length) % _delayBuffer.Length; // 500ms delay
                short delayedSample = _delayBuffer[readIndex];
                
                // Mix
                int mixed = currentSample + (delayedSample / 2); // 50% feedback
                
                // Clamp
                if (mixed > short.MaxValue) mixed = short.MaxValue;
                if (mixed < short.MinValue) mixed = short.MinValue;
                
                short finalSample = (short)mixed;
                
                // Write back
                _delayBuffer[_writeIndex] = finalSample;
                _writeIndex = (_writeIndex + 1) % _delayBuffer.Length;
                
                byte[] bytes = BitConverter.GetBytes(finalSample);
                buffer[i] = bytes[0];
                buffer[i + 1] = bytes[1];
            }
        }
    }
}
```

## 3. Deployment
1. Build your Class Library in `Release` mode.
2. Navigate to your output `bin/Release/net10.0-windows/` folder.
3. Copy `MyCustomAudioEffects.dll`.
4. Paste the `.dll` into the `VoiceEffects/` directory located inside your Scrim application folder.
5. Launch Scrim. Your new effect will automatically appear in the Microphone card dropdown!

## Best Practices
- **Performance**: The `Process` method is called sequentially on the live audio thread. Any blocking operations (like `Task.Delay`, HTTP requests, or `Thread.Sleep`) will instantly halt the user's broadcast and cause severe audio stuttering.
- **Statefulness**: Your class is instantiated exactly **once** as a Singleton by the `VoiceEffectLoader`. Any variables or buffers you declare at the class level will persist throughout the life of the application.
