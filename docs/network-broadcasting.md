# Network & Outside Network Broadcasting

Scrim provides built-in capabilities to broadcast your station over your local home network (Wi-Fi / LAN) and across the internet to remote listeners outside your local network.

## How It Works

1. **Dual-Mode HTTP Stream Server & Zero-Config Socket Bridge**:
   - Scrim binds its embedded HTTP streaming server (`HttpStreamServer`) to serve on all network interfaces.
   - On Windows, kernel-level `HTTP.sys` URL reservations normally require administrative privileges to bind non-localhost IPs. Scrim incorporates an automatic, zero-permission TCP socket bridge (`TcpListener`) fallback on the configured port.
   - This ensures that standard non-elevated user runs work seamlessly out of the box on all local IPv4/IPv6 addresses (`192.168.x.x`, `10.x.x.x`), custom domains, and Wi-Fi networks without requiring UAC prompts or returning `HTTP 400 Bad Request - Invalid Hostname`.
   - Listeners on the same Wi-Fi or LAN can connect immediately without special configuration by pointing their browser or media player to `http://<Local-IP>:<Port>`.

2. **Outside of Local Network (Internet) Access**:
   - Remote listeners outside of your local network can reach your stream using your public IP address: `http://<Public-IP>:<Port>`.
   - To route incoming traffic through your home router without manual router administration, Scrim includes native Windows UPnP NAT port mapping (`HNetCfg.NATUPnP`).
   - When **UPnP Router Port Forwarding** is enabled in Scrim, Scrim asks your router to automatically forward your station port to your local machine.

3. **Custom Public Domain / Host / IP Override & Reverse Proxy Settings**:
   - If you use Dynamic DNS (e.g. DuckDNS, No-IP) or run Scrim behind a reverse proxy (e.g. Caddy, Nginx, Apache, or Cloudflare Tunnel) with SSL/TLS, you can configure your host and connection options in the **Network & Internet Broadcasting** card:
     ```
     Custom Domain / Public Host (Optional)
     [ radio.mydomain.com ]
     [x] Use HTTPS (https://)     [x] Using Reverse Proxy (Remove Port)
     ```
   - **Use HTTPS**: Automatically switches generated outside URLs and playlists from `http://` to `https://`.
   - **Using Reverse Proxy**: Automatically removes the local server port (e.g. `:4242`) from generated outside URLs and playlists, making it seamless to copy/paste and open addresses managed by reverse proxies on standard HTTP/HTTPS ports (80/443).
   - When configured, Scrim automatically formats and propagates these preferences across:
     - The Outside Network URL box and "Open ↗" button.
     - The **Share Broadcast** modal.
     - The **VLC & Media Players Endpoint** (`/<mount>`).
     - Live `.m3u` and `.pls` playlist generators.
   - Click `✕ Reset` to revert immediately to your auto-detected public WAN IP.

4. **Configurable Live Stream URL Mount Path**:
   - The broadcaster can customize the audio streaming endpoint path (e.g. `/live`, `/radio`, `/metal`, or `/station`) directly in the **Network & Internet Broadcasting** console card.
   - Quick preset buttons (`stream`, `live`, `radio`, `station`) allow instant one-click switching.
   - For complete backwards compatibility with existing bookmarks, media player links, and PWAs, the default `/stream` path remains active as an automatic alias alongside the custom mount point.
   - Playlists (`.m3u` and `.pls`) automatically update to point to the configured mount point (e.g., `/radio.m3u`).

5. **Restrict to Local Network Only (Private Stream Mode)**:
   - When **Restrict to Local Network Only** is enabled in the Network card:
     - Listeners on the local Wi-Fi or LAN (`192.168.x.x`, `10.x.x.x`, `172.16.x.x`, `localhost`) continue to have full broadcasting and playback capabilities.
     - External/remote visitors connecting over the public internet see a full-page **"🔒 Private Stream"** banner masking the player with no access to audio, track metadata, queue, or live chat.
     - Direct audio requests from non-local IPs return `403 Forbidden` with a JSON explanation.

6. **Direct Media Player Endpoints & Playlists**:
   - In addition to the interactive web player at the root URL (`/`), Scrim exposes direct audio endpoints at `/<mount>` and `/stream`.
   - This endpoint delivers chunked HTTP audio streams natively compatible with desktop and mobile media players:
     - **VLC Media Player**: Media → Open Network Stream (`Ctrl+N`) → paste `http://<Host>:<Port>/<mount>`.
     - **Winamp / foobar2000 / mpv**: File → Open URL.
     - **M3U / PLS Playlists**: Direct download via `http://<Host>:<Port>/<mount>.m3u` or `/listen.m3u`.

7. **Console Management & Share Controls**:
   - **Network & Internet Broadcasting Card**: Allows broadcasters to toggle network access, toggle UPnP router forwarding, monitor UPnP forwarding status, configure live stream mount paths, toggle private LAN-only mode, enter custom domain overrides, and copy connection URLs with one click.
   - **Top Bar Quick Share**: When ON AIR, a "Share Stream" button in the top navigation bar opens a quick modal with direct links for local LAN and remote internet listeners.
   - **Persistence**: Network broadcasting, custom mount points, private LAN restriction, custom URLs, and UPnP preferences are persisted automatically to `~/.scrim/<Profile>.json`.

