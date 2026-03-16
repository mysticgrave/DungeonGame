using System;
using DungeonGame.Combat;
using DungeonGame.Enemies;
using DungeonGame.Player;
using DungeonGame.Weapons;
using Unity.Netcode;
using UnityEngine;

namespace DungeonGame.Items
{
    /// <summary>
    /// Knock/ragdoll settings passed to attack methods.
    /// </summary>
    public struct KnockSettings
    {
        public float TeammateKnockForward;
        public float TeammateKnockUp;
        public float TeammateKnockDuration;
        public float EnemyKnockForward;
        public float EnemyKnockUp;
    }

    /// <summary>
    /// Static utility for executing weapon attacks. Extracted from WeaponController
    /// so both WeaponController and HandSystem can share the same combat logic.
    /// All methods run server-side only.
    /// </summary>
    public static class ItemAttackExecutor
    {
        /// <summary>Enable to draw Debug.DrawRay for attack shapes (visible in Scene view).</summary>
        public static bool DebugDraw;

        /// <summary>Fired after any attack is executed. bool = at least one target was hit.</summary>
        public static event Action<bool> OnAttackFired;

        public static void PerformMelee(Transform attackOrigin, WeaponConfig config,
            NetworkObject attacker, KnockSettings knock)
        {
            Vector3 pos = attackOrigin.position;
            Vector3 dir = attackOrigin.forward;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
            dir.Normalize();

            Vector3 center = pos + dir * config.range * 0.5f;
            var hits = Physics.OverlapSphere(center, config.hitRadius,
                ~0, QueryTriggerInteraction.Ignore);

            if (DebugDraw)
            {
                // Draw attack sphere as cross lines
                DrawDebugSphere(center, config.hitRadius, hits.Length > 0 ? Color.green : Color.red, 0.5f);
                Debug.DrawRay(pos, dir * config.range, Color.yellow, 0.5f);
            }

            bool didHit = false;
            foreach (var col in hits)
            {
                if (ProcessMeleeHit(col, dir, config.damage, attacker, knock))
                    didHit = true;
            }
            OnAttackFired?.Invoke(didHit);
        }

        public static void PerformDefaultMelee(Transform attackOrigin, int damage, float range,
            float hitRadius, NetworkObject attacker, KnockSettings knock)
        {
            Vector3 pos = attackOrigin.position;
            Vector3 dir = attackOrigin.forward;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
            dir.Normalize();

            Vector3 center = pos + dir * (range * 0.5f);
            var hits = Physics.OverlapSphere(center, hitRadius, ~0, QueryTriggerInteraction.Ignore);

            if (DebugDraw)
            {
                DrawDebugSphere(center, hitRadius, hits.Length > 0 ? Color.green : Color.red, 0.5f);
                Debug.DrawRay(pos, dir * range, Color.yellow, 0.5f);
            }

            bool didHit = false;
            foreach (var col in hits)
            {
                if (ProcessMeleeHit(col, dir, damage, attacker, knock))
                    didHit = true;
            }
            OnAttackFired?.Invoke(didHit);
        }

        public static void PerformRanged(Transform attackOrigin, WeaponConfig config,
            NetworkObject attacker)
        {
            Vector3 origin = attackOrigin.position;
            Vector3 dir = attackOrigin.forward;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
            dir.Normalize();

            bool didHit = false;
            if (Physics.Raycast(origin, dir, out var hit, config.range, config.hitLayers,
                QueryTriggerInteraction.Ignore))
            {
                var damageable = hit.collider.GetComponentInParent<NetworkHealth>();
                if (damageable != null &&
                    !(damageable.NetworkObject != null && damageable.NetworkObject.IsPlayerObject))
                {
                    var info = new DamageInfo(config.damage)
                    {
                        AttackerClientId = attacker.OwnerClientId,
                        HitPosition = hit.point
                    };
                    damageable.TakeDamage(info);
                    didHit = true;
                }
            }

            if (DebugDraw)
                Debug.DrawRay(origin, dir * config.range, didHit ? Color.green : Color.red, 0.5f);

            OnAttackFired?.Invoke(didHit);
        }

