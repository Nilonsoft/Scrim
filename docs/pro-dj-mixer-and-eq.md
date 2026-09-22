# Professional DJ Stereo Mixer & Full EQ Guide

Scrim includes a native, hardware-class **DJ Stereo Channel Mixer and Multi-Band Equalizer** built directly into the real-time 44.1kHz audio DSP pipeline. It delivers studio-grade sound shaping, true stereo channel manipulation, and tactile frequency isolation inspired by club-standard DJ hardware and professional DJ software (such as Pioneer DJM, Traktor Pro, and Serato DJ).

---

## 1. Overview & Capabilities

Unlike conventional desktop players that only offer a basic global volume slider, Scrim provides **discrete channel architecture**, **Mid-Side stereo spatial imaging**, **3-Band Isolator / Kill filters**, **10-Band ISO Graphic EQ**, **DJ Sound Color sweep filters**, and **analog soft-saturation limiting**.

All processing runs natively in real time with sub-millisecond latency. Any EQ adjustments, channel trims, or spatial expansion are encoded directly by the `MultiFormatTranscoder` and broadcast in pristine stereo to all web listeners, Icecast mounts, and external media players.

---

## 2. Channel Architecture

### 🎵 Music Deck (Channel 1)
- **Trim / Gain Fader**: `-12.0 dB` to `+12.0 dB` pre-fader gain adjustment for balancing track loudness.
- **Constant-Power Stereo Panning**: Smooth Left-to-Right panning with constant acoustic power (`-100% L` to `+100% R`) and a quick center detent reset (`C`).
- **Stereo Width Expander (Mid-Side Matrix)**:
  - `0.0x (MONO)`: Sums Left and Right channels equally into True Mono. Essential for club sound system compatibility testing and mono radio checks.
  - `1.0x (STEREO)`: Completely transparent, natural stereo pass-through.
  - `1.0x - 2.0x (WIDE)`: Expands the stereo soundstage by selectively amplifying the Side channel relative to the Mid channel. Delivers an expansive, immersive spatial image in headphones and stereo speakers.
- **Mute & Solo**: Instantly silences or isolates the music channel during talk segments or drops.

### 🎙️ Microphone Deck (Channel 2)
- **Mic Gain**: `-12.0 dB` to `+12.0 dB` level adjustment.
- **Mic Pan**: Positions the host/broadcaster voice in the stereo panorama.
- **80Hz Rumble Cut (High-Pass Filter)**: High-pass filter tuned to 80Hz that removes desk vibrations, air conditioner hum, and vocal plosives without affecting speech warmth.
- **Live Talkover Status**: Clear visual confirmation of microphone live / muted state.

### 🔊 Master Output Bus
- **Master Gain**: Final output level adjustment from `-12.0 dB` to `+6.0 dB`.
- **Master Balance**: Final master panorama balance.
- **Analog Soft-Clip Limiter**: Uses transparent hyperbolic tangent (`tanh`) saturation. If a DJ pushes EQ bands or gain beyond 0dBFS, the limiter rounds peaks into warm analog saturation rather than harsh, jagged digital clipping.

---

## 3. Equalizer Modes

### Mode A: 3-Band DJ Isolator with Kill Switches
Engineered specifically for DJ transitions, live track battles, and electronic music performance:
1. **HIGH Shelf**: 2.5 kHz to 20 kHz (`-26 dB` to `+6 dB`).
2. **MID Peaking**: 300 Hz to 2.5 kHz (`-26 dB` to `+6 dB`).
3. **LOW Shelf**: 20 Hz to 300 Hz (`-26 dB` to `+6 dB`).
4. **Kill Switches**:
   - **`[LOW KILL]`**: Drops low frequencies to `-inf dB`. Drops the bassline completely for seamless beatmatched transitions and drop build-ups.
   - **`[MID KILL]`**: Cuts vocals, leads, and snares, creating instant dub instrumental sections.
   - **`[HIGH KILL]`**: Isolates bass and drums by eliminating cymbals and high-frequency air.
5. **DJ Sound Color Sweep Filter (HPF / LPF)**:
   - **Turned Left (`-100%` to `0%`)**: Resonant Low-Pass Filter sweeping cutoff down to 160Hz with resonant analog edge.
   - **Center (`0%`)**: Completely bypassed.
   - **Turned Right (`0%` to `+100%`)**: Resonant High-Pass Filter sweeping cutoff up to 3.5kHz.

### Mode B: 10-Band ISO Graphic Equalizer
For surgical broadcast audio tuning across the entire audible frequency spectrum:
- **10 ISO Standard Bands**: `31 Hz`, `63 Hz`, `125 Hz`, `250 Hz`, `500 Hz`, `1 kHz`, `2 kHz`, `4 kHz`, `8 kHz`, `16 kHz`.
- Each band offers precision `+/- 12 dB` boost/cut with vertical faders and numeric readouts.

---

## 4. Real-Time Visual Feedback

- **Live SVG Response Curve**: A real-time vector curve displaying the combined filter response curve across the entire frequency range (20Hz to 20kHz) with an illuminated gradient fill.
- **Dual Stereo Peak VU Meters**: Real-time Left and Right level meters tracking peak amplitudes with yellow warning and redline overload indicators.

---

## 5. Pro DJ Presets

Scrim includes instant 1-click presets covering standard broadcast and club performance styles:
- **Flat / Bypass**: Transparent, uncolored frequency response.
- **Club / EDM Bass**: Heavy sub-bass boost with clean mid scoop and sparkling highs.
- **Radio DJ Voice**: Broadcast vocal presence boost with high-end air and low-end clarity.
- **Rock & Live**: Punchy mid-range presence for guitars, live drums, and punchy snares.
- **Warm Vinyl & Acoustic**: Analog saturation warmth with gentle high-end attenuation.
- **Hip-Hop Thump**: Deep, rumbling low-end response with crisp vocal cut.
- **Lofi Lounge**: Warm, vintage, mellow response.
- **Treble Sparkle**: Ultra-crisp top-end clarity.
