# Doors + Caps (MVP)

## Goal
After procedural room generation, fill each room socket with:
- a **door connector** if the socket is connected to another room
- a **wall cap** if the socket is unconnected

This prevents open holes and bridges navmesh gaps.

## Setup
### 1) Create 4 prefabs (NetworkObject)
Create these prefabs (simple cubes are fine for now):
- `DoorConnector_Small.prefab`
- `WallCap_Small.prefab`
- `DoorConnector_Large.prefab`
- `WallCap_Large.prefab`

Each must have a `NetworkObject` on the root.

Pivot/orientation: forward should point outward from the room.

Tip: Put a thin floor strip inside the DoorConnector prefab to bridge the seam.

### 2) Add DoorCapBuilder
In `Spire_Slice` scene, on the same object as `SpireLayoutGenerator` (e.g., `Spire`):
- Add `DungeonGame.SpireGen.DoorCapBuilder`
- Assign the 4 prefabs.

### 3) Socket types
On `RoomSocket`:
- Use `DoorSmall` for normal openings
- Use `DoorLarge` for big entrances (boss/landmark)

## Notes
- Door/cap prefabs are spawned as top-level NetworkObjects (not nested under rooms) to avoid NGO nested spawn limitations.
- Current logic treats "used socket" as "connected".
- Later: store explicit connection pairs for locked doors / navmesh links.
