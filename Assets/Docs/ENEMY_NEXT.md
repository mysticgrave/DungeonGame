# Enemy MVP: Runner Ghoul (NavMesh)

## Goal
Add one fast, aggressive, easy-to-kill mob that creates pressure in darkness.

## Prefab
Create `Assets/Prefabs/GhoulRunner.prefab`:
- Capsule (visual)
- `NetworkObject`
- `NetworkTransform` (server authoritative)
- `NavMeshAgent`
- `DungeonGame.Enemies.GhoulRunnerAI`
- `DungeonGame.Combat.NetworkHealth` (e.g., maxHp=1 or 2)

## NavMesh (procedural)
In `Spire_Slice` scene, on your `Spire` generator object:
- Add `NavMeshSurface` (from com.unity.ai.navigation)
  - Collect Objects = Children (recommended)
- Add `DungeonGame.SpireGen.NavMeshBakeOnLayout`

This rebuilds the navmesh after the layout spawns.

## Spawning
Create an empty GameObject in `Spire_Slice` scene:
- Add `NetworkObject`
- Add `DungeonGame.Enemies.GhoulSpawner`
- Assign `ghoulPrefab` to your `GhoulRunner` prefab

## Test
- Host
- Load Spire_Slice
- Verify ghouls chase and lunge
