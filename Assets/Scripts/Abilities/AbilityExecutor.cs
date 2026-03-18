using System;
using DungeonGame.Classes;
using DungeonGame.Combat;
using DungeonGame.Enemies;
using DungeonGame.Player;
using Unity.Netcode;
using UnityEngine;

namespace DungeonGame.Abilities
{
    /// <summary>
    /// Static server-side ability execution. Mirrors ItemAttackExecutor pattern.
    /// Called from PlayerAbilityController.UseAbilityServerRpc.
    /// </summary>
    public static class AbilityExecutor
    {
        /// <summary>Fired after any ability executes. bool = hit at least one target.</summary>
        public static event Action<bool> OnAbilityFired;

        /// <summary>
        /// Execute an ability. Server-only.
        /// </summary>
        public static bool Execute(AbilityConfig config, Transform caster,
            NetworkObject casterNetObj, PlayerClassStats stats)
        {
            if (config == null || caster == null) return false;

            float dmgMult = stats != null ? stats.DamageMultiplier : 1f;
            bool hit = false;

            switch (config.abilityType)
            {
                case AbilityType.MeleeAoE:
                    hit = ExecuteMeleeAoE(config, caster, casterNetObj, dmgMult);
                    break;
                case AbilityType.Projectile:
                    hit = ExecuteProjectile(config, caster, casterNetObj, dmgMult);
                    break;
                case AbilityType.SelfBuff:
                    hit = ExecuteSelfBuff(config, caster, casterNetObj, stats);
                    break;
                case AbilityType.DashStrike:
                    hit = ExecuteDashStrike(config, caster, casterNetObj, dmgMult);
                    break;
                case AbilityType.AreaZone:
                    hit = ExecuteAreaZone(config, caster, casterNetObj, dmgMult);
                    break;
            }

            OnAbilityFired?.Invoke(hit);
            return hit;
        }

        /// <summary>
        /// Execute an ability at a specific world position (for targeted/hold-to-cast abilities). Server-only.
        /// </summary>
        public static bool ExecuteAtPosition(AbilityConfig config, Transform caster,
            NetworkObject casterNetObj, PlayerClassStats stats, Vector3 targetPosition)
        {
            if (config == null || caster == null) return false;

            float dmgMult = stats != null ? stats.DamageMultiplier : 1f;
            bool hit = false;

            switch (config.abilityType)
            {
                case AbilityType.AreaZone:
                    hit = ExecuteAreaZoneAtPosition(config, caster, casterNetObj, dmgMult, targetPosition);
                    break;
                case AbilityType.MeleeAoE:
                    hit = ExecuteMeleeAoEAtPosition(config, caster, casterNetObj, dmgMult, targetPosition);
                    break;
                default:
                    // For other types, fall back to normal execution
                    hit = Execute(config, caster, casterNetObj, stats);
                    break;
            }

            OnAbilityFired?.Invoke(hit);
            return hit;
        }

        // ──────────────────── MeleeAoE ────────────────────

