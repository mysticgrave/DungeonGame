using System;
using System.Collections.Generic;
using DungeonGame.Combat;
using DungeonGame.Player;
using UnityEngine;

namespace DungeonGame.Enemies
{
    /// <summary>
    /// Manages active status effects on a character (enemy or player).
    /// Server-authoritative: only the server ticks effects and applies damage.
    /// Attach to any GameObject that has NetworkHealth (or PlayerHealth).
    /// Multiple different effect types can be active simultaneously.
    /// </summary>
    public class StatusEffectController : MonoBehaviour
    {
        private readonly List<StatusEffectInstance> _active = new();
        private readonly HashSet<StatusEffectType> _immunities = new();

        private IDamageable _damageable;
        private KnockableCapsule _knockable;
        private PlayerBodyStateMachine _bodyStateMachine;

        /// <summary>Fired when an effect is added or removed. Useful for UI/VFX.</summary>
        public event Action<StatusEffectType, bool> OnEffectChanged;

        /// <summary>Current speed multiplier from all active slow/freeze effects (1 = normal).</summary>
        public float SpeedMultiplier { get; private set; } = 1f;

        private void Awake()
        {
            _damageable = GetComponent<IDamageable>();
            _knockable = GetComponent<KnockableCapsule>();
            _bodyStateMachine = GetComponent<PlayerBodyStateMachine>();
        }

        public void SetImmunities(StatusEffectType[] types)
        {
            _immunities.Clear();
            if (types == null) return;
            foreach (var t in types)
                _immunities.Add(t);
        }

        public bool HasEffect(StatusEffectType type)
        {
            foreach (var e in _active)
                if (e.Type == type && !e.IsExpired) return true;
            return false;
        }

        /// <summary>Apply a status effect. Multiple different types stack simultaneously.</summary>
        public void ApplyEffect(StatusEffectType type, float duration)
        {
            ApplyEffect(type, duration, Vector3.zero);
        }

        /// <summary>Apply a status effect with an optional impulse direction (used by Ragdoll).</summary>
        public void ApplyEffect(StatusEffectType type, float duration, Vector3 impulse)
        {
            if (type == StatusEffectType.None) return;
            if (_immunities.Contains(type)) return;

            // Ragdoll is instant — triggers the player ragdoll system directly, no ticking needed.
            if (type == StatusEffectType.Ragdoll)
            {
                TriggerRagdoll(impulse, duration);
                return;
            }

            // Refresh duration if the same type is already active.
            foreach (var existing in _active)
            {
                if (existing.Type == type)
                {
                    existing.RemainingDuration = Mathf.Max(existing.RemainingDuration, duration);
                    return;
                }
            }

            var inst = new StatusEffectInstance
            {
                Type = type,
                RemainingDuration = duration,
            };

            switch (type)
            {
                case StatusEffectType.Poison:
                    inst.TickInterval = 1f;
                    inst.DamagePerTick = 1;
                    break;
                case StatusEffectType.Burn:
                    inst.TickInterval = 0.5f;
                    inst.DamagePerTick = 1;
                    break;
                case StatusEffectType.Bleed:
                    inst.TickInterval = 1f;
                    inst.DamagePerTick = 1;
                    break;
                case StatusEffectType.Slow:
                    inst.SpeedMultiplier = 0.5f;
                    break;
                case StatusEffectType.Freeze:
                    inst.SpeedMultiplier = 0f;
                    break;
                case StatusEffectType.Stun:
                    inst.SpeedMultiplier = 0f;
                    break;
                case StatusEffectType.Weaken:
                    break;
            }

            _active.Add(inst);
            OnEffectChanged?.Invoke(type, true);
        }

        private void TriggerRagdoll(Vector3 impulse, float duration)
        {
            if (_bodyStateMachine != null)
            {
                _bodyStateMachine.EnterRagdoll(impulse, duration);
                return;
            }

            if (_knockable != null)
            {
                _knockable.KnockFromServer(impulse, duration);
                return;
            }

            Debug.LogWarning($"[StatusFX] Ragdoll effect applied but no PlayerBodyStateMachine or KnockableCapsule found on {gameObject.name}");
        }

        public void RemoveEffect(StatusEffectType type)
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                if (_active[i].Type == type)
                {
                    _active.RemoveAt(i);
                    OnEffectChanged?.Invoke(type, false);
                    return;
                }
            }
        }

        public void ClearAll()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var t = _active[i].Type;
                _active.RemoveAt(i);
                OnEffectChanged?.Invoke(t, false);
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            float lowestSpeed = 1f;

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var fx = _active[i];
                fx.RemainingDuration -= dt;

                if (fx.IsExpired)
                {
                    var t = fx.Type;
                    _active.RemoveAt(i);
                    OnEffectChanged?.Invoke(t, false);
                    continue;
                }

                // DoT ticks
                if (fx.DamagePerTick > 0 && fx.TickInterval > 0f && Time.time >= fx.NextTickAt)
                {
                    fx.NextTickAt = Time.time + fx.TickInterval;
                    _damageable?.TakeDamage(fx.DamagePerTick);
                }

                if (fx.SpeedMultiplier < lowestSpeed)
                    lowestSpeed = fx.SpeedMultiplier;
            }

            SpeedMultiplier = lowestSpeed;
        }
    }
}
