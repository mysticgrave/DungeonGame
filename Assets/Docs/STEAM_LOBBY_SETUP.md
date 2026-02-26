# Steam Lobby & Multiplayer Setup

## Overview

This replaces direct IP connections with Steam's relay networking. Players host/join through Steam — friends can join via the Steam overlay friends list or invites. No port forwarding needed.

## Prerequisites

1. **Steam App ID** — You need one from Steamworks (use `480` for testing — Valve's "Spacewar" test app).
2. **Steam must be running** on the machine for any of this to work.

---

## Step 1: Install Facepunch.Steamworks

This is the C# wrapper for the Steamworks SDK.

1. Download the latest release from: https://github.com/Facepunch/Facepunch.Steamworks/releases
2. Extract the **`Unity`** folder from the zip.
3. Copy it into your project at `Assets/Plugins/Facepunch.Steamworks/`
4. The `.meta` files are included — no manual DLL configuration needed.

## Step 2: Install Facepunch Transport for NGO

This replaces UnityTransport with Steam's P2P relay.

1. In Unity: **Window → Package Manager → Add package from git URL**
2. Enter: `https://github.com/Unity-Technologies/multiplayer-community-contributions.git?path=/Transports/com.community.netcode.transport.facepunch`
3. Click Add.

## Step 3: Create `steam_appid.txt`

1. In your **project root** (next to Assets/), create a file called `steam_appid.txt`
2. Put your App ID in it (just the number, e.g. `480` for testing).
3. This file tells Steam which app you are when running from the Editor.

## Step 4: Configure NetworkManager

1. On your **NetworkManager** GameObject:
   - Remove (or disable) the `UnityTransport` component.
   - Add the **`FacepunchTransport`** component.
   - In **NetworkManager → NetworkConfig → Network Transport**, assign the new `FacepunchTransport`.

## Step 5: Add SteamManager to your scene

Add `SteamManager.cs` (included in this project at `Assets/Scripts/Core/SteamManager.cs`) to a persistent GameObject in your first scene (e.g. on the NetworkManager or a "Boot" object). It handles:
- `SteamClient.Init()` on Awake
- `SteamClient.RunCallbacks()` every frame
- `SteamClient.Shutdown()` on quit

## Step 6: Wire the Lobby Menu

`SteamLobbyManager.cs` (at `Assets/Scripts/Core/SteamLobbyManager.cs`) handles:
- Creating a Steam lobby (friends-only by default)
- Joining via lobby ID
- Steam overlay "Join Game" support (friends click your name → Join Game)
- Listing available lobbies

The `LobbyMenuController.cs` has been updated to work with both Steam and direct IP.

---

## How It Works (Player Flow)

### Hosting
1. Player clicks "Host Game" in the menu.
2. `SteamLobbyManager` creates a Steam lobby (friends-only).
3. NGO starts as Host using `FacepunchTransport` (Steam relay, no port forwarding).
4. Friends see the host as "In Game" on their Steam friends list and can click "Join Game".

### Joining (Friends List)
1. Open Steam overlay (Shift+Tab).
2. Right-click the host's name → "Join Game".
3. Steam fires a callback → `SteamLobbyManager` auto-connects.

### Joining (Invite)
1. Host clicks "Invite" in the lobby panel.
2. Steam overlay opens with friend picker.
3. Invited player gets a Steam notification → clicks it → auto-connects.

### Joining (Lobby List)
1. Player clicks "Join Game" → sees a list of available lobbies.
2. Clicks one → connects.

---

## Testing

- Use App ID `480` (Spacewar) for local testing.
- Steam must be running.
- Two separate Steam accounts are needed for two-player testing (or use two PCs).
- In-Editor testing works if `steam_appid.txt` exists in the project root.
