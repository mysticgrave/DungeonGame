# Steam Join / Town Late-Join Audit

## Summary

This audit covers why clients joining via Steam invite may fail to load Town and walk around the lobby. Multiple potential causes have been identified and addressed.

---

## 1. Scene Setup

| Requirement | Status |
|-------------|--------|
| Town in Build Settings | ✅ Yes (`EditorBuildSettings.asset`) |
| MainMenu, Town, Spire_Slice in build order | ✅ Correct |
| Town loaded via `NetworkSceneManager.LoadScene` | ✅ Yes (SteamLobbyManager, LobbyMenuController) |
| `EnableSceneManagement = 1` on NetworkManager | ✅ Yes |
| Player spawn points in Town | ✅ Yes (PlayerSpawnPoint components, "PlayerSpawn" tag) |

**Conclusion:** Scene setup is correct for multiplayer.

---

## 2. Network Configuration

| Setting | Value |
|---------|-------|
| EnableSceneManagement | 1 |
| ConnectionApproval | 0 (auto-approve) |
| LoadSceneTimeOut | 120 |
| SpawnTimeout | 10 |
| PlayerPrefab | Assigned |
| Transport (Host) | FacepunchTransport (switched at runtime) |
| Transport (Client) | FacepunchTransport (switched before StartClient) |

**Conclusion:** Network config is correct. Host and client both use FacepunchTransport when joining via Steam.

---

## 3. Steam Invite Flow

### 3.1 Join via overlay (game already running)
1. User at Main Menu → accepts Steam invite
2. `OnGameLobbyJoinRequested` fires → `JoinLobby(lobby.Id)`
3. `HandleLobbyEntered` → set `targetSteamId`, 10-frame delay
4. `StartClient()` → FacepunchTransport connects via Steam relay
5. Netcode sync → client loads Town

### 3.2 Join via invite (game launched by Steam)
- Steam may pass `+connect_lobby <id>` on command line
- **FIX:** `SteamLobbyManager` now checks `SteamApps.CommandLine` on startup and auto-joins if present

---

## 4. Issues Found & Fixes

### 4.1 LoadingScreenManager not receiving scene events (FIXED)
- **Cause:** `NetworkSceneManager` only exists after `StartClient`. When the client is in MainMenu, `SceneManager` is null at `Start()`, so we never subscribed.
- **Fix:** `LoadingScreenManager.Update()` now calls `SubscribeToSceneEvents()` every frame until subscribed, so we receive `SynchronizeComplete` and hide the loading screen.

### 4.2 Steam invite when game launches (FIX ADDED)
- **Cause:** If Steam launches the game with `+connect_lobby X`, we were not checking the command line.
- **Fix:** `SteamLobbyManager` checks `SteamApps.CommandLine` on first `Update` and joins the lobby if present.

### 4.3 FacepunchTransport message processing during sync
- **Cause:** `ConnectionManager.Receive()` has max `bufferSize` 256 (Facepunch limit). Must process frequently during sync.
- **Fix:** Call `Receive(256)` on both server and client; do NOT use 512 (throws ArgumentOutOfRangeException). UnityTransport's MaxPacketQueueSize=512 is separate (UTP only, not Steam).

### 4.4 Client connect delay
- **Cause:** 10 frames might be too short for Steam relay to propagate when host is busy or on slow networks.
- **Fix:** Increased to 15 frames.

---

## 5. Verification Checklist

For hosts:
- [ ] Create lobby (Host) → Town loads
- [ ] Use Invite Friends from Steam overlay
- [ ] Verify `[FacepunchTransport] Server started` and `[SteamLobby] Loaded game scene: Town` in log

For clients:
- [ ] Accept Steam invite (game running or launched)
- [ ] See "Connecting to host..." loading screen
- [ ] Log shows `[FacepunchTransport] Connecting to host Steam ID X`
- [ ] Log shows `[FacepunchTransport] Connected to host.`
- [ ] Town loads, loading screen fades out
- [ ] Player spawns at PlayerSpawn point
- [ ] Can move and see host

If clients still fail:
- Ensure Steam is running and logged in
- Check Steam App ID matches (480 for Spacewar testing)
- Enable "Use launch command line" in Steamworks app settings if using launch-by-invite
- Verify no firewall blocking Steam P2P
- **ConfigHash mismatch**: If client connects then immediately disconnects, compare `[SteamLobby] HOST ConfigHash=...` with `[SteamLobby] CLIENT ConfigHash=...`. They must match. Mismatch = different NetworkConfig (TickRate, prefabs, protocol, etc.) — ensure host and client run the same build.