        private static bool ExecuteMeleeAoE(AbilityConfig config, Transform caster,
            NetworkObject casterNetObj, float dmgMult)
        {
            int finalDamage = Mathf.Max(1, Mathf.RoundToInt(config.damage * dmgMult));
            Vector3 center = caster.position;

            var hits = Physics.OverlapSphere(center, config.radius, ~0, QueryTriggerInteraction.Ignore);
            bool hitAny = false;
            var processed = new System.Collections.Generic.HashSet<ulong>();

            foreach (var col in hits)
            {
                var no = col.GetComponentInParent<NetworkObject>();
                if (no == null) continue;
                if (no.NetworkObjectId == casterNetObj.NetworkObjectId) continue;
                if (no.IsPlayerObject) continue; // Don't damage other players with abilities
                if (!processed.Add(no.NetworkObjectId)) continue; // Deduplicate

                var health = col.GetComponentInParent<NetworkHealth>();
                if (health == null) continue;

                Vector3 hitPoint = col.ClosestPoint(center);
                Vector3 knockDir = (col.transform.position - center).normalized;

                var info = new DamageInfo(finalDamage)
                {
                    Type = config.damageType,
                    AttackerClientId = casterNetObj.OwnerClientId,
                    HitPosition = hitPoint,
                    KnockImpulse = knockDir * 5f + Vector3.up * 2f
                };
                health.TakeDamage(info);

                // Apply status effect
                ApplyStatusEffect(col, config, knockDir);

                // Stagger/ragdoll
                var enemyAi = col.GetComponentInParent<EnemyAI>();
                if (enemyAi != null)
                {
                    if (health.Hp <= 0)
                        enemyAi.ApplyHitImpulse(info.KnockImpulse);
                    else
                        enemyAi.Stagger();
                }

                hitAny = true;
            }

            Debug.Log($"[Ability] MeleeAoE '{config.displayName}' — {processed.Count} enemies hit, {finalDamage} dmg each");
            return hitAny;
        }

        // ──────────────────── Projectile ────────────────────

        private static bool ExecuteProjectile(AbilityConfig config, Transform caster,
            NetworkObject casterNetObj, float dmgMult)
        {
            if (config.projectilePrefab == null)
            {
                Debug.LogWarning($"[Ability] Projectile ability '{config.displayName}' has no projectilePrefab!");
                return false;
            }

            int finalDamage = Mathf.Max(1, Mathf.RoundToInt(config.damage * dmgMult));
            Vector3 forward = caster.forward;
            // Spawn projectile ahead of the player to avoid colliding with camera/player body
            Vector3 spawnPos = caster.position + Vector3.up * 1.2f + forward * 2f;

            for (int i = 0; i < config.projectileCount; i++)
            {
                // Calculate spread for multiple projectiles
                Vector3 dir = forward;
                if (config.projectileCount > 1)
                {
                    float totalSpread = config.projectileSpreadAngle * (config.projectileCount - 1);
                    float angle = -totalSpread / 2f + config.projectileSpreadAngle * i;
                    dir = Quaternion.Euler(0f, angle, 0f) * forward;
                }

                var go = UnityEngine.Object.Instantiate(config.projectilePrefab, spawnPos, Quaternion.LookRotation(dir));
                var netObj = go.GetComponent<NetworkObject>();
                if (netObj == null)
                {
                    Debug.LogError($"[Ability] Projectile prefab for '{config.displayName}' missing NetworkObject!");
                    UnityEngine.Object.Destroy(go);
                    continue;
                }

                netObj.Spawn(true);

                // Initialize projectile behavior
                var proj = go.GetComponent<AbilityProjectile>();
                if (proj != null)
                {
                    float lifetime = config.range / config.projectileSpeed;
                    proj.Initialize(finalDamage, config.damageType, casterNetObj.OwnerClientId,
                        config.statusEffect, config.statusDuration, config.statusImpulseForce, lifetime);
                }

                // Set velocity
                var rb = go.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.useGravity = config.projectileUseGravity;
                    rb.linearVelocity = dir * config.projectileSpeed;
                }
            }

            Debug.Log($"[Ability] Projectile '{config.displayName}' — fired {config.projectileCount} projectile(s)");
            return true;
        }

        // ──────────────────── SelfBuff ────────────────────

        private static bool ExecuteSelfBuff(AbilityConfig config, Transform caster,
            NetworkObject casterNetObj, PlayerClassStats stats)
        {
            bool applied = false;

            // Heal
            if (config.healAmount > 0)
            {
                var health = caster.GetComponent<PlayerHealth>();
                if (health != null)
                {
                    health.HealRpc(config.healAmount);
                    applied = true;
                }
            }

            // Speed boost
            if (config.speedBoostMultiplier > 0f && stats != null)
            {
                stats.ApplySpeedBoost(config.speedBoostMultiplier, config.buffDuration);
                applied = true;
            }

            Debug.Log($"[Ability] SelfBuff '{config.displayName}' — heal={config.healAmount}, " +
                      $"speedBoost={config.speedBoostMultiplier}x for {config.buffDuration}s");
            return applied;
        }

