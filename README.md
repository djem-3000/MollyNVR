# MollyNVR

Local live video surveillance in a single Docker container: ASP.NET Core (.NET 10) + React, the go2rtc media engine and FFmpeg. The v1 scope below covers live viewing, recording and archive playback.

## Features

- ONVIF and Hikvision cameras: probing, main/sub profiles, snapshots. The ONVIF module also serves Reolink and other compatible cameras.
- Camera discovery: multicast (WS-Discovery, SSDP/UPnP, mDNS, Hikvision SADP), unicast subnet/CIDR/range scanning and single IP checks; a four-step wizard with main stream and substream selection.
- Camera registry in SQLite; credentials are encrypted with AES-GCM; camera settings cover the name, substream, device and stream details.
- "Live": layouts 1, 2, 4 (2x2), 4 with a 3x3 tile, 6 (3x2), 6 with two large tiles, 9, 12 with a large tile and 12 even — selected through the grid icon with a popover; buttons for image stretching and whole-grid fullscreen (exit with `Esc`).
- Active cameras fill the free tiles automatically; the tile order can be changed by dragging and is stored separately for each layout.
- Click focuses a tile and enables its sound; double click expands or collapses a tile to fullscreen with the main stream.
- Mouse wheel zooms the image under the cursor; the zoomed image is dragged with the left button.
- When the cursor is in the bottom third of a tile, round "Info" and "Settings" buttons appear smoothly at the bottom center; the settings page shows a preview with sound, device details (including DHCP), the name, a stream table with codec color statuses, a substream selector and a ▶ preview button.
- Tiles reconnect automatically after a connection loss or a media engine restart; "connecting" and "error" states are shown and the Live indicator is hidden.
- Volume: a global mute to the left of the layout picker and a Windows 11 style slider on the active tile; muted or unsupported audio codecs are marked with a red icon.
- Transcoding only when needed: H.265 → H.264 (NVENC) and AAC → Opus when the browser does not support the source codec.
- Layout, camera order, focus, stretch and global mute are stored in the browser (`localStorage`) and never on the server.
- PWA: manifest, service worker (offline shell) and icons; it installs as an app on a phone. Installation requires HTTPS with a trusted certificate (see below). On mobile there is a hamburger menu, a single-column camera list and a sorting dialog.

## Quick start

Requirements: Docker Desktop (WSL2) and access to the cameras on the local network. Hardware transcoding needs an NVIDIA GPU and driver; without a GPU, H.265 video may not play in a browser that does not support it.

1. Create `.env` and set the host LAN address:

   ```powershell
   Copy-Item .env.example .env
   notepad .env
   ```

   `HOST_LAN_IP` is the IPv4 address of the computer on the local network (for example, `192.168.1.151`). Browsers use it as the WebRTC ICE candidate.

2. Build and start the containers (the application and the Reolink bridge):

   ```powershell
   docker compose up -d --build
   ```

3. To install the app on a phone, issue a local certificate (requires `mkcert`):

   ```powershell
   .\scripts\make-cert.ps1 -HostIp <HOST_LAN_IP>
   ```

   The script creates `./certs` (not committed to git) and `rootCA.crt`. Install this root certificate on the phone (Android: Settings → Security → Encryption & credentials → Install a certificate → CA certificate).

4. Open http://localhost:9080 (or `http://<HOST_LAN_IP>:9080` from another LAN device) or use HTTPS at `https://<HOST_LAN_IP>:9443`. On a phone, open the HTTPS address and choose "Install app" / "Add to Home screen" in the Chrome menu.

5. Open "Cameras" → "+ Add". On the "Find camera" step choose Multicast, Unicast (CIDR/range) or IP address and click "Scan". In the found row click "Connect", enter the login and password, change the substream if needed on the "Profiles" step, set the name on the "Save" step and click "Add camera".

6. The camera appears in "Live". Settings are opened with the pencil button in the camera list or with the "Settings" button on the tile; if the camera should not be in the grid, disable it in the camera list. The "Back" button on the settings page returns to the previous page.

## Local run (without Docker)

Needed to verify **multicast discovery**: inside a bridge container WS-Discovery/SSDP/mDNS/SADP cannot see the local network. On the host the application gets the LAN directly.

