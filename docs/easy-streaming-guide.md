# Easy Streaming Guide: Stream Without Ports Using Caddy & Free HTTPS

This beginner-friendly guide walks you step-by-step through setting up **Scrim** with a clean, professional web address (like `https://myradio.duckdns.org`) that has **no port numbers** (`:4242`) and **free automatic HTTPS** with a trusted green security lock.

---

## Why Use This Setup?

Normally, when you stream directly from your PC:
- Visitors have to type an ugly port number at the end of the address (e.g. `http://123.45.67.89:4242`).
- Web browsers like Chrome and Safari will display a **"Not Secure"** warning badge because standard audio streams lack an SSL certificate.

By adding **Caddy** (a tiny, free, lightweight helper program) in front of Scrim:
- ✅ **No Port Numbers**: Listeners connect on standard web ports (`https://yourname.duckdns.org`).
- ✅ **Automatic Free SSL Certificate**: Caddy automatically requests and renews a free Let's Encrypt SSL padlock.
- ✅ **Works with Scrim's Web Player & Chat**: Your real-time station player, live chat, visualizers, and listener song requests work seamlessly over encrypted HTTPS.

---

## Step 1: Get a Free Domain Name (2 Minutes)

You need a web address to give to your listeners. The easiest, completely free way to get one is with **DuckDNS**:

1. Go to **[duckdns.org](https://www.duckdns.org/)**.
2. Sign in with your Google, Reddit, or GitHub account.
3. Under **domains**, type a name for your station (for example, `mycoolradio`) and click **add domain**.
4. DuckDNS will detect your public IP address automatically and link it to `mycoolradio.duckdns.org`.

> [!TIP]
> If you already own a custom domain name (e.g., through Namecheap, Cloudflare, or GoDaddy), you can create an `A` record pointing `radio.yourdomain.com` directly to your home's public IP address instead!

---

## Step 2: Download Caddy

Caddy is a single file program that requires no complex installation.

### Option A: Official Download (Recommended)
1. Visit the **[Official Caddy Download Page](https://caddyserver.com/download)** (or [Caddy GitHub Releases](https://github.com/caddyserver/caddy/releases)).
2. Select the **Windows (amd64)** build and click **Download**.
3. Create a new folder on your computer, such as `C:\Caddy\`.
4. Move the downloaded `caddy.exe` into `C:\Caddy\`.

### Option B: 1-Click Install via Windows Terminal / PowerShell
If you prefer using the command line, open PowerShell and run:
```powershell
winget install CaddyServer.Caddy
```

---

## Step 3: Create Your 3-Line Configuration File (`Caddyfile`)

Caddy uses a tiny text file called `Caddyfile` (with no file extension) to know where to direct traffic.

1. In the same folder where `caddy.exe` is located (e.g. `C:\Caddy\`), create a new text file named `Caddyfile`.
2. Open `Caddyfile` in Notepad and paste the following 3 lines (replace `mycoolradio.duckdns.org` with your actual domain name from Step 1):

```caddy
mycoolradio.duckdns.org {
    reverse_proxy localhost:4242
}
```

> [!NOTE]
> That's all Caddy needs! This tells Caddy: *"Whenever someone visits `mycoolradio.duckdns.org`, automatically set up a free HTTPS certificate and forward their audio connection to Scrim running on port 4242 on this computer."*

3. Save the file.
4. Open a PowerShell or Command Prompt window in your `C:\Caddy\` folder and run:
```powershell
caddy run
```
You will see Caddy start up, verify your domain name, and fetch your free SSL certificate automatically!

---

## Step 4: Allow Traffic in Your Home Router (Port 80 and 443)

Because visitors will now connect on standard web ports instead of port 4242, you need to open web ports **80** (HTTP) and **443** (HTTPS) on your home router and direct them to your PC:

1. Log into your home Wi-Fi router's admin panel (usually `192.168.1.1` or `192.168.0.1` in your web browser).
2. Look for **Port Forwarding**, **Virtual Server**, or **NAT Gaming** settings.
3. Add two forwarding rules pointing to your PC's local IP address (e.g. `192.168.1.50`):
   - **Port 80 (TCP)** → Forward to your PC's IP port 80.
   - **Port 443 (TCP)** → Forward to your PC's IP port 443.
4. Save your router settings.

> [!TIP]
> Not sure what your PC's local IP is? Look at Scrim's **Network & Internet Broadcasting** card—Scrim displays your exact local network IP right inside the console!

---

## Step 5: Configure Scrim (The 30-Second Setup)

Now tell Scrim that you are using Caddy so that all generated links and share buttons show your clean address:

1. Launch **Scrim** and look at the **Network & Internet Broadcasting** card.
2. In the **Custom Domain / Public Host** box, type your domain name:
   ```text
   mycoolradio.duckdns.org
   ```
3. Check the box for **`[x] Use HTTPS`**:
   - A green **HTTPS** badge will appear.
4. Check the box for **`[x] Using Reverse Proxy`**:
   - A cyan **NO PORT** badge will appear.
5. Click **`🔴 ON AIR`**!

---

## Step 6: Test Your Broadcast!

Look at the **Outside Network URL** box in Scrim. You will see your clean broadcast address:

```text
https://mycoolradio.duckdns.org/
```

- **Test on Your Phone**: Turn off Wi-Fi on your smartphone (so you are testing over mobile cellular data) and navigate to `https://mycoolradio.duckdns.org/`. Your Scrim live station player and chat should load with a secure green padlock!
- **1-Click Share**: Click **Share Stream** in Scrim's top navigation bar to copy clean `https://` links for friends or post them to social media.
- **Media Player Links**: VLC and Winamp stream endpoints automatically format cleanly as `https://mycoolradio.duckdns.org/live` (or your chosen mount point) with no port numbers needed.

---

## Keeping Caddy Running in the Background

To make Caddy run silently whenever you boot Windows:

1. Open PowerShell as Administrator.
2. Navigate to your Caddy directory:
   ```powershell
   cd C:\Caddy
   ```
3. Run:
   ```powershell
   caddy start
   ```
   `caddy start` launches Caddy as a background process so you can close the terminal window and your station stays online.

---

## Troubleshooting & Quick Fixes

- **"Browser says Connection Refused / Site Cannot Be Reached"**:
  1. Confirm that Caddy is actively running (`caddy run`).
  2. Confirm that Scrim is **ON AIR** (broadcasting).
  3. Double-check that ports **80** and **443** are forwarded in your router to your PC's local IP address.
  4. Ensure Windows Defender Firewall allows incoming connections on port 80 and 443 for Caddy.

- **"My ISP uses CGNAT (Carrier-Grade NAT) and doesn't give me a public IP"**:
  If your ISP uses CGNAT and router port forwarding does not reach your home, you can use **Cloudflare Tunnels** (which is also free and does not require opening any router ports at all). Scrim's **`[x] Use HTTPS`** and **`[x] Using Reverse Proxy`** settings work identically with Cloudflare Tunnels!
