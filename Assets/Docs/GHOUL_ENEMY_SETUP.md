# Ghoul / Capsule Enemy Setup

## Capsule Testing (No Animator)

When using a simple **capsule** for testing (no Animator, no ragdoll bones):

- **Ragdoll on death**: The enemy now uses a "simple physics" fallback: a Rigidbody is added to the root on death, and the capsule tumbles from the hit impulse instead of disappearing.
- **No despawn**: Set `NetworkHealth.despawnOnDeath = false` on the enemy prefab so corpses stay.
- **Animator spam**: If you see "not using an animator" or similar, it may come from:
  - **Synty PropBoneBinder** (if the prefab has Synty character parts) — disable or remove that component on capsule test prefabs.
  - **EnemyStateMachine / EnemyAttackExecutor** — these handle missing Animators silently; no logs from them.

## Combat Feel

1. **Hit feedback**: Add `HitFeedback` to the enemy prefab and assign a hit sound and/or particle VFX. This plays when the enemy takes damage.
2. **Knock strength**: Tune `WeaponController.enemyKnockForward` and `enemyKnockUp` (default 8 / 3) for stronger or weaker knockback.
3. **Death impulse**: On death, the sword applies impulse in the attack direction. Capsules use the root Rigidbody; full ragdolls use the hips.

## Full Ragdoll (Later)

For a proper ragdoll with bones: add child objects with Rigidbodies and CharacterJoints. The system will use those instead of the simple capsule fallback.
