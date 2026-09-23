# Multi-Station Console & Channel Dial Architecture

Scrim provides native multi-station radio broadcasting from a single running console instance on a single port. Broadcasters can run multiple concurrent radio stations simultaneously, each with completely isolated audio pipelines, distinct web player front-ends, independent branding, chat rooms, listener request queues, and streaming mounts—with **zero virtual audio cables required**.

---

## 1. Zero-Cable Multi-Station Audio Routing

Traditional broadcast setups require complex matrices of third-party virtual audio cables (e.g. VB-Audio Cable A, B, C) to run multiple streams. Scrim completely eliminates this complexity by utilizing direct Windows WASAPI Process-Loopback capture and dedicated station pipelines:

| Station | Typical Audio Source | Ingest Mechanism | Example Stream Mount | Web Player URL |
| :--- | :--- | :--- | :--- | :--- |
| **Station 1** | Spotify Desktop | WASAPI Process Loopback (`Spotify.exe`) | `/spotify` | `http://localhost:4242/?station=spotify` |
| **Station 2** | Built-in Music Player | Local Clockwheel & Automation Crates | `/vault` | `http://localhost:4242/?station=vault` |
| **Station 3** | Google Chrome / Browser | WASAPI Process Loopback (`chrome.exe`) | `/chrome` | `http://localhost:4242/?station=chrome` |

Each station pipeline runs its own:
- Dedicated **BroadcastHub** tracking client connections and listeners.
- Dedicated **AudioDuckingMixer** mixing the station's audio source with optional live DJ microphone ducking.
- Dedicated **MultiFormatTranscoder** encoding on-the-fly to MP3, AAC, Opus, or lossless FLAC.
- Dedicated **SongRequestController**, **LiveChatService**, **SongReactionService**, and **SongHistoryService**.

---

## 2. Console Station Switcher Tab Bar

In the Scrim Dashboard, a persistent Station Switcher Bar sits directly beneath the top header:

- **Station Tabs**: Each tab displays a live status dot (`🟢 ON AIR` / gray `OFFLINE`), the station's display name, and its URL mount path (e.g., `/spotify`).
- **Independent On-Air Controls**: Broadcasters can start or stop individual stations independently via the **ON AIR / GO LIVE** button on each tab, or use the global master transmitter control.
- **Context-Sensitive Cards**: Clicking a station tab switches the active editing context of the console cards (Audio Routing, Web Branding, Song Requests, Live Chat, and Queue) to configure that specific station.
- **Add Station Modal**: Broadcasters can add unlimited new stations with custom display names, URL mount paths, and audio sources.

---

## 3. Web Player Channel Dial

Listeners browsing the Scrim Web Player get an interactive, real-time **Channel Dial** in the top navigation bar:

- **Dynamic Discovery (`/api/stations`)**: On page load, the web player queries `/api/stations` to fetch all available stations, their live transmission statuses, current listener counts, genres, themes, and stream URLs.
- **1-Click Channel Surfing**: Clicking any channel pill in the dial smoothly hot-swaps the audio stream, reconnects Server-Sent Events (SSE), and updates branding, track metadata, chat messages, and reactions without page reloads.
- **Direct Link Support**: Listeners can bookmark or share direct links with the `?station=` query parameter (e.g. `http://scrim.fm/?station=vault`) or direct media player mounts (e.g. `http://scrim.fm/vault` in VLC, Winamp, or mobile apps).

---

## 4. Multi-Mount HTTP & SSE API Routing

All stations share the same configured HTTP port (default: `4242`) using path and query-based routing:

- **Audio Streams**: `GET /{mount}` or `GET /{mount}.mp3` (e.g., `/spotify`, `/vault`, `/chrome`).
- **Server-Sent Events (SSE)**: `GET /api/events?station={mount}` or `GET /api/{mount}/events`.
- **Branding**: `GET /api/branding?station={mount}` or `GET /api/{mount}/branding`.
- **Chat & Moderation**: `GET /api/chat?station={mount}` & `POST /api/chat?station={mount}`.
- **Reactions**: `GET /api/reactions?station={mount}` & `POST /api/reactions?station={mount}`.
- **Song Requests**: `POST /api/requests?station={mount}`.
- **Station Directory**: `GET /api/stations` (JSON array of all configured stations).
- **PWA Manifests**: `GET /manifest.webmanifest?station={mount}` (dynamic, station-scoped Web App Manifest).

---

## 5. Dynamic Station-Scoped PWA Mobile Web Apps

Each station functions as an independent Progressive Web App (PWA) on iOS and Android:

- **Isolated App Identity (`id`)**: The server dynamically generates distinct W3C Web App Manifests per station (`/pwa/{mount}`), preventing mobile operating systems from overwriting different stations under the same origin.
- **Dedicated Start URLs**: When a user adds a channel to their home screen (e.g. from `?station=vault`), the mobile app launches in fullscreen standalone mode directly into that station's stream and chat room (`/?station=vault`).
- **Independent Home Screen Icons & Names**: Each station displays its own custom station name, icon/logo, and theme accent color on the listener's phone home screen.
- **Side-by-Side Installation**: Listeners can install multiple stations from the same broadcaster (e.g., *StreamSource Live*, *The Vault 80s*, and *Chrome Radio*) as separate apps side-by-side on their home screen.
