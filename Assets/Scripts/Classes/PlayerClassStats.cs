using DungeonGame.Player;
using Unity.Netcode;
using UnityEngine;

namespace DungeonGame.Classes
{
    /// <summary>
    /// Bridge between ClassDefinition data and runtime player components.
    /// Listens for class changes and pushes stats to PlayerHealth, ThirdPersonMotor, etc.
    /// Also exposes damage multiplier and reduction for the combat pipeline.
    /// </summary>
    public class PlayerClassStats : NetworkBehaviour
    {
        private readonly NetworkVariable<float> _damageMultiplier = new(1f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> _damageReduction = new(0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        // Temporary speed boost from abilities
        private readonly NetworkVariable<float> _speedBoostExpiry = new(0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> _speedBoostMultiplier = new(1f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private PlayerClass _playerClass;
        private ThirdPersonMotor _motor;
        private float _baseSpeed;

        /// <summary>Damage multiplier from class stats. Applied in ItemAttackExecutor.</summary>
        public float DamageMultiplier => _damageMultiplier.Value;

        /// <summary>Damage reduction from class stats. Applied in PlayerHealth.TakeDamage.</summary>
        public float DamageReduction => _damageReduction.Value;

        /// <summary>Whether a speed boost is currently active.</summary>
        public bool HasSpeedBoost => Time.time < _speedBoostExpiry.Value;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            _playerClass = GetComponent<PlayerClass>();
            _motor = GetComponent<ThirdPersonMotor>();

            if (_playerClass != null)
            {
                _playerClass.OnClassIndexChanged += OnClassChanged;

                // Apply immediately if class is already set
                if (_playerClass.ClassIndex >= 0)
                    OnClassChanged(-1, _playerClass.ClassIndex);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (_playerClass != null)
                _playerClass.OnClassIndexChanged -= OnClassChanged;
            base.OnNetworkDespawn();
        }

        private void OnClassChanged(int previousIndex, int newIndex)
        {
            if (!IsServer) return;

            var def = ClassRegistry.GetByIndex(newIndex);
            if (def == null)
            {
                Debug.LogWarning($"[PlayerClassStats] Class index {newIndex} not found in registry.");
                return;
            }

            ApplyStats(def);
        }

        private void ApplyStats(ClassDefinition def)
        {
            // Health
            var health = GetComponent<PlayerHealth>();
            if (health != null)
                health.SetMaxHp(def.baseMaxHp);

            // Movement speed
            if (_motor != null)
            {
                _baseSpeed = def.baseMoveSpeed;
                _motor.SetMoveSpeed(def.baseMoveSpeed);
            }

            // Combat modifiers
            _damageMultiplier.Value = def.baseDamageMultiplier;
            _damageReduction.Value = def.baseDamageReduction;

            Debug.Log($"[PlayerClassStats] Applied stats for '{def.displayName}': " +
                      $"HP={def.baseMaxHp}, Speed={def.baseMoveSpeed}, " +
                      $"DmgMult={def.baseDamageMultiplier:F2}, DmgReduc={def.baseDamageReduction:F2}");
        }

        /// <summary>
        /// Server-only. Apply a temporary speed boost (from abilities).
        /// </summary>
        public void ApplySpeedBoost(float multiplier, float duration)
        {
            if (!IsServer) return;
            _speedBoostMultiplier.Value = multiplier;
            _speedBoostExpiry.Value = Time.time + duration;

            if (_motor != null)
                _motor.SetMoveSpeed(_baseSpeed * multiplier);
        }

        private void Update()
        {
            if (!IsServer) return;

            // Check if speed boost expired
            if (_speedBoostExpiry.Value > 0f && Time.time >= _speedBoostExpiry.Value)
            {
                _speedBoostExpiry.Value = 0f;
                _speedBoostMultiplier.Value = 1f;

                if (_motor != null)
                    _motor.SetMoveSpeed(_baseSpeed);
            }
        }
    }
}
