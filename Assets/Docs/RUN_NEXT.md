# Run Progression (MVP Stub)

## Goal
Prototype the 5-floor segment cadence + evac portal logic without needing full procedural floors yet.

## Setup (Town)
### 1) Create RunState prefab
Create `Assets/Prefabs/RunState.prefab`:
- `NetworkObject`
- `DungeonGame.Run.SpireRunState`
- `DungeonGame.Run.RunDebugHotkeys`
- `DungeonGame.Run.RunDebugHUD`

### 2) Add bootstrap in Town scene
On a non-NetworkManager object in the Town scene (e.g., `Bootstrap`) add:
- `DungeonGame.Run.RunStateBootstrap`

Assign the `runStatePrefab` field to your `RunState` prefab.

> Note: Netcode disallows NetworkBehaviours under the NetworkManager hierarchy.

## Hotkeys (Host only)
- F10: +1 floor
- F11: +5 floors
- F12: Evac (knock back 1 segment) + load Town

## Notes
- This is a placeholder until “floors” map to real gameplay milestones.
- Later we’ll gate evac to ritual rooms and make it interruptible.
