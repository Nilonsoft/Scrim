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

3. **Custom Public Domain / Host / IP Override**:
   - If you use Dynamic DNS (e.g. DuckDNS, No-IP) or run Scrim behind a custom reverse proxy or domain name, you can specify your custom hostname in the **Network & Internet Broadcasting** card:
     ```
     Custom Domain / Public Host (Optional)
     [ radio.mydomain.com:8080 ]
     ```
   - When configured, Scrim automatically formats and propagates this domain across:
     - The Outside Network URL box.
     - The **Share Broadcast** modal.
     - The **VLC & Media Players Endpoint** (`/stream`).
   - Click `✕ Reset` to revert immediately to your auto-detected public WAN IP.

4. **Direct Media Player Endpoint (`/stream`)**:
   - In addition to the interactive web player at the root URL (`/`), Scrim exposes a direct streaming audio endpoint at `/stream`.
   - This endpoint delivers chunked HTTP audio streams natively compatible with desktop and mobile media players:
     - **VLC Media Player**: Media → Open Network Stream (`Ctrl+N`) → paste `http://<Host>:<Port>/stream`.
     - **Winamp / foobar2000 / mpv**: File → Open URL.
     - **Mobile Streaming Apps & In-Car Web Receivers**.

5. **Console Management & Share Controls**:
   - **Network & Internet Broadcasting Card**: Allows broadcasters to toggle network access, toggle UPnP router forwarding, monitor UPnP forwarding status, enter custom domain overrides, and copy connection URLs with one click.
   - **Top Bar Quick Share**: When ON AIR, a "Share Stream" button in the top navigation bar opens a quick modal with direct links for local LAN and remote internet listeners.
   - **Persistence**: Network broadcasting, custom URLs, and UPnP preferences are persisted automatically to `~/.scrim/<Profile>.json`.

