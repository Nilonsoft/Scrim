# Advanced Voice Effects (Lua Scripting)

For power users and audio enthusiasts who want programmatic control over their audio without needing to compile C# assemblies, Scrim supports Lua scripting via the NLua engine.

This allows you to write raw mathematical DSP (Digital Signal Processing) logic that runs directly on your microphone's byte stream.

## Getting Started

1. Navigate to your Scrim installation folder.
2. Open the `VoiceEffects` folder.
3. Create a file named `my-script.lua`.
4. Open the file in your preferred code editor.

## The Format

Your Lua script must define a global function called `processBuffer(buffer)`. Scrim will pass the raw 16-bit PCM byte array into this function 100 times a second. You also can optionally define a global string `name` to customize how it appears in the dropdown.

### Example: Simple Volume Booster
```lua
name = "Lua Volume Booster"

function processBuffer(buffer)
    -- The buffer is a raw C# byte[] representing 16-bit PCM audio.
    local length = buffer.Length
    
    -- Iterate by 2 because 16-bit audio takes 2 bytes per sample.
    for i = 0, length - 2, 2 do
        -- NLua allows us to call BitConverter directly if the namespace is imported,
        -- but because we are dealing with raw arrays, it's easier to do the math manually.
        -- Note: Direct byte manipulation in Lua can be slow. 
        -- In advanced implementations, you'll use CLR bridging.
    end
end
```

> [!WARNING]
> **Performance Warning**: Lua is an interpreted language. The `processBuffer` function is executing continuously on a high-priority audio thread. If your Lua script contains complex loops, heavy allocations, or blocking operations, you **will** cause audio stuttering and massive latency. Keep your scripts extremely lightweight.

## CLR Interop

NLua allows you to access native C# classes. You can import C# libraries to assist with math:

```lua
import ('System')

name = "Math Modulator"

local phase = 0

function processBuffer(buffer)
    local sampleCount = buffer.Length / 2
    local sampleRate = 44100.0
    
    for i = 0, buffer.Length - 1, 2 do
        local sample = BitConverter.ToInt16(buffer, i)
        local normalized = sample / 32768.0
        
        -- Apply a 100Hz ring modulator
        local mod = Math.Sin(phase)
        normalized = normalized * mod
        
        phase = phase + (2.0 * Math.PI * 100.0 / sampleRate)
        if phase > (2.0 * Math.PI) then
            phase = phase - (2.0 * Math.PI)
        end
        
        local processed = math.floor(normalized * 32767.0)
        local bytes = BitConverter.GetBytes(processed)
        
        buffer[i] = bytes[0]
        buffer[i+1] = bytes[1]
    end
end
```
