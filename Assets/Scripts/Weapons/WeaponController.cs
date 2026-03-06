using DungeonGame.Classes;
using DungeonGame.Items;
using DungeonGame.UI;
using DungeonGame.Meta;
using DungeonGame.Player;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DungeonGame.Weapons
{
    /// <summary>
    /// Legacy server-authoritative weapon controller. When a HandSystem component is present,
    /// this component defers all input and visual handling to it. Without HandSystem, it
    /// preserves the original single-weapon behavior for backward compatibility.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class WeaponController : NetworkBehaviour
    {
        [Tooltip("Fallback if class has no default weapon and no equipped unlock. If still null, uses inline default melee.")]
        [SerializeField] private WeaponConfig configFallback;
        [SerializeField] private Transform attackOrigin;

        [Header("Weapon visual (dual display)")]
        [Tooltip("Sword/weapon mesh. Use Prefab if the weapon lives in its own prefab (avoids 'Transform in Prefab asset' error).")]
        [SerializeField] private GameObject weaponVisualPrefab;
        [Tooltip("Or assign a Transform already in the Player hierarchy. Ignored if weaponVisualPrefab is set.")]
        [SerializeField] private Transform weaponVisual;
        [Tooltip("Hand bone on the 3P rig (other players see the weapon here).")]
        [SerializeField] private Transform weaponBoneAttach;

        private Transform _weaponVisualInstance;

        [Header("Default (when no config)")]
        [Tooltip("Used when no WeaponConfig is available so left-click still works.")]
        [SerializeField] private int defaultDamage = 2;
        [SerializeField] private float defaultRange = 2.5f;
        [SerializeField] private float defaultCooldown = 0.6f;
        [SerializeField] private float defaultHitRadius = 0.5f;

        [Header("Teammate knock (no health damage)")]
        [Tooltip("When hitting a teammate, ragdoll them. Forward = attack direction * this; Up = vertical boost. No health is reduced.")]
        [SerializeField] private float teammateKnockForward = 6f;
        [SerializeField] private float teammateKnockUp = 2f;
        [Tooltip("Ragdoll duration when hitting a teammate (seconds).")]
        [SerializeField] private float teammateKnockDuration = 3f;

        [Header("Enemy knock (on damage)")]
        [Tooltip("When hitting an enemy, ragdoll them in the attack direction. Forward = dir * this; Up = vertical boost.")]
        [SerializeField] private float enemyKnockForward = 8f;
        [SerializeField] private float enemyKnockUp = 3f;

        [Header("Animation")]
        [Tooltip("Animator that drives the player. Assign the one with the attack trigger.")]
        [SerializeField] private Animator animator;
        [Tooltip("Trigger parameter name in the Animator (e.g. attack_sword_01 or Sword_Attack_1).")]
        [SerializeField] private string attackTriggerName = "attack_sword_01";

        private WeaponConfig _config;
        private int _weaponIndex;
        private float _nextAttackTime;
        private bool _resolvedConfig;
        private bool _resolvedOrigin;
        private bool _resolvedAnimator;
        private int _attackTriggerId;
        private bool? _hasAttackTrigger;
        private bool _handSystemPresent;

        public WeaponConfig Config => _config != null ? _config : configFallback;

        /// <summary>Attack origin transform, resolved lazily. Used by HandSystem.</summary>
        public Transform AttackOrigin
        {
            get
            {
                TryResolveAttackOrigin();
                return attackOrigin;
            }
        }

        /// <summary>Knock settings from inspector, used by HandSystem for shared attack logic.</summary>
        public KnockSettings GetKnockSettings() => new KnockSettings
        {
            TeammateKnockForward = teammateKnockForward,
            TeammateKnockUp = teammateKnockUp,
            TeammateKnockDuration = teammateKnockDuration,
            EnemyKnockForward = enemyKnockForward,
            EnemyKnockUp = enemyKnockUp,
        };

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _handSystemPresent = GetComponent<HandSystem>() != null;
            _attackTriggerId = Animator.StringToHash(attackTriggerName);
            TryResolveAttackOrigin();
            TryResolveConfig();
            TryResolveAnimator();

            if (!_handSystemPresent)
                TryParentWeaponVisual();
        }

        public override void OnNetworkDespawn()
        {
            if (!_handSystemPresent && _weaponVisualInstance != null)
            {
                Destroy(_weaponVisualInstance.gameObject);
                _weaponVisualInstance = null;
            }
            base.OnNetworkDespawn();
        }

        private void LateUpdate()
        {
            if (!_handSystemPresent)
                TryParentWeaponVisual();
        }

        private void Update()
        {
            if (!IsOwner) return;
            if (_handSystemPresent) return;

            TryResolveAttackOrigin();
            if (!_resolvedConfig) TryResolveConfig();
            if (!_resolvedAnimator) TryResolveAnimator();

            if (PauseMenuController.IsPaused) return;
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;
            if (Time.time < _nextAttackTime) return;

            var c = Config;
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                if (!_hasAttackTrigger.HasValue)
                    _hasAttackTrigger = HasTriggerParameter(animator, _attackTriggerId);
                if (_hasAttackTrigger.Value)
                    animator.SetTrigger(_attackTriggerId);
            }

            AttackServerRpc(_weaponIndex >= 0 ? _weaponIndex : (c != null ? WeaponRegistry.IndexOf(c) : -1));
        }

        private void TryResolveAnimator()
        {
            if (_resolvedAnimator) return;
            if (animator != null) { _resolvedAnimator = true; return; }
            animator = GetComponentInChildren<Animator>(true);
            _resolvedAnimator = true;
            if (animator != null && animator.runtimeAnimatorController != null && !HasTriggerParameter(animator, _attackTriggerId))
                Debug.LogWarning($"[WeaponController] Animator has no trigger '{attackTriggerName}'. Add it in the Animator (Parameters → Trigger) and ensure transitions use it.", this);
        }

        public static bool HasTriggerParameter(Animator anim, int hash)
        {
            foreach (var p in anim.parameters)
                if (p.type == AnimatorControllerParameterType.Trigger && p.nameHash == hash) return true;
            return false;
        }

        private void TryResolveAttackOrigin()
        {
            if (_resolvedOrigin && attackOrigin != null) return;
            if (attackOrigin != null) { _resolvedOrigin = true; return; }
            attackOrigin = transform;
            _resolvedOrigin = true;
        }

        private void TryResolveConfig()
        {
            if (_config != null) { _resolvedConfig = true; return; }
            var meta = MetaProgression.Instance;
            var playerClass = GetComponent<PlayerClass>();
            var def = playerClass != null ? playerClass.Definition : null;

            if (meta != null)
            {
                string equippedId = meta.GetEquippedWeaponId();
                if (!string.IsNullOrEmpty(equippedId) && meta.IsWeaponUnlocked(equippedId))
                {
                    var reg = WeaponRegistry.Get(equippedId);
                    if (reg != null) { _config = reg; _weaponIndex = WeaponRegistry.IndexOf(reg); _resolvedConfig = true; return; }
                }
            }

            if (def != null && def.defaultWeapon != null) { _config = def.defaultWeapon; _weaponIndex = WeaponRegistry.IndexOf(def.defaultWeapon); _resolvedConfig = true; return; }
            _config = configFallback;
            if (_config == null && WeaponRegistry.Instance != null && WeaponRegistry.GetAll().Count > 0)
            {
                _config = WeaponRegistry.GetByIndex(0);
                _weaponIndex = 0;
            }
            else
                _weaponIndex = configFallback != null ? WeaponRegistry.IndexOf(configFallback) : -1;
            _resolvedConfig = true;
        }

        private void TryParentWeaponVisual()
        {
            Transform visual = ResolveWeaponVisual();
            if (visual == null) return;

            Transform target = weaponBoneAttach;

            if (target != null && visual.parent != target)
            {
                visual.SetParent(target, false);
                visual.localPosition = Vector3.zero;
                visual.localRotation = Quaternion.identity;
                visual.localScale = Vector3.one;
            }
        }

        private Transform ResolveWeaponVisual()
        {
            if (weaponVisualPrefab != null)
            {
                if (_weaponVisualInstance == null)
                {
                    _weaponVisualInstance = Instantiate(weaponVisualPrefab).transform;
                    _weaponVisualInstance.name = "WeaponVisual";
                }
                return _weaponVisualInstance;
            }
            return weaponVisual;
        }

        [ServerRpc]
        private void AttackServerRpc(int weaponIndex)
        {
            var c = weaponIndex >= 0 ? WeaponRegistry.GetByIndex(weaponIndex) : Config;
            float cooldown = c != null ? c.cooldown : defaultCooldown;
            if (Time.time < _nextAttackTime) return;
            _nextAttackTime = Time.time + cooldown;

            if (attackOrigin == null) attackOrigin = transform;

            var knock = GetKnockSettings();

            if (c == null)
            {
                ItemAttackExecutor.PerformDefaultMelee(attackOrigin, defaultDamage, defaultRange,
                    defaultHitRadius, NetworkObject, knock);
                return;
            }

            ItemAttackExecutor.ExecuteAttack(attackOrigin, c, NetworkObject, knock);
        }
    }
}
