# SAM Broadcaster PRO Modular Suite in Scrim

Scrim provides professional, broadcast-grade features inspired by **SAM Broadcaster PRO**. 
To preserve a clean, uncluttered interface, **none of these cards are enabled by default**. Each feature is encapsulated as an independent modular card that you can enable, position, or close at any time through the **+ Add / Manage Cards** drawer.

---

## Modular Cards Overview

| Card ID | Card Title | Description |
|---|---|---|
| `card-dual-deck` | **Dual Decks & Crossfader** | Deck A / Deck B playback, CUE markers, pitch nudge (-8% to +8%), and equal-loudness crossfader with 1-click Auto-DJ transitions. |
| `card-event-scheduler` | **Automated Show Clock & Scheduler** | Timed top-of-hour station IDs, chat announcements, sweeps, and theme changes triggered hourly, daily, or on custom minute intervals. |
| `card-voice-tracking` | **Voice-Tracking Transition Recorder** | Record DJ talk-over clips with automated ducking over track intros and outros before going to air. |
| `card-multi-encoder` | **Multi-Encoder & Relay Rack** | Broadcast multiple concurrent audio codecs (MP3 128k, AAC+ 64k mobile, FLAC lossless) or relay to external Icecast / SHOUTcast servers. |
| `card-listener-inspector` | **Live Listener Connections & IP Inspector** | Real-time table of connected listener IPs, duration, data transferred, client player types, and 1-click listener disconnect/kick. |
| `card-silence-recovery` | **Dead-Air Auto-Recovery & Alarm** | Master output silence watchdog with configurable threshold and timeout countdown, auto-triggering backup jingles or audio alarms. |
| `card-rotation-rules` | **Music Rotation Rules & Category Bins** | Category crates (Heavy Current, Medium, Gold Classics, Sweepers), artist separation rules, and automated 1-hour clockwheel queue generator. |

---

## How to Enable and Arrange Cards

1. In the Scrim Studio Console, click **+ Add Card** at the bottom of any column (Left, Center, or Right), or click **Cards** in the top navigation bar.
2. In the card selection drawer, click any card to immediately spawn it in your console.
3. Drag any card by its header to reposition it anywhere across the 3 console columns.
4. Click the **✕** button on any card header to hide it without losing your settings. Layouts are automatically saved to your active profile in `~/.scrim/profiles/`.

---

## Feature Details

### 1. Dual Decks & Crossfader (`card-dual-deck`)
* **Track Input Sources & Loading**:
  * **Playlist Quick-Cueing (`[A]` / `[B]`)**: Tracks are loaded directly from the **Playlist & Console Player** card. Each row in your playlist features illuminated cue buttons:
    * Click **`[A]`** on any track to instantly load its Title, Artist, File Path, and Duration into **Deck A (Master)**.
    * Click **`[B]`** on any track to load it into **Deck B (Standby)**.
  * **File Association**: Decks maintain full awareness of the underlying audio file (`.mp3`, `.wav`, `.flac`, `.m4a`, `.aac`, `.wma`), track duration, and playback position.
* **Deck Controls & Pitch Bending**:
  * **Transport**: Independent **▶ Play**, **⏸ Pause**, and **■ Stop** buttons per deck.
  * **CUE Points**:
    * Clicking **CUE** sets a cue marker at the current playback position.
    * When playback is active, clicking **CUE** immediately pauses and rewinds the deck back to the marked cue position for precision beat drops.
  * **Pitch / Tempo Fader**: Fine-grained variable tempo adjustment from `-8.0%` to `+8.0%` with a 1-click `0%` center reset button.