        // ──────────────────── DashStrike ────────────────────

        private static bool ExecuteDashStrike(AbilityConfig config, Transform caster,
            NetworkObject casterNetObj, float dmgMult)
        {
            int finalDamage = Mathf.Max(1, Mathf.RoundToInt(config.damage * dmgMult));
            Vector3 start = caster.position;
            Vector3 dir = caster.forward;
            Vector3 end = start + dir * config.dashDistance;

            // Use OverlapCapsule along the dash path to find enemies
            float capsuleRadius = 1f;
            Vector3 midPoint = (start + end) / 2f + Vector3.up * 1f;
            float halfLength = config.dashDistance / 2f;

            var hits = Physics.OverlapCapsule(
                midPoint - dir * halfLength,
                midPoint + dir * halfLength,
                capsuleRadius, ~0, QueryTriggerInteraction.Ignore);

            bool hitAny = false;
            var processed = new System.Collections.Generic.HashSet<ulong>();

            foreach (var col in hits)
            {
                var no = col.GetComponentInParent<NetworkObject>();
                if (no == null) continue;
                if (no.NetworkObjectId == casterNetObj.NetworkObjectId) continue;
                if (no.IsPlayerObject) continue;
                if (!processed.Add(no.NetworkObjectId)) continue;

                var health = col.GetComponentInParent<NetworkHealth>();
                if (health == null) continue;

                Vector3 hitPoint = col.ClosestPoint(midPoint);
                var info = new DamageInfo(finalDamage)
                {
                    Type = config.damageType,
                    AttackerClientId = casterNetObj.OwnerClientId,
                    HitPosition = hitPoint,
                    KnockImpulse = dir * 8f + Vector3.up * 3f
                };
                health.TakeDamage(info);

                ApplyStatusEffect(col, config, dir);

                var enemyAi = col.GetComponentInParent<EnemyAI>();
                if (enemyAi != null)
                {
                    if (health.Hp <= 0)
                        enemyAi.ApplyHitImpulse(info.KnockImpulse);
                    else
                        enemyAi.Ragdoll(info.KnockImpulse);
                }

                hitAny = true;
            }

            Debug.Log($"[Ability] DashStrike '{config.displayName}' — {processed.Count} enemies hit, dashed {config.dashDistance}m");
            return hitAny;
        }

        // ──────────────────── AreaZone ────────────────────

        private static bool ExecuteAreaZone(AbilityConfig config, Transform caster,
            NetworkObject casterNetObj, float dmgMult)
        {
            // Place zone at caster's position (or forward offset)
            Vector3 zonePos = caster.position + caster.forward * Mathf.Min(config.range, 2f);
            return SpawnZone(config, casterNetObj, dmgMult, zonePos);
        }

        // ──────────────────── Targeted Variants ────────────────────

        private static bool ExecuteAreaZoneAtPosition(AbilityConfig config, Transform caster,
            NetworkObject casterNetObj, float dmgMult, Vector3 targetPosition)
        {
            return SpawnZone(config, casterNetObj, dmgMult, targetPosition);
        }