Requirements: .NET 10 SDK and `tools/go2rtc.exe` (already in the repository). For Reolink cameras over the Baichuan protocol — `tools/neolink/neolink.net.dll` (files from the [neolink.net](https://github.com/borexola/neolink.net/releases) release); without it the Reolink module is unavailable, everything else works.

1. Stop the container to free port 9080 and avoid locking the database:

   ```powershell
   docker compose stop
   ```

2. Start the application on the host:

   ```powershell
   .\scripts\run-local.ps1
   ```

   The script builds the UI (`ui/dist`), serves it from ASP.NET Core, uses the shared `data/` directory (the same database and encryption key) and starts go2rtc from `tools/`. The UI opens at http://localhost:9080.

3. Check multicast on the "Find camera" step. After testing, bring the container back:

   ```powershell
   docker compose start
   ```

Script parameters: `-Port`, `-WebRtcPort`, `-RtspListen`, `-DataDirectory`, `-NoBuild` (do not rebuild .NET), `-SkipUiBuild` (use the existing `ui/dist`). To run on another port without stopping the container, use a separate data directory, for example `.\scripts\run-local.ps1 -Port 9081 -WebRtcPort 8556 -DataDirectory .\data-local`.

## Reolink cameras without RTSP/ONVIF (Baichuan bridge)

Some Reolink models (for example, E1 with HTTP/ONVIF/RTSP disabled) expose only port **9000** — the proprietary Baichuan protocol. For them compose includes a separate service `reolink`: [neolink.net](https://github.com/borexola/neolink.net) (AGPL-3.0), which connects to the camera over Baichuan and exposes regular RTSP.

- In the wizard select the vendor **Reolink**: the application tries ONVIF first, then Baichuan through the bridge.
- The camera is added to `data/reolink/config.json` automatically; the bridge restarts and picks it up (a few seconds).
- Streams are served as `rtsp://reolink:8654/cam-<ip>/mainStream` and `/subStream` inside the compose network and are not published externally.
- The camera password is stored in that file in plain text — otherwise the bridge cannot sign in. The `data/` directory is excluded from git.
- In a local run the bridge is started by `scripts/run-local.ps1` when `tools/neolink/neolink.net.dll` is present.
- The bridge record name is `cam-<ip>`; when the camera IP changes, a new record appears (the old one can be removed from `data/reolink/config.json`).
- Limitations: with HTTP disabled on the camera there are no image settings and no "light" snapshots (the frame is taken from the camera over Baichuan). Details and the license are in `docs/third-party/neolink.net.md`.

## Video recording

Configuration is on the "Camera settings" page, the **"Recording"** block:

- the master switch and the stream to record (any camera profile);
- **continuous recording** — a 7x24 schedule: weekdays starting on Monday, hourly slots (0:00-24:00), selected with the mouse;
- **event recording** — detection types reported by the camera (ONVIF PullPoint / Hikvision alertStream), its own schedule, and a margin **before** and **after** the event (0/15/30/60 s, 0 by default).

The storage pool is on the "Settings" page → **"Recording storage"**: directory, quota as a % of the volume capacity (90 by default) and the overwrite mode:

- **ring buffer (FIFO)** — the oldest segment is deleted as soon as the quota is exceeded;
- **watermarking** — when the high watermark is reached, old data is deleted down to the low watermark.

Files are fMP4 segments of 5 minutes: `<directory>/<camera id>/<session>/seg-YYYYMMDD-HHMMSS.mp4`. The used space and the segment list are tracked in SQLite. A separate volume is recommended (`./recordings:/recordings` in compose) — it will not be included in the database backup.

Requirements: `ffmpeg` in PATH (already present in the container). Recording goes through the internal go2rtc RTSP: the camera keeps a single connection and the password is not visible in the process list. If HTTP/ONVIF are disabled on the camera (for example, Reolink through the Baichuan bridge), events are unavailable — only scheduled continuous recording remains.

## Archive playback

The **"Archive"** tab: the camera list on the left, the player on top, the calendar below (dots mark days with recordings), "Schedule"/event type filters and a 0-24 h timeline.

- timeline: gray bars are scheduled recording, blue bars are events; wheel zooms, dragging scrolls, a click plays from the nearest recording to the right; the red playhead is the current position;
- buttons: previous/next recording, pause, speed 1/2/4/8, a separate real-time counter of the frame;
- recording and screenshots are done in the browser (WebM on Chromium, MP4 on Safari; screenshot — PNG), fullscreen applies to the player container;
- HLS playback: H.264 is copied, H.265/MJPEG are transcoded to H.264 only when the browser does not support the source codec; temporary sessions live in `<data>/playback-cache` and are removed automatically.

The default date is today. Gaps between recordings on the timeline are played with an automatic jump.

## Archive and playback

The **"Archive"** page (`/archive`):

- camera selection (list at the bottom) and date — a calendar with marks on days that have recordings;
- player: timeline seeking, pause, speed 1x-8x, snapshot, in-browser fragment recording, fullscreen;
- 24 h timeline: blue bars are recorded intervals, light blue bars are events, the red line is the position; zoom with the wheel or buttons;
- filters: show the schedule and individual event types.

The server prepares playback: ffmpeg builds HLS from the segments (copy for H.264; for H.265 either hvc1 or transcoding when the browser does not support HEVC). The cache is `<data>/playback-cache`, a session closes after 5 minutes of idle time (60 at most), and no more than 12 sessions run at once.

## Environment variables

| Variable | Purpose | Default |
|---|---|---|
| `HOST_LAN_IP` | Host LAN address for the WebRTC ICE candidate | `192.168.1.151` |
| `DISCOVERY_RANGE` | Discovery range used by the wizard | `192.168.1.0/24` |
| `Reolink__ApiBaseUrl` | Reolink bridge HTTP API | `http://127.0.0.1:8655` (in compose — `http://reolink:8655`) |
| `Reolink__RtspBaseUrl` | Reolink bridge RTSP | `rtsp://127.0.0.1:8654` (in compose — `rtsp://reolink:8654`) |
| `Reolink__ConfigPath` | Bridge configuration file | `<data>/reolink/config.json` |
| `TZ` | Container time zone (recording schedules and file names) | `Europe/Moscow` |
| `Recording__StoragePath` | Recording pool directory | `/recordings` (otherwise `<data>/recordings`) |
| `Recording__SegmentSeconds` | Recording segment length, s | `300` |

Inside the container you can also set `Storage__DataDirectory`, `Storage__DatabasePath`, `Go2Rtc__*`, `Discovery__DefaultRange`.

## Operations

```powershell
docker compose ps
docker compose logs -f mollynvr
docker compose restart mollynvr
docker compose down          # data in ./data is preserved
docker compose up -d --build # upgrade
```

Ports: `9080/tcp` — UI/API/WHEP; `8555/tcp` and `8555/udp` — WebRTC media. The go2rtc API is reachable only on loopback inside the container.

## Data and security

- `./data` is mounted to `/data`: the SQLite `mollynvr.db`, the `secret.key` key and service files.
- Camera passwords are stored only encrypted (AES-GCM). The key is created on first start; losing `secret.key` makes the stored passwords unreadable.
- The go2rtc config with working camera URLs is kept outside `/data` (tmpfs or a temporary directory) and never reaches the volume.
- Do not commit `.env`, `data/` and `Task.md` — they are in `.gitignore`.
- UI authentication is disabled: a trusted local network is assumed.

## Development

```powershell
dotnet build MollyNvr.slnx
dotnet test MollyNvr.slnx
npm --prefix ui run build
npm --prefix ui run lint
```

Running the API without Docker (the container must be stopped, otherwise port 9080 is busy):

```powershell
docker compose stop mollynvr
dotnet run --project src/MollyNvr.Api --no-launch-profile
```

UI in dev mode (second terminal): `npm --prefix ui run dev` — Vite proxies `/api` to `http://localhost:9080`. A local API run needs `tools/go2rtc.exe` (it is already present in the container).

## v1 limitations

- Multicast discovery from a bridge container usually gets no LAN responses (WS-Discovery/SSDP/mDNS/SADP): the UI suggests using a subnet scan. On the host or on Linux multicast works.
- A /24 scan takes about 20-30 s: an ICMP probe of live hosts first, then the ports. Cameras with Wi-Fi power save sometimes answer a TCP SYN after 1-3 s — such ports are checked with a 4 s timeout.
- Hikvision DS-KV9503 provides G.722.1 audio, which FFmpeg cannot decode; for sound, switch the camera audio codec to G.711ulaw/PCMU (or AAC).
- An ONVIF camera snapshot with a closed HTTP port is taken as a frame from RTSP — about 8 seconds.
- Reolink cameras without RTSP/ONVIF work only through the `reolink` bridge (Baichuan, port 9000). With HTTP disabled on the camera, image settings are unavailable; the first probe after adding takes about 5-10 seconds (the bridge restarts).
- Watching and downloading recordings in the UI is not implemented (the files are on disk); MQTT and server-side detection are absent.
- H.265 transcoding uses `#hardware` (CUDA/NVENC). Hardware acceleration is unavailable on a machine without NVIDIA.

## Documentation

- Architecture and decisions: [`docs/architecture.md`](docs/architecture.md).
- Stage status: M0-M6 are complete.
