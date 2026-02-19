# Netcode Next Steps (MVP)

## 1) Player Prefab
Create `Assets/Prefabs/Player.prefab`:
- Capsule (visual)
- `NetworkObject`
- `NetworkTransform`

Assign it on the **NetworkManager** in Town:
- NetworkManager → Player Prefab = Player
- Ensure `Enable Scene Management` is ON

## 2) Player spawn points
In Town scene:
- Create one or more empty GameObjects
- Tag them **PlayerSpawn**
- Place them where you want players to appear

## 3) Add components to NetworkManager
On the Town scene `NetworkManager` GameObject add:
- `DungeonGame.Core.PlayerSpawnSystem`

This will move newly spawned player objects to a spawn point.

## 4) Host-only scene hotkeys
Add this in both Town and Spire scenes (any GameObject is fine, e.g. Bootstrap):
- `DungeonGame.Core.HostSceneHotkeys`

Hotkeys:
- F5 = load `Spire_Layer`
- F6 = load `Town`

## 5) Test
- Play in Town
- F1 to Host
- Start second instance (or build) and F2 to connect
- Verify both players spawn at PlayerSpawn points
- Press F5 on host: both should load `Spire_Layer`
