using DungeonGame.Combat;
using DungeonGame.Weapons;
using Unity.Netcode;
using UnityEngine;

namespace DungeonGame.Items
{
    /// <summary>
    /// Physical item in the world. Has a Rigidbody for physics when free, and hides
    /// itself when held by a player. Server-authoritative hold state.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(Rigidbody))]
    public class WorldItem : NetworkBehaviour
    {
        [SerializeField] private WeaponConfig itemConfig;

        [Header("Throw Damage")]
        [Tooltip("If true, this item can deal damage on impact after being thrown.")]
        [SerializeField] private bool enableThrowDamage = true;

        private readonly NetworkVariable<bool> _isHeld = new(false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<ulong> _holderClientId = new(0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private Rigidbody _rb;
        private Collider[] _colliders;
        private Renderer[] _renderers;
        private bool _wasThrown;
        private bool _throwDamageApplied;

        public WeaponConfig ItemConfig => itemConfig;
        public bool IsHeld => _isHeld.Value;
        public ulong HolderClientId => _holderClientId.Value;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _rb = GetComponent<Rigidbody>();
            _colliders = GetComponentsInChildren<Collider>(true);
            _renderers = GetComponentsInChildren<Renderer>(true);

            _isHeld.OnValueChanged += OnHeldChanged;
            ApplyHeldState(_isHeld.Value);
        }

        public override void OnNetworkDespawn()
        {
            _isHeld.OnValueChanged -= OnHeldChanged;
            base.OnNetworkDespawn();
        }

        private void OnHeldChanged(bool prev, bool cur)
        {
            ApplyHeldState(cur);
        }

        private void ApplyHeldState(bool held)
        {
            if (_rb != null)
            {
                if (held)
                {
                    if (!_rb.isKinematic)
                    {
                        _rb.linearVelocity = Vector3.zero;
                        _rb.angularVelocity = Vector3.zero;
                    }
                }
                _rb.isKinematic = held;
            }

            foreach (var col in _colliders)
                col.enabled = !held;

            foreach (var rend in _renderers)
                rend.enabled = !held;
        }

        /// <summary>Server-only: mark this item as picked up by a player.</summary>
        public void ServerPickup(ulong clientId)
        {
            _isHeld.Value = true;
            _holderClientId.Value = clientId;
            _wasThrown = false;
            _throwDamageApplied = false;

            if (_rb != null)
            {
                if (!_rb.isKinematic)
                {
                    _rb.linearVelocity = Vector3.zero;
                    _rb.angularVelocity = Vector3.zero;
                }
                _rb.isKinematic = true;
            }
        }

        /// <summary>Server-only: drop this item at a position with optional velocity (for throws).</summary>
        public void ServerDrop(Vector3 position, Vector3 velocity)
        {
            _isHeld.Value = false;
            _holderClientId.Value = 0;

            transform.position = position;

            if (_rb != null)
            {
                _rb.isKinematic = false;
                _rb.linearVelocity = velocity;
            }

            _wasThrown = velocity.sqrMagnitude > 1f;
            _throwDamageApplied = false;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!IsServer) return;
            if (!_wasThrown || _throwDamageApplied) return;
            if (!enableThrowDamage) return;
            if (itemConfig == null || itemConfig.throwDamage <= 0) return;

            _throwDamageApplied = true;

            var no = collision.collider.GetComponentInParent<NetworkObject>();
            if (no == null) return;

            // Don't damage the thrower
            if (no.OwnerClientId == _holderClientId.Value) return;

            var health = collision.collider.GetComponentInParent<NetworkHealth>();
            if (health != null)
            {
                Vector3 hitPos = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
                var info = new DungeonGame.Combat.DamageInfo(itemConfig.throwDamage)
                {
                    AttackerClientId = _holderClientId.Value,
                    HitPosition = hitPos
                };
                health.TakeDamage(info);
            }
        }
    }
}
