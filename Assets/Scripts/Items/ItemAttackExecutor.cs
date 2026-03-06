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
        public static void PerformMelee(Transform attackOrigin, WeaponConfig config,
            NetworkObject attacker, KnockSettings knock)
        {
            Vector3 pos = attackOrigin.position;
            Vector3 dir = attackOrigin.forward;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
            dir.Normalize();

            var hits = Physics.OverlapSphere(pos + dir * config.range * 0.5f, config.hitRadius,
                ~0, QueryTriggerInteraction.Ignore);
            foreach (var col in hits)
                ProcessMeleeHit(col, dir, config.damage, attacker, knock);
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
            foreach (var col in hits)
                ProcessMeleeHit(col, dir, damage, attacker, knock);
        }

        public static void PerformRanged(Transform attackOrigin, WeaponConfig config,
            NetworkObject attacker)
        {
            Vector3 origin = attackOrigin.position;
            Vector3 dir = attackOrigin.forward;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
            dir.Normalize();

            if (!Physics.Raycast(origin, dir, out var hit, config.range, config.hitLayers,
                QueryTriggerInteraction.Ignore))
                return;

            var damageable = hit.collider.GetComponentInParent<NetworkHealth>();
            if (damageable == null) return;
            if (damageable.NetworkObject != null && damageable.NetworkObject.IsPlayerObject) return;
            damageable.TakeDamage(config.damage);
        }

        public static void PerformMagic(Transform attackOrigin, WeaponConfig config,
            NetworkObject attacker)
        {
            Vector3 origin = attackOrigin.position;
            Vector3 dir = attackOrigin.forward;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
            dir.Normalize();

            if (!Physics.Raycast(origin, dir, out var hit, config.range, config.hitLayers,
                QueryTriggerInteraction.Ignore))
                return;

            var damageable = hit.collider.GetComponentInParent<NetworkHealth>();
            if (damageable == null) return;
            if (damageable.NetworkObject != null && damageable.NetworkObject.IsPlayerObject) return;
            damageable.TakeDamage(config.damage);
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

        public static void ProcessMeleeHit(Collider col, Vector3 attackDir, int damage,
            NetworkObject attacker, KnockSettings knock)
        {
            var no = col.GetComponentInParent<NetworkObject>();
            if (no != null && no.NetworkObjectId == attacker.NetworkObjectId)
                return;

            // Teammate: ragdoll only, no health damage
            var knockable = col.GetComponentInParent<KnockableCapsule>();
            if (knockable != null && no != null && no.IsPlayerObject)
            {
                Vector3 impulse = attackDir * knock.TeammateKnockForward + Vector3.up * knock.TeammateKnockUp;
                knockable.KnockFromServer(impulse, knock.TeammateKnockDuration);
                return;
            }

            // Enemy: take damage + optional ragdoll
            var health = col.GetComponentInParent<NetworkHealth>();
            if (health == null) return;
            if (no != null && no.IsPlayerObject) return;

            Vector3 enemyImpulse = attackDir * knock.EnemyKnockForward + Vector3.up * knock.EnemyKnockUp;
            var enemyAi = col.GetComponentInParent<EnemyAI>();

            health.TakeDamage(damage);

            if (enemyAi != null && (enemyAi.Config == null || enemyAi.Config.canBeRagdolled))
            {
                if (health.Hp <= 0)
                    enemyAi.ApplyHitImpulse(enemyImpulse);
                else
                    enemyAi.Ragdoll(enemyImpulse);
            }
        }
    }
}
