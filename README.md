# AR-VR Project README

## Overview

This repository hosts Unity-based AR/VR projects for Pico Neo VR headsets and SteamVR, featuring networked multiplayer via WebSocket. Development occurs in a Linux environment with C# for client-side and Python for the server.

The `dev` branch includes scenes, assets, networking scripts (WSManager, NetworkManager), and `server.py` for the backend.

## Features

- Pico Neo VR integration with 6DoF tracking.
- SteamVR PC testing support.
- WebSocket-based networking for multiplayer.
- Interactive VR scenes with controllers.

## Tech Stack

- Unity (C#), Pico SDK, SteamVR.
- Python WebSocket server (`server.py`).
- Linux dev tools (ADB, Git).

## Prerequisites

- Unity 2021 LTS+, Pico SDK, SteamVR.
- Pico Neo with dev mode.
- Python 3+ for `server.py` (likely `websockets` lib).
- Devices on same WiFi hotspot.

## Setup Instructions

### 1. Unity Project Setup (Do This First)

1. Clone: `git clone -b dev https://github.com/kashif003/AR-VR.git`.
2. Open in Unity Hub.
3. Import Pico Unity SDK (Assets > Import Package).
4. Configure XR: Install OpenXR, enable Pico/SteamVR.
5. Verify IPs: Set server IP in WSManager and FullNetwork/NetworkManager.
6. Connect Pico via USB/WiFi.

### 2. Network Configuration

- Both server PC and Pico on **same hotspot**.
- Note server IP (e.g., `ifconfig` or `ip addr`).

### 3. Start Server

1. `cd` to repo root (where `server.py` is).
2. Run: `python3 server.py` (install deps if needed, e.g., `pip install websockets`).
3. Confirm server logs show listening IP/port.

### 4. Build & Run Client

- Android platform for Pico: Build APK, `adb install`.
- Launch VR app; it connects to running server.

## Building and Deployment

| Target   | Platform      | Steps                                                |
| -------- | ------------- | ---------------------------------------------------- |
| Pico Neo | Android       | Set IP, build APK, sideload, run after server start. |
| SteamVR  | PC Standalone | Build exe, run with SteamVR & server on.             |

## Project Structure

```
AR-VR/
├── Assets/
│   ├── Scenes/
│   ├── Scripts/
│   ├── FullNetwork/     # NetworkManager
│   ├── WSManager/       # WebSocket client
│   ├── Prefabs/
│   └── Plugins/
├── server.py            # Python WebSocket server
└── ...
```

## Troubleshooting

- **No connection**: Check hotspot, ping server IP, firewall ports.
- **Server errors**: Verify `python3 server.py` runs, deps installed.
- ADB: `sudo adb` on Linux.

## Contributing

PRs to `dev` branch; test networking.

## License

MIT.

Contact: kashif003 via GitHub.
