# Combat (MVP Stub): Click-to-Damage

## Goal
Get a basic damage loop so enemies can die and you can validate pacing.

## Player setup
On `Player.prefab` root add:
- `DungeonGame.Combat.DebugDamageRaycaster`
- `DungeonGame.UI.CrosshairUI`

This lets the local player click to damage targets:
- LMB: 1 damage
- RMB: 2 damage

## Enemy setup
On `GhoulRunner.prefab` add:
- `DungeonGame.Combat.NetworkHealth`

When HP reaches 0, the ghoul NetworkObject despawns.

## Notes
This is a placeholder until weapon-collider attacks are implemented.
