# Combat: Damage & Weapons

## Overview

- **Player → Enemies**: `WeaponController` (server-authoritative) uses `WeaponConfig` (melee or ranged) and deals damage to `NetworkHealth`. Enemies die and despawn at 0 HP.
- **Enemies → Player**: `GhoulRunnerAI` (and similar) call `PlayerHealth.TakeDamage(int)` on the server when a lunge connects. Player does not despawn at 0 HP (downed system later).
- **Feedback**: `NetworkHealth` exposes `OnDamaged` and `OnDied`. Add `HitFeedback` on enemies to play hit sound/VFX.

## Player setup

On **Player.prefab** root:

- `DungeonGame.Player.PlayerHealth` (maxHp, server-authoritative)
- `DungeonGame.Weapons.WeaponController`
  - Assign **Config Fallback** to a `WeaponConfig` ScriptableObject (or rely on class default / WeaponRegistry first entry)
- `DungeonGame.UI.CrosshairUI` (optional; for aim reference)

WeaponController resolves its config from: equipped meta weapon → class default weapon → configFallback → first weapon in WeaponRegistry.

## Enemy setup

On **GhoulRunner.prefab** (or any damageable enemy):

- `DungeonGame.Combat.NetworkHealth` (e.g. maxHp = 1 or 2)
- Optional: `DungeonGame.Combat.HitFeedback` (assign hitSound and/or hitVfx for hit feedback)

When HP reaches 0, the enemy’s `NetworkObject` is despawned by `NetworkHealth`.

## Enemy → Player damage

`GhoulRunnerAI` has **Lunge Damage** (default 1). When a lunge connects, the server calls `PlayerHealth.TakeDamage(lungeDamage)` on the targeted player. No extra setup required if the player has `PlayerHealth`.

## Weapon config

Create **WeaponConfig** assets (right-click → Create → DungeonGame → Weapon Config):

- **weaponId** – used by meta/registry (e.g. `"sword"`, `"crossbow"`)
- **attackType** – Melee (OverlapSphere in front) or Ranged (raycast)
- **damage**, **range**, **cooldown**
- Melee: **hitRadius**
- Ranged: **hitLayers**

Assign weapons to **WeaponRegistry** (scene singleton) and/or to **ClassDefinition.defaultWeapon** and **WeaponController.configFallback**.

## Debug fallback (click-to-damage)

For testing without a weapon config, you can add **DebugDamageRaycaster** to the Player:

- LMB: 1 damage, RMB: 2 damage (raycast from camera center).

This is optional; the main combat path is WeaponController + WeaponConfig.

## Scripts reference

| Script | Purpose |
|--------|--------|
| `NetworkHealth` | Server-authoritative HP; `TakeDamage(int)`, `TakeDamageRpc(int)`; despawns at 0 (if NetworkObject). |
| `PlayerHealth` | Player HP; `TakeDamage(int)` (server-only, used by enemies), `DamageRpc(int)`, `HealRpc(int)`. Does not despawn at 0. |
| `WeaponController` | Owner-only input; ServerRpc performs melee (OverlapSphere) or ranged (raycast), calls `NetworkHealth.TakeDamage`. |
| `WeaponConfig` | ScriptableObject: damage, range, cooldown, attackType, hitRadius / hitLayers. |
| `HitFeedback` | Listens to `NetworkHealth.OnDamaged`; plays optional AudioClip and/or ParticleSystem. |
| `IDamageable` | Interface with `TakeDamage(int)`; implemented by `NetworkHealth` (and can be used for future shared damage API). |

## Next steps (optional)

- **Hit reactions**: Stagger/flinch animation or state when an enemy is hit (e.g. from `OnDamaged`).
- **Damage numbers**: UI or world-space text showing damage per hit (subscribe to `OnDamaged`).
- **Weapon colliders**: Replace raycast/overlap with timed colliders for melee (animation-driven hits).
- **Downed state**: When `PlayerHealth` reaches 0, transition to a downed/revive flow instead of just logging.
