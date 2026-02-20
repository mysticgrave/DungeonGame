# Run Progression (MVP Stub)

## Goal
Prototype the 5-floor segment cadence + evac portal logic without needing full procedural floors yet.

## Add to NetworkManager (Town)
On the persistent `NetworkManager` GameObject in the **Town** scene add:
- `DungeonGame.Run.SpireRunState`
- `DungeonGame.Run.RunDebugHotkeys`
- `DungeonGame.Run.RunDebugHUD`

## Hotkeys (Host only)
- F10: +1 floor
- F11: +5 floors
- F12: Evac (knock back 1 segment) + load Town

## Notes
- This is a placeholder until “floors” map to real gameplay milestones.
- Later we’ll gate evac to ritual rooms and make it interruptible.
