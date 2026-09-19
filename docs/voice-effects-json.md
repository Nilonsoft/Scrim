# Creating Custom Voice Effects (JSON Chains)

Scrim allows you to create your own custom voice effects by chaining together simple building blocks. This is the easiest way to make a custom voice effect, and you don't need any programming experience to do it!

## Getting Started

1. Navigate to the folder where Scrim is installed.
2. Open the `VoiceEffects` folder. If it doesn't exist, create it.
3. Create a new text file and name it something like `my-voice.json`. (Make sure the extension is `.json`, not `.json.txt`).
4. Open the file in Notepad or any text editor.

## The Format

Every JSON effect file must have a `name` and a `chain` of effects. Scrim will read the chain from top to bottom and apply each effect to your microphone audio in order.

Here is an example of a "Walkie Talkie" effect:

```json
{
  "name": "Walkie Talkie",
  "chain": [
    {
      "effect": "LowPass"
    },
    {
      "effect": "Distortion",
      "gain": 4.5
    }
  ]
}
```

## Available Effect Blocks

Currently, the JSON Chain engine supports the following effect blocks:

### 1. Distortion
Adds hard-clipping gain to your voice, making it sound loud, crackly, or overblown.
- **`gain`** (Number): The multiplier for your volume. `1.0` is normal. `4.5` is heavily distorted.

```json
{
  "effect": "Distortion",
  "gain": 2.0
}
```

### 2. LowPass
Muffles the audio slightly by removing high frequencies. It's a simple 1-pole filter, great for making you sound like you are talking through a cheap speaker or a wall.
- *(No configuration properties required)*

```json
{
  "effect": "LowPass"
}
```

## Troubleshooting

- **The effect doesn't show up in the dropdown**: Ensure your file ends in `.json` and is placed directly inside the `VoiceEffects/` directory. Check that your JSON syntax is valid (no missing commas or quotes).
- **The effect is completely silent**: You may have set your `gain` too low, or you have a typo in the `effect` name. Ensure the names exactly match the blocks listed above.
