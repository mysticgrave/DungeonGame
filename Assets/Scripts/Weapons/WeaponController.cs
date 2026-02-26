using DungeonGame.Combat;
using DungeonGame.Classes;
using DungeonGame.Meta;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DungeonGame.Weapons
{
    /// <summary>
    /// Server-authoritative weapon. Config comes from the player's class (ClassDefinition.defaultWeapon)
    /// or from the equipped unlock (MetaProgression). Attack origin is auto-found from the player's camera.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class WeaponController : NetworkBehaviour
    {
        [Tooltip("Fallback if class has no default weapon and no equipped unlock.")]
        [SerializeField] private WeaponConfig configFallback;
        [SerializeField] private Transform attackOrigin;

        private WeaponConfig _config;
        private int _weaponIndex;
        private float _nextAttackTime;
        private bool _resolvedConfig;
        private bool _resolvedOrigin;

        public WeaponConfig Config => _config != null ? _config : configFallback;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsOwner) enabled = false;
            TryResolveAttackOrigin();
            TryResolveConfig();
        }

        private void Update()
        {
            if (!IsOwner) return;
            TryResolveAttackOrigin();
            if (!_resolvedConfig) TryResolveConfig();

            var c = Config;
            if (c == null)
            {
                // Assign configFallback on WeaponController, or add a default weapon to the player's class / WeaponRegistry.
                return;
            }
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;
            if (Time.time < _nextAttackTime) return;

            AttackServerRpc(_weaponIndex >= 0 ? _weaponIndex : WeaponRegistry.IndexOf(Config));
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

        [ServerRpc]
        private void AttackServerRpc(int weaponIndex)
        {
            var c = weaponIndex >= 0 ? WeaponRegistry.GetByIndex(weaponIndex) : Config;
            if (c == null) return;
            if (Time.time < _nextAttackTime) return;

            _nextAttackTime = Time.time + c.cooldown;

            if (attackOrigin == null) attackOrigin = transform;
            if (c.attackType == WeaponAttackType.Melee)
                PerformMelee(c);
            else
                PerformRanged(c);
        }

        private void PerformMelee(WeaponConfig c)
        {
            Vector3 origin = attackOrigin.position;
            Vector3 dir = attackOrigin.forward;
            dir.y = 0;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
            dir.Normalize();

            var hits = Physics.OverlapSphere(origin + dir * c.range * 0.5f, c.hitRadius, ~0, QueryTriggerInteraction.Ignore);
            foreach (var col in hits)
            {
                var damageable = col.GetComponentInParent<NetworkHealth>();
                if (damageable == null) continue;
                if (damageable.NetworkObject != null && damageable.NetworkObject.IsPlayerObject) continue;
                damageable.TakeDamage(c.damage);
            }
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
    }
}
