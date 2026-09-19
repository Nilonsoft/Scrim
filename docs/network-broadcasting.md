# Network & Outside Network Broadcasting

Scrim provides built-in capabilities to broadcast your station over your local home network (Wi-Fi / LAN) and across the internet to remote listeners outside your local network.

## How It Works

1. **Multi-Prefix HTTP Stream Server**:
   - By default, Scrim binds its embedded HTTP streaming server (`HttpStreamServer`) to `localhost`, all active local IPv4 addresses (`192.168.x.x`, `10.x.x.x`), and optionally a wildcard prefix (`http://+:{port}/`).
   - Listeners on the same Wi-Fi or LAN can connect immediately without special configuration by pointing their browser or media player to `http://<Local-IP>:<Port>`.

2. **Outside of Local Network (Internet) Access**:
   - Remote listeners outside of your local network can reach your stream using your public IP address: `http://<Public-IP>:<Port>`.
   - To route incoming traffic through your home router without manual router administration, Scrim includes native Windows UPnP NAT port mapping (`HNetCfg.NATUPnP`).
   - When **UPnP Router Port Forwarding** is enabled in Scrim, Scrim asks your router to automatically forward your station port to your local machine.

3. **Console Management & Share Controls**:
   - **Network & Internet Broadcasting Card**: Allows broadcasters to toggle network access, toggle UPnP router forwarding, monitor UPnP forwarding status, and copy connection URLs with one click.
   - **Top Bar Quick Share**: When ON AIR, a "Share Stream" button in the top navigation bar opens a quick modal with direct links for local LAN and remote internet listeners.
   - **Persistence**: Network broadcasting and UPnP preferences are persisted automatically to `~/.scrim/<Profile>.json`.
