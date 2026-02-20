# Spire Generation (MVP v0) — Socket Snap

## Goal
Procedurally assemble `Spire_Slice` from a library of room prefabs using explicit sockets.

This version:
- Server-only generation
- 90° yaw rotations only
- Spawns room prefabs as NetworkObjects (easy sync)
- Designed so we can later switch to a “layout recipe” instantiation backend

## Components
### Room prefabs
Each room prefab root needs:
- `RoomPrefab`
- `NetworkObject`
- Colliders (for overlap checks)

Each connection point needs:
- Empty child transform
- `RoomSocket` component
  - forward points OUT of the room
  - set `socketType` and `size`

### Spire generator
On the `Spire` GameObject in `Spire_Slice` scene (the same object that has `SpireSeed`):
- Add `SpireLayoutGenerator`
- Assign `roomPrefabs` list (your room prefabs)

## Testing
- Start host
- Load `Spire_Slice`
- Generator will spawn a main path + branches

## Notes / Next upgrades
- Add “unique rooms” (landmarks) once per slice
- Add repeat cooldown for common rooms
- Add loops (currently branches only)
- Add recipe backend to reduce NetworkObject count
