# Player spawn points

There are two moments when players are placed at spawn points:

1. **When a client first connects** (e.g. in Town) — **PlayerSpawnSystem** moves their player to the next spawn point in the current scene.
2. **When players enter the dungeon** (e.g. Spire_Slice loads) — **DungeonPlayerSpawner** repositions *all* connected players to spawn points in the dungeon scene (so everyone starts the run at your chosen dungeon spawns).

Use **PlayerSpawnPoint** (or tag "PlayerSpawn") in the scene or inside prefabs to define positions. In the dungeon, put spawn points in the scene or inside room prefabs (e.g. the **start room**) so they exist when the layout is generated.

---

## Option 1: PlayerSpawnPoint component (recommended for prefabs)

1. Open the **prefab** or **scene** where you want players to spawn (e.g. Town, or your RunState/level prefab).
2. **Create Empty** (right-click in Hierarchy → Create Empty). Name it e.g. `PlayerSpawn` or `Spawn_Entrance`.
3. **Position** the transform where the player’s feet should be (move and rotate in the Scene view).
4. **Add Component** → search **Player Spawn Point** → add **Player Spawn Point** (`DungeonGame.Core.PlayerSpawnPoint`).
5. (Optional) Set **Spawn Index** to control order when you have multiple spawns (lower = used first; host gets index 0, next client index 1, etc.).
6. **Save** the prefab or scene.

Add more empty GameObjects with **Player Spawn Point** for multiple spawn locations. The server cycles through them (by Spawn Index, then order in the list).

---

## Option 2: Tag "PlayerSpawn"

If you don’t use the component, the system falls back to **tagged** objects:

1. Create an empty GameObject and position it where players should spawn.
2. In the Inspector, set its **Tag** to **PlayerSpawn** (add the tag in Project Settings → Tags and Layers if needed).
3. Add more tagged objects for multiple spawns.

---

## Dungeon: spawn when entering the run

To have players spawn on your chosen points **when they enter the dungeon** (e.g. after F5 loads Spire_Slice):

1. **Add DungeonPlayerSpawner** to a GameObject in the **dungeon scene** (e.g. Spire_Slice). Use the same object that has **SpireLayoutGenerator** (or any object in that scene).
2. **Add PlayerSpawnPoint** to empty GameObjects **in the dungeon** — either in the scene or **inside room prefabs** (e.g. the **start room** prefab). Position them where players should stand when the run starts.
3. When the dungeon loads:
   - If the layout is **procedural**, the spawner runs after **SpireLayoutGenerator** builds the layout (so spawn points inside the start room exist).
   - Otherwise it runs after a short delay (fallback).

Result: when the host loads the dungeon (e.g. F5), all connected players are moved to those dungeon spawn points.

## Where to put spawn points

- **Town / first connect**: In the scene or in a prefab that is in the scene when clients connect. **PlayerSpawnSystem** uses them when each client joins.
- **Dungeon**: In the Spire scene or inside **room prefabs** (e.g. start room). **DungeonPlayerSpawner** finds them after the layout is generated (or after the fallback delay) and repositions all players.

---

## Requirements

- **PlayerSpawnSystem**: On the same GameObject as **NetworkManager** (or in the scene). Handles **first connect** (Town) and moves each new player to a spawn point.
- **DungeonPlayerSpawner**: On a GameObject in the **dungeon scene** (e.g. Spire_Slice, same object as SpireLayoutGenerator). Handles **enter dungeon** and repositions all players to dungeon spawn points.
- At least one spawn point (component or tag) in the relevant scene, or players stay at (0,0,0) or their previous position.

---

## Gizmos

In the Editor, **Player Spawn Point** components draw a green wire sphere and a forward line so you can see and orient spawns in the Scene view.