* **Crossfader Curves & Equal-Loudness Math**:
  * The crossfader position slider ranges from `-1.0` (100% Deck A) through `0.0` (Center) to `+1.0` (100% Deck B).
  * **Constant Power (Recommended)**: Uses equal-loudness trigonometric quarter-circle curves:
    $$\text{Gain}_A = \cos\left((\text{Pos} + 1) \cdot \frac{\pi}{4}\right), \quad \text{Gain}_B = \sin\left((\text{Pos} + 1) \cdot \frac{\pi}{4}\right)$$
    The sum of power squares ($\text{Gain}_A^2 + \text{Gain}_B^2 = 1.0$) eliminates the hollow volume drop-outs common with basic linear crossfaders.
  * **Linear**: Direct arithmetic transition ($\text{Gain}_A = 1 - \text{norm}, \text{Gain}_B = \text{norm}$).
  * **Cut / Scratch**: Maintains full volume on the active deck until the crossfader reaches the extreme opposite edge, tailored for quick scratch cuts and hip-hop drop-ins.
* **1-Click Auto-DJ Crossfade**:
  * Clicking **`⚡ 1-Click Auto-DJ Crossfade`** triggers an automated 4-second crossfade transition.
  * It computes a smooth cubic S-curve ($3t^2 - 2t^3$) across the transition, ensures the standby deck is playing, sweeps the crossfader across to the opposing deck, and automatically stops the outgoing deck upon completion.

### 2. Automated Show Clock & Event Scheduler (`card-event-scheduler`)
* **Trigger Types**:
  * **Hourly Sweep**: Fires at a specific minute (e.g. `:00` top of hour or `:30` bottom of hour).
  * **Daily Show Clock**: Fires at an exact hour and minute (e.g. `18:00` evening show opener).
  * **Minute Interval**: Fires every $N$ minutes (e.g. promo announcements every 20 minutes).
* **Automated Actions**:
  * **Play Jingle**: Fires station sound bites and sweepers.
  * **Chat Announcement**: Posts host messages to the listener chat room.
  * **Switch Theme**: Automatically changes console & web themes for evening or special shows.
  * **Launch Poll**: Prompts listeners with live interactive track battle votes.

### 3. Voice-Tracking Transition Recorder (`card-voice-tracking`)
* Enables radio DJs to record dry voice clips and audition how their voice sounds ramped between the outgoing track outro and incoming track intro.
* Configurable ducking depth (default `-12 dB`) and smooth cross-ramp durations (`0.5s` to `4.0s`).
* Commit button seamlessly inserts the voice-track into the station broadcast queue.

### 4. Multi-Encoder & Relay Rack (`card-multi-encoder`)
* Run concurrent audio streams tailored for different audiences:
  * **Standard MP3 (128 kbps)**: High-compatibility stream for web players and desktop listeners.
  * **Mobile AAC+ (64 kbps)**: Ultra-low bandwidth high-efficiency audio for cellular listeners.
  * **FLAC Lossless (1411 kbps)**: Studio audiophile stream for high-end audio enthusiasts.
* **External Relay**: Push the live stream upstream to Icecast or SHOUTcast servers (configurable Host, Port, Mount, and Password).

### 5. Live Listener Connections & IP Inspector (`card-listener-inspector`)
* Inspect all currently connected listeners in real time.
* Displays remote IP address, listening session duration, total MB streamed, and client player User-Agent.
* **1-Click Kick**: Instantly terminate suspicious or abusive connections.

### 6. Dead-Air Auto-Recovery & Alarm (`card-silence-recovery`)
* Audio watchdog continuously monitors the master broadcast bus for energy drops below the silence threshold (default `-54 dB`).
* Live countdown timer displays remaining seconds before emergency recovery engages.
* Upon timeout, increments the session incident counter, sounds an audible console alarm, or triggers backup jingles.

### 7. Music Rotation Rules & Category Bins (`card-rotation-rules`)
* Organize music into category crates:
  * **Heavy Rotation (A-List)**: Top recurrent currents.
  * **Medium Rotation (B-List)**: Recent hits and rising tracks.
  * **Gold & Classics**: Deep library favorites.
  * **Sweepers & IDs**: Station imaging and legal IDs.
* **Artist Separation Limit**: Prevents the same artist from playing more than once within a configurable window (e.g. 45 minutes).
* **1-Hour Clock Generator**: Builds a mathematically balanced 1-hour broadcast queue with alternating power tracks, classics, and sweeps.
