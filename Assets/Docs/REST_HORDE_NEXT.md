# Rest Horde (MVP)

## Goal
Prevent infinite turtling in Rest rooms by sending a single big wave if players linger.

## Components
- `DungeonGame.Spire.RoomTag` (tag room type)
- `DungeonGame.Spire.RestRoomHordeTrigger` (server-side timer + wave spawn)
- `DungeonGame.Enemies.EnemySpawnPoint` (spawn marker)

## Setup
1) Choose a room prefab to be your Rest room.
2) On the room root, add `RoomTag` and set tag = `Rest`.
3) Inside that room prefab, create an empty child `RestHordeVolume`:
   - Add a BoxCollider (isTrigger)
   - Add `RestRoomHordeTrigger`
   - Assign `ghoulPrefab` to your `GhoulRunner` prefab (NetworkObject)
4) Add 2–6 spawn markers inside/near the exits of the room:
   - Empty children with `EnemySpawnPoint` set kind = `RestHorde`

## Behavior (defaults)
- 90s: warning log
- 120s: spawns one wave (10 ghouls)
- 5 min cooldown per room trigger

## Notes
- Later: replace warning log with audio + torch flicker.
- Later: wave composition comes from Spawn Director tables.
