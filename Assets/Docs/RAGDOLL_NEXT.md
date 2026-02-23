# Slop Knockdown (MVP): Capsule Ragdoll

## Goal
Temporary ragdoll knockdowns (goofy physics) that recover automatically if HP > 0.

## Player setup
On `Player.prefab` root add:
- `DungeonGame.Player.PlayerHealth`
- `DungeonGame.Player.KnockableCapsule`
- `DungeonGame.Player.KnockHotkeys` (MVP test)
- `DungeonGame.Items.BombSpawnerHotkeys` (MVP test)

Player must have:
- CharacterController
- CapsuleCollider
- Rigidbody

`KnockableCapsule` will:
- disable CharacterController while knocked
- enable CapsuleCollider + Rigidbody physics
- recover after a short duration

Assign `disableWhileKnocked` with movement scripts (e.g. ThirdPersonMotor / later FPS motor).

## Bomb prefab
Create `Assets/Prefabs/KnockBomb.prefab`:
- NetworkObject
- (optional) Rigidbody
- `DungeonGame.Items.KnockBomb`

Assign this prefab into `BombSpawnerHotkeys.knockBombPrefab`.

## Hotkeys
- K: knock yourself
- B: spawn a knock bomb at your feet

## Notes
Physics is simulated locally on each client for now (good enough for early slop). Later: move to server authoritative ragdoll.