        public static void PerformMagic(Transform attackOrigin, WeaponConfig config,
            NetworkObject attacker)
        {
            Vector3 origin = attackOrigin.position;
            Vector3 dir = attackOrigin.forward;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
            dir.Normalize();

            bool didHit = false;
            if (Physics.Raycast(origin, dir, out var hit, config.range, config.hitLayers,
                QueryTriggerInteraction.Ignore))
            {
                var damageable = hit.collider.GetComponentInParent<NetworkHealth>();
                if (damageable != null &&
                    !(damageable.NetworkObject != null && damageable.NetworkObject.IsPlayerObject))
                {
                    var info = new DamageInfo(config.damage)
                    {
                        AttackerClientId = attacker.OwnerClientId,
                        HitPosition = hit.point
                    };
                    damageable.TakeDamage(info);
                    didHit = true;
                }
            }

            if (DebugDraw)
                Debug.DrawRay(origin, dir * config.range, didHit ? Color.cyan : Color.blue, 0.5f);

            OnAttackFired?.Invoke(didHit);
        }

        public static void ExecuteAttack(Transform attackOrigin, WeaponConfig config,
            NetworkObject attacker, KnockSettings knock)
        {
            switch (config.attackType)
            {
                case WeaponAttackType.Melee:
                    PerformMelee(attackOrigin, config, attacker, knock);
                    break;
                case WeaponAttackType.Ranged:
                    PerformRanged(attackOrigin, config, attacker);
                    break;
                case WeaponAttackType.Magic:
                    PerformMagic(attackOrigin, config, attacker);
                    break;
                default:
                    PerformMelee(attackOrigin, config, attacker, knock);
                    break;
            }
        }

        /// <summary>Returns true if a valid target was hit (enemy or teammate).</summary>
        public static bool ProcessMeleeHit(Collider col, Vector3 attackDir, int damage,
            NetworkObject attacker, KnockSettings knock)
        {
            var no = col.GetComponentInParent<NetworkObject>();
            if (no != null && no.NetworkObjectId == attacker.NetworkObjectId)
                return false;

            // Teammate: ragdoll only, no health damage
            var knockable = col.GetComponentInParent<KnockableCapsule>();
            if (knockable != null && no != null && no.IsPlayerObject)
            {
                Vector3 impulse = attackDir * knock.TeammateKnockForward + Vector3.up * knock.TeammateKnockUp;
                knockable.KnockFromServer(impulse, knock.TeammateKnockDuration);
                return true;
            }

            // Enemy: take damage + optional ragdoll
            var health = col.GetComponentInParent<NetworkHealth>();
            if (health == null) return false;
            if (no != null && no.IsPlayerObject) return false;

            Vector3 enemyImpulse = attackDir * knock.EnemyKnockForward + Vector3.up * knock.EnemyKnockUp;
            var enemyAi = col.GetComponentInParent<EnemyAI>();

            // Build rich DamageInfo
            Vector3 hitPoint = col.ClosestPoint(attacker.transform.position);
            var info = new DamageInfo(damage)
            {
                AttackerClientId = attacker.OwnerClientId,
                HitPosition = hitPoint,
                KnockImpulse = enemyImpulse
            };
            health.TakeDamage(info);

            if (enemyAi != null && (enemyAi.Config == null || enemyAi.Config.canBeRagdolled))
            {
                if (health.Hp <= 0)
                    enemyAi.ApplyHitImpulse(enemyImpulse);
                else
                    enemyAi.Ragdoll(enemyImpulse);
            }

            return true;
        }

        private static void DrawDebugSphere(Vector3 center, float radius, Color color, float duration)
        {
            // Approximate sphere with 3 circles (XY, XZ, YZ planes)
            int segments = 16;
            for (int i = 0; i < segments; i++)
            {
                float a1 = (float)i / segments * Mathf.PI * 2f;
                float a2 = (float)(i + 1) / segments * Mathf.PI * 2f;

                // XZ circle
                var p1 = center + new Vector3(Mathf.Cos(a1) * radius, 0f, Mathf.Sin(a1) * radius);
                var p2 = center + new Vector3(Mathf.Cos(a2) * radius, 0f, Mathf.Sin(a2) * radius);
                Debug.DrawLine(p1, p2, color, duration);

                // XY circle
                p1 = center + new Vector3(Mathf.Cos(a1) * radius, Mathf.Sin(a1) * radius, 0f);
                p2 = center + new Vector3(Mathf.Cos(a2) * radius, Mathf.Sin(a2) * radius, 0f);
                Debug.DrawLine(p1, p2, color, duration);

                // YZ circle
                p1 = center + new Vector3(0f, Mathf.Cos(a1) * radius, Mathf.Sin(a1) * radius);
                p2 = center + new Vector3(0f, Mathf.Cos(a2) * radius, Mathf.Sin(a2) * radius);
                Debug.DrawLine(p1, p2, color, duration);
            }
        }
    }
}
