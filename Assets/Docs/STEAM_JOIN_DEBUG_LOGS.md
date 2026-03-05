# Steam Join Debug Logs

When a player joins via Steam lobby but stays on "Connecting to host...", use these logs to trace where the flow stops.

## Expected Log Sequence (Client)

1. **`[SteamLobby] HandleLobbyEntered`** — Client joined Steam lobby; shows host ID, member count
2. **`[SteamLobby] Joined lobby as client, will StartClient in 15 frames`** — Deferring connect
3. **`[SteamLobby] Defer complete, starting client connect now.`** — After 15 frames
4. **`[SteamLobby] Calling StartClient (host Steam ID …)`** — About to call Netcode StartClient
5. **`[FacepunchTransport] Client connecting to Steam ID …`** — Transport initiating
6. **`[FacepunchTransport] CLIENT: Connecting via Steam relay...`** — Steam relay handshake started
7. **`[FacepunchTransport] CLIENT: Connected to host`** — Transport-level connection done
8. **`[LoadingScreen] Subscribed to SceneManager.OnSceneEvent`** — Scene sync listener ready
9. **`[Lobby] OnClientConnected: clientId=… isLocal=True`** — Netcode approved and connected
10. **`[LoadingScreen] SceneEvent: Synchronize scene=Town`** — Server telling client to load Town
11. **`[LoadingScreen] SceneEvent: SynchronizeComplete scene=Town`** — Town loaded
12. **`[LoadingScreen] Fading out`** — Loading screen hides

## Expected Log Sequence (Host/Server)

1. **`[FacepunchTransport] Client 1 connected (Steam: …)`** — Transport sees new client
2. **`[Lobby] OnClientConnected: clientId=1 isLocal=False`** — Netcode approved client
3. **`[Spawn] Positioned client 1 at …`** — Player spawned and placed

## Where It Can Break

| Last log seen on client | Likely cause |
|------------------------|--------------|
| Stops at `HandleLobbyEntered` with "We are the host" | Client joined their own lobby (hostId == own Steam ID). Use a different Steam account. |
| Stops before `StartClient` | `FacepunchTransport` not on NetworkManager, or 15-frame defer still running. |
| Stops at `Connecting via Steam relay` | Steam relay failing (NAT, firewall, or Steam network issues). |
| Never sees `Connected to host` | Transport connect failed; check Steam, firewall, VPN. |
| Sees `Connected to host` but no `OnClientConnected` | Netcode approval or spawn failing; check Player prefab in NetworkManager. |
| Sees `OnClientConnected` but no `SceneEvent: Synchronize` | Server not sending scene sync; host may not have Town loaded yet. |
| Sees `Synchronize` but no `SynchronizeComplete` | Town scene load failing on client. |
| `[Spawn] PlayerObject is null` on server | Player prefab missing or not in Network Prefabs. |

## Quick Checklist

- [ ] Host has Town loaded before client connects (host sees `[SteamLobby] Loaded game scene: Town`)
- [ ] NetworkManager in MainMenu has `FacepunchTransport` component
- [ ] NetworkManager → Network Config → Player Prefab is assigned
- [ ] Player prefab is in Network Prefabs list
- [ ] Both players use different Steam accounts (no self-join)
- [ ] Steam overlay / relay working (no strict firewall blocking Steam)
