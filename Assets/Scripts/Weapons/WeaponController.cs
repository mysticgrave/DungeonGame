using DungeonGame.Combat;
using DungeonGame.Classes;
using DungeonGame.Meta;
using DungeonGame.Player;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DungeonGame.Weapons
{
    /// <summary>
    /// Server-authoritative weapon. Config comes from the player's class (ClassDefinition.defaultWeapon)
    /// or from the equipped unlock (MetaProgression). Attack origin: leave empty to auto-use the camera.
    /// Teammates: ragdoll on hit (no health damage). Enemies: take damage. Owners see the weapon on FPS arms.
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

        [Header("Animation")]
        [Tooltip("Animator that drives the player body or FPS arms. Assign the one with the attack trigger.")]
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
        private bool? _hasAttackTrigger; // null = not checked, true/false = cached

        public WeaponConfig Config => _config != null ? _config : configFallback;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _attackTriggerId = Animator.StringToHash(attackTriggerName);
            TryResolveAttackOrigin();
            TryResolveConfig();
            TryResolveAnimator();
            TryParentWeaponVisual();
        }

        public override void OnNetworkDespawn()
        {
            if (_weaponVisualInstance != null)
            {
                Destroy(_weaponVisualInstance.gameObject);
                _weaponVisualInstance = null;
            }
            base.OnNetworkDespawn();
        }

        private void LateUpdate()
        {
            TryParentWeaponVisual();
        }

        private void Update()
        {
            if (!IsOwner) return;
            TryResolveAttackOrigin();
            if (!_resolvedConfig) TryResolveConfig();
            if (!_resolvedAnimator) TryResolveAnimator();

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

        private static bool HasTriggerParameter(Animator anim, int hash)
        {
            foreach (var p in anim.parameters)
                if (p.type == AnimatorControllerParameterType.Trigger && p.nameHash == hash) return true;
            return false;
        }

        private void TryResolveAttackOrigin()
        {
            if (_resolvedOrigin && attackOrigin != null) return;
            if (attackOrigin != null) { _resolvedOrigin = true; return; }
            var cam = GetComponentInChildren<Camera>();
            if (cam != null) { attackOrigin = cam.transform; _resolvedOrigin = true; return; }
            if (Camera.main != null) { attackOrigin = Camera.main.transform; _resolvedOrigin = true; return; }
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

            Transform target = null;
            if (IsOwner)
            {
                var fps = GetComponent<FPSArmsController>();
                if (fps != null && fps.FPSWeaponMount != null)
                    target = fps.FPSWeaponMount;
            }
            else if (weaponBoneAttach != null)
            {
                target = weaponBoneAttach;
            }

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

            if (c == null)
            {
                PerformDefaultMelee();
                return;
            }

            switch (c.attackType)
            {
                case WeaponAttackType.Melee:
                    PerformMelee(c);
                    break;
                case WeaponAttackType.Ranged:
                    PerformRanged(c);
                    break;
                case WeaponAttackType.Magic:
                    PerformMagic(c);
                    break;
                default:
                    PerformMelee(c);
                    break;
            }
        }

        private void PerformDefaultMelee()
        {
            Transform originT = attackOrigin != null ? attackOrigin : transform;
            Vector3 pos = originT.position;
            Vector3 dir = originT.forward;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
            dir.Normalize();
            Vector3 center = pos + dir * (defaultRange * 0.5f);
            var hits = Physics.OverlapSphere(center, defaultHitRadius, ~0, QueryTriggerInteraction.Ignore);
            foreach (var col in hits)
                ProcessMeleeHit(col, dir, defaultDamage);
        }

        private void PerformMelee(WeaponConfig c)
        {
            Vector3 pos = attackOrigin.position;
            Vector3 dir = attackOrigin.forward;
            dir.y = 0;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
            dir.Normalize();

            var hits = Physics.OverlapSphere(pos + dir * c.range * 0.5f, c.hitRadius, ~0, QueryTriggerInteraction.Ignore);
            foreach (var col in hits)
                ProcessMeleeHit(col, dir, c.damage);
        }

        private void ProcessMeleeHit(Collider col, Vector3 attackDir, int damage)
        {
            var no = col.GetComponentInParent<NetworkObject>();
            if (no != null && no.NetworkObjectId == NetworkObject.NetworkObjectId)
                return;

            var knock = col.GetComponentInParent<KnockableCapsule>();
            if (knock != null && no != null && no.IsPlayerObject)
            {
                Vector3 impulse = attackDir * teammateKnockForward + Vector3.up * teammateKnockUp;
                knock.KnockFromServer(impulse, teammateKnockDuration);
                return;
            }

            var health = col.GetComponentInParent<NetworkHealth>();
            if (health == null) return;
            if (no != null && no.IsPlayerObject) return;
            health.TakeDamage(damage);
        }

        private void PerformRanged(WeaponConfig c)
        {
            Vector3 origin = attackOrigin.position;
            Vector3 dir = attackOrigin.forward;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
            dir.Normalize();

            if (!Physics.Raycast(origin, dir, out var hit, c.range, c.hitLayers, QueryTriggerInteraction.Ignore))
                return;
            var damageable = hit.collider.GetComponentInParent<NetworkHealth>();
            if (damageable == null) return;
            if (damageable.NetworkObject != null && damageable.NetworkObject.IsPlayerObject) return;
            damageable.TakeDamage(c.damage);
        }

        private void PerformMagic(WeaponConfig c)
        {
            Vector3 origin = attackOrigin.position;
            Vector3 dir = attackOrigin.forward;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
            dir.Normalize();

            if (!Physics.Raycast(origin, dir, out var hit, c.range, c.hitLayers, QueryTriggerInteraction.Ignore))
                return;
            var damageable = hit.collider.GetComponentInParent<NetworkHealth>();
            if (damageable == null) return;
            if (damageable.NetworkObject != null && damageable.NetworkObject.IsPlayerObject) return;
            damageable.TakeDamage(c.damage);
        }
    }
}
