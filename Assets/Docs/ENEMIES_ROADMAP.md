# Enemies Roadmap (Roguelite)

## Current
- **GhoulRunner** – Fast melee, NavMesh chase + lunge. Low HP. See `ENEMY_NEXT.md`.

## Suggested Variants (same AI, different config)
Add new prefabs with different stats and optional behavior flags:

| Type        | Prefab idea   | HP  | Speed | Notes                          |
|------------|---------------|-----|-------|---------------------------------|
| Ranged     | GhoulShooter  | 2   | 4     | Stops at range, fires projectile or raycast (new component). |
| Tank       | GhoulBrute    | 6   | 3     | Slow, high HP, same chase/lunge. |
| Swarm      | GhoulCrawler  | 1   | 8     | Very fast, very low HP, same AI. |

## Implementation
1. Duplicate `GhoulRunner.prefab`, rename (e.g. `GhoulBrute`).
2. Adjust `NetworkHealth.maxHp`, `GhoulRunnerAI` speed/aggro/lunge as needed.
3. For **ranged**: add a new script (e.g. `RangedEnemyAI`) that uses NavMesh to close to a “fire range” then shoots at player (server-spawned projectile or hitscan RPC). Reuse `NetworkHealth` and spawn from same spawner or a separate one.
4. Register new prefabs in your spawner(s) or a Spawn Director (future) that picks by room type / floor.

## Bosses
See `BOSSES_NEXT.md` for Spire Heart and future boss design.
