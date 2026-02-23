# Slop Knockdown (MVP): Capsule Ragdoll

## Goal
Temporary ragdoll knockdowns (goofy physics) that recover automatically if HP > 0.

## Player setup
On `Player.prefab` root add:
- `DungeonGame.Player.PlayerHealth`
- `DungeonGame.Player.KnockableCapsule`

Player must have:
- CharacterController
- CapsuleCollider
- Rigidbody

`KnockableCapsule` will:
- disable CharacterController while knocked
- enable CapsuleCollider + Rigidbody physics
- recover after a short duration

Assign `disableWhileKnocked` with movement scripts (e.g. ThirdPersonMotor / later FPS motor).

## Item (optional)
Create a `KnockBomb` prefab with:
- NetworkObject
- Rigidbody (for throwing later)
- `DungeonGame.Items.KnockBomb`

Spawn it from server to test knockdowns.

## Notes
Physics is simulated locally on each client for now (good enough for early slop). Later: move to server authoritative ragdoll.
