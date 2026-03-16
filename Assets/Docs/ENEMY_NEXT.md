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

**Builds / runtime:** NavMesh baking at runtime needs mesh read access. If you see "Source mesh X does not allow read access" in the player build:
- **Synty assets (SM_Bld_Wall_*, etc.):** Run **Tools → DungeonGame → Enable Read/Write on Synty Models Only** — faster, targets dungeon/prop meshes.
- **All models:** Run **Tools → DungeonGame → Enable Read/Write on All Model Meshes** — processes Assets + Packages. Let it finish (don't cancel); reimport can take several minutes.
- **Single mesh:** Select the FBX in Project, Inspector → Model tab → enable **Read/Write Enabled**.

## Spawning
### Recommended (procedural): EnemySpawnPoint markers
In each room prefab, add 1–3 empty child transforms where enemies can spawn.
Attach:
- `DungeonGame.Enemies.EnemySpawnPoint`

In `Spire_Slice` scene, create an empty GameObject:
- Add `NetworkObject`
- Add `DungeonGame.Enemies.GhoulSpawner`
- Assign `ghoulPrefab` to your `GhoulRunner` prefab

The spawner will auto-collect EnemySpawnPoint markers after navmesh/layout is ready.

## Test
- Host
- Load Spire_Slice
- Verify ghouls chase and lunge