        /// <summary>
        /// Shared zone spawning logic. Uses the zonePrefab if available, otherwise creates
        /// a RuntimeAbilityZone (server-only, no NetworkObject needed).
        /// </summary>
        private static bool SpawnZone(AbilityConfig config, NetworkObject casterNetObj,
            float dmgMult, Vector3 position)
        {
            int tickDamage = Mathf.Max(1, Mathf.RoundToInt(config.zoneDamagePerTick * dmgMult));

            if (config.zonePrefab != null)
            {
                var go = UnityEngine.Object.Instantiate(config.zonePrefab, position, Quaternion.identity);
                var netObj = go.GetComponent<NetworkObject>();
                if (netObj == null)
                {
                    Debug.LogError($"[Ability] Zone prefab for '{config.displayName}' missing NetworkObject!");
                    UnityEngine.Object.Destroy(go);
                    return false;
                }

                netObj.Spawn(true);

                var zone = go.GetComponent<AbilityZone>();
                if (zone != null)
                {
                    zone.Initialize(tickDamage, config.zoneTickInterval, config.damageType,
                        casterNetObj.OwnerClientId, config.statusEffect, config.statusDuration,
                        config.zoneDuration, config.radius);
                }
            }
            else
            {
                // No prefab — create a server-only runtime zone (damage still works, VFX handled by client RPC)
                var go = new GameObject($"RuntimeZone_{config.displayName}");
                go.transform.position = position;
                var runtimeZone = go.AddComponent<RuntimeAbilityZone>();
                runtimeZone.Initialize(tickDamage, config.zoneTickInterval, config.damageType,
                    casterNetObj.OwnerClientId, config.statusEffect, config.statusDuration,
                    config.zoneDuration, config.radius);
            }

            Debug.Log($"[Ability] AreaZone '{config.displayName}' — placed at {position}, " +
                      $"{tickDamage} dmg every {config.zoneTickInterval}s for {config.zoneDuration}s");
            return true;
        }

        private static bool ExecuteMeleeAoEAtPosition(AbilityConfig config, Transform caster,
            NetworkObject casterNetObj, float dmgMult, Vector3 targetPosition)
        {
            int finalDamage = Mathf.Max(1, Mathf.RoundToInt(config.damage * dmgMult));

            var hits = Physics.OverlapSphere(targetPosition, config.radius, ~0, QueryTriggerInteraction.Ignore);
            bool hitAny = false;
            var processed = new System.Collections.Generic.HashSet<ulong>();

            foreach (var col in hits)
            {
                var no = col.GetComponentInParent<NetworkObject>();
                if (no == null) continue;
                if (no.NetworkObjectId == casterNetObj.NetworkObjectId) continue;
                if (no.IsPlayerObject) continue;
                if (!processed.Add(no.NetworkObjectId)) continue;

                var health = col.GetComponentInParent<NetworkHealth>();
                if (health == null) continue;

                Vector3 hitPoint = col.ClosestPoint(targetPosition);
                Vector3 knockDir = (col.transform.position - targetPosition).normalized;

                var info = new DamageInfo(finalDamage)
                {
                    Type = config.damageType,
                    AttackerClientId = casterNetObj.OwnerClientId,
                    HitPosition = hitPoint,
                    KnockImpulse = knockDir * 5f + Vector3.up * 2f
                };
                health.TakeDamage(info);

                ApplyStatusEffect(col, config, knockDir);

                var enemyAi = col.GetComponentInParent<EnemyAI>();
                if (enemyAi != null)
                {
                    if (health.Hp <= 0)
                        enemyAi.ApplyHitImpulse(info.KnockImpulse);
                    else
                        enemyAi.Stagger();
                }

                hitAny = true;
            }

            Debug.Log($"[Ability] MeleeAoE '{config.displayName}' — {processed.Count} enemies hit at {targetPosition} (targeted)");
            return hitAny;
        }

        // ──────────────────── Helpers ────────────────────

        private static void ApplyStatusEffect(Collider col, AbilityConfig config, Vector3 direction)
        {
            if (config.statusEffect == StatusEffectType.None) return;

            var sec = col.GetComponentInParent<StatusEffectController>();
            if (sec == null) return;

            Vector3 impulse = direction * config.statusImpulseForce;
            sec.ApplyEffect(config.statusEffect, config.statusDuration, impulse);
        }
    }
}
