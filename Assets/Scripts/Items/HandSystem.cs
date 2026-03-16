using DungeonGame.Meta;
using DungeonGame.Player;
using DungeonGame.UI;
using DungeonGame.Weapons;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DungeonGame.Items
{
    /// <summary>
    /// Server-authoritative two-hand item system. Players hold items in left/right hands.
    /// Items can be one-handed (one slot) or two-handed (both slots).
    /// Input: LMB = left hand, RMB = right hand, Hold G + click = drop, F = interact.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class HandSystem : NetworkBehaviour
    {
        [Header("Hand Bones")]
        [Tooltip("Left hand bone on the 3P rig.")]
        [SerializeField] private Transform leftHandBone;
        [Tooltip("Right hand bone on the 3P rig.")]
        [SerializeField] private Transform rightHandBone;

        [Header("Pickup")]
        [SerializeField] private float pickupRange = 3f;
        [SerializeField] private LayerMask pickupLayers = -1;

        [Header("Drop / Throw")]
        [SerializeField] private float dropForwardOffset = 1f;
        [SerializeField] private float dropUpOffset = 0.5f;

        [Header("Debug")]
        [SerializeField] private bool debugLog;

        // --- Networked state (server-authoritative) ---
        // Item identity as ItemRegistry index. -1 = empty.
        private readonly NetworkVariable<int> _leftItemIndex = new(-1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> _rightItemIndex = new(-1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        // NetworkObjectId of the WorldItem that was picked up. 0 = spawned-in (Meta weapon).
        private readonly NetworkVariable<ulong> _leftWorldItemNetId = new(0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<ulong> _rightWorldItemNetId = new(0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        // --- Local state ---
        private Transform _leftVisualInstance;
        private Transform _rightVisualInstance;
        private LocalPlayerCameraRig _cameraRig;
        private WeaponController _weaponController;
        private SwordCombatLayerBlender _layerBlender;
        private Animator _animator;
        private PlayerBodyStateMachine _bodyState;
        private float _nextAttackTimeLeft;
        private float _nextAttackTimeRight;
        private bool _metaWeaponRequested;

        // --- Computed properties ---
        public bool LeftHandEmpty => _leftItemIndex.Value < 0;
        public bool RightHandEmpty => _rightItemIndex.Value < 0;

        public bool HasTwoHandedItem
        {
            get
            {
                if (_leftItemIndex.Value < 0) return false;
                var config = ItemRegistry.GetByIndex(_leftItemIndex.Value);
                return config != null && config.grip == ItemGrip.TwoHanded;
            }
        }

        public WeaponConfig LeftItem =>
            _leftItemIndex.Value >= 0 ? ItemRegistry.GetByIndex(_leftItemIndex.Value) : null;

        public WeaponConfig RightItem =>
            _rightItemIndex.Value >= 0 ? ItemRegistry.GetByIndex(_rightItemIndex.Value) : null;

        /// <summary>Fired on all clients when either hand changes. Args: this HandSystem.</summary>
        public event System.Action<HandSystem> OnHandsChanged;

        // ────────────────────── Lifecycle ──────────────────────

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            _cameraRig = GetComponent<LocalPlayerCameraRig>();
            _weaponController = GetComponent<WeaponController>();
            _layerBlender = GetComponent<SwordCombatLayerBlender>();
            _animator = GetComponentInChildren<Animator>(true);
            _bodyState = GetComponent<PlayerBodyStateMachine>();

            _leftItemIndex.OnValueChanged += OnLeftItemChanged;
            _rightItemIndex.OnValueChanged += OnRightItemChanged;

            // Apply current state for late joiners
            UpdateVisual(Hand.Left, _leftItemIndex.Value);
            UpdateVisual(Hand.Right, _rightItemIndex.Value);
            UpdateTwoHandedMode();

            if (ItemRegistry.Instance == null)
                Debug.LogWarning("[HandSystem] ItemRegistry not found! Add an ItemRegistry component to a scene GameObject and populate it.", this);

            // Owner requests their MetaProgression starting weapon
            if (IsOwner && !_metaWeaponRequested)
            {
                _metaWeaponRequested = true;
                RequestMetaWeapon();
            }
        }

        public override void OnNetworkDespawn()
        {
            _leftItemIndex.OnValueChanged -= OnLeftItemChanged;
            _rightItemIndex.OnValueChanged -= OnRightItemChanged;

            // Server: drop all held items when player disconnects
            if (IsServer)
            {
                DropInternal(Hand.Left, Vector3.forward, false);
                DropInternal(Hand.Right, Vector3.forward, false);
            }

            DestroyVisual(Hand.Left);
            DestroyVisual(Hand.Right);

            base.OnNetworkDespawn();
        }

        // ────────────────────── Input (Owner only) ──────────────────────

        private void Update()
        {
            if (!IsOwner) return;
            if (Keyboard.current == null || Mouse.current == null) return;
            if (PauseMenuController.IsPaused) return;
            if (_bodyState != null && _bodyState.IsMovementDisabled) return;

            bool gHeld = Keyboard.current.gKey.isPressed;
            bool lmbPressed = Mouse.current.leftButton.wasPressedThisFrame;
            bool rmbPressed = Mouse.current.rightButton.wasPressedThisFrame;

            // --- DROP/THROW MODE (Hold G) ---
            if (gHeld)
            {
                if (HasTwoHandedItem)
                {
                    // Any click drops the two-handed item
                    if (lmbPressed || rmbPressed)
                    {
                        var camFwd = GetCameraForward();
                        DropServerRpc((int)Hand.Left, camFwd, true);
                    }
                }
                else
                {
                    if (lmbPressed && !LeftHandEmpty)
                    {
                        var camFwd = GetCameraForward();
                        DropServerRpc((int)Hand.Left, camFwd, true);
                    }
                    if (rmbPressed && !RightHandEmpty)
                    {
                        var camFwd = GetCameraForward();
                        DropServerRpc((int)Hand.Right, camFwd, true);
                    }
                }
                return; // Don't process use/pickup while G held
            }

            // --- TWO-HANDED ITEM USE ---
            if (HasTwoHandedItem)
            {
                if (lmbPressed)
                    TryUseItem(Hand.Left, false);
                else if (rmbPressed)
                    TryUseItem(Hand.Left, true); // secondary action
                return;
            }

            // --- LEFT HAND (LMB) ---
            if (lmbPressed)
            {
                if (LeftHandEmpty)
                    TryPickup(Hand.Left);
                else
                    TryUseItem(Hand.Left, false);
            }

            // --- RIGHT HAND (RMB) ---
            if (rmbPressed)
            {
                if (RightHandEmpty)
                    TryPickup(Hand.Right);
                else
                    TryUseItem(Hand.Right, false);
            }
        }

        // ────────────────────── Client-side helpers ──────────────────────

        private void TryPickup(Hand hand)
        {
            var camT = _cameraRig != null ? _cameraRig.CameraTransform : null;
            if (camT == null)
            {
                if (debugLog) Debug.Log("[HandSystem] TryPickup: no camera transform", this);
                return;
            }

            var ray = new Ray(camT.position, camT.forward);
            if (!Physics.Raycast(ray, out var hit, pickupRange, pickupLayers, QueryTriggerInteraction.Ignore))
            {
                if (debugLog) Debug.Log($"[HandSystem] TryPickup: raycast missed (range={pickupRange}, layers={pickupLayers.value})", this);
                return;
            }

            var worldItem = hit.collider.GetComponentInParent<WorldItem>();
            if (worldItem == null)
            {
                if (debugLog) Debug.Log($"[HandSystem] TryPickup: hit '{hit.collider.name}' but no WorldItem on it or parents", this);
                return;
            }
            if (worldItem.IsHeld)
            {
                if (debugLog) Debug.Log($"[HandSystem] TryPickup: '{worldItem.name}' already held", this);
                return;
            }

            if (debugLog) Debug.Log($"[HandSystem] TryPickup: requesting '{worldItem.name}' into {hand} hand", this);
            PickupServerRpc((int)hand, worldItem.NetworkObject.NetworkObjectId);
        }

        private void TryUseItem(Hand hand, bool secondary)
        {
            int itemIndex = hand == Hand.Left ? _leftItemIndex.Value : _rightItemIndex.Value;
            if (itemIndex < 0) return;

            // Client-side cooldown check
            float nextTime = hand == Hand.Left ? _nextAttackTimeLeft : _nextAttackTimeRight;
            if (Time.time < nextTime) return;

            var config = ItemRegistry.GetByIndex(itemIndex);
            if (config == null || config.damage <= 0) return; // non-weapon items don't attack

            // Set client-side cooldown
            if (hand == Hand.Left)
                _nextAttackTimeLeft = Time.time + config.cooldown;
            else
                _nextAttackTimeRight = Time.time + config.cooldown;

            // Play animation locally for responsiveness
            PlayAttackAnimation(config, secondary);

            UseItemServerRpc((int)hand, itemIndex, secondary);
        }

        private void PlayAttackAnimation(WeaponConfig config, bool secondary)
        {
            if (_animator == null || _animator.runtimeAnimatorController == null) return;

            string triggerName = secondary && !string.IsNullOrEmpty(config.secondaryActionTrigger)
                ? config.secondaryActionTrigger
                : config.primaryAttackTrigger;

            if (string.IsNullOrEmpty(triggerName)) return;

            int hash = Animator.StringToHash(triggerName);
            if (WeaponController.HasTriggerParameter(_animator, hash))
                _animator.SetTrigger(hash);
        }

        private Vector3 GetCameraForward()
        {
            var camT = _cameraRig != null ? _cameraRig.CameraTransform : null;
            if (camT != null) return camT.forward;
            return transform.forward;
        }

        private void RequestMetaWeapon()
        {
            var meta = MetaProgression.Instance;
            if (meta == null) return;

            string equippedId = meta.GetEquippedWeaponId();
            if (string.IsNullOrEmpty(equippedId)) return;
            if (!meta.IsWeaponUnlocked(equippedId)) return;

            RequestSpawnWeaponServerRpc(equippedId);
        }

        // ────────────────────── Server RPCs ──────────────────────

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void PickupServerRpc(int hand, ulong worldItemNetId)
        {
            if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(worldItemNetId, out var itemNetObj))
            {
                if (debugLog) Debug.Log($"[HandSystem] PickupRpc: netId {worldItemNetId} not found", this);
                return;
            }

            var worldItem = itemNetObj.GetComponent<WorldItem>();
            if (worldItem == null || worldItem.IsHeld)
            {
                if (debugLog) Debug.Log($"[HandSystem] PickupRpc: WorldItem null or already held", this);
                return;
            }

            var config = worldItem.ItemConfig;
            if (config == null)
            {
                if (debugLog) Debug.Log($"[HandSystem] PickupRpc: '{worldItem.name}' has no ItemConfig", this);
                return;
            }

            int registryIndex = ItemRegistry.IndexOf(config);
            if (registryIndex < 0)
            {
                Debug.LogWarning($"[HandSystem] PickupRpc: '{config.displayName}' NOT in ItemRegistry! Add it.", this);
                return;
            }

            var handSlot = (Hand)hand;

            if (config.grip == ItemGrip.TwoHanded)
            {
                // Two-handed: drop whatever is in both hands first
                DropInternal(Hand.Left, transform.forward, false);
                DropInternal(Hand.Right, transform.forward, false);

                // Set both hands to this item
                _leftItemIndex.Value = registryIndex;
                _rightItemIndex.Value = registryIndex;
                _leftWorldItemNetId.Value = worldItemNetId;
                _rightWorldItemNetId.Value = 0; // tracked via left
            }
            else
            {
                // One-handed: requested hand must be empty
                if (handSlot == Hand.Left && !LeftHandEmpty) return;
                if (handSlot == Hand.Right && !RightHandEmpty) return;

                // Can't pick up if holding a two-handed item
                if (HasTwoHandedItem) return;

                if (handSlot == Hand.Left)
                {
                    _leftItemIndex.Value = registryIndex;
                    _leftWorldItemNetId.Value = worldItemNetId;
                }
                else
                {
                    _rightItemIndex.Value = registryIndex;
                    _rightWorldItemNetId.Value = worldItemNetId;
                }
            }

            worldItem.ServerPickup(OwnerClientId);
            if (debugLog) Debug.Log($"[HandSystem] PickupRpc: SUCCESS '{config.displayName}' -> {(Hand)hand} (idx={registryIndex})", this);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void DropServerRpc(int hand, Vector3 cameraForward, bool isThrow)
        {
            DropInternal((Hand)hand, cameraForward, isThrow);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void UseItemServerRpc(int hand, int expectedItemIndex, bool secondary)
        {
            var handSlot = (Hand)hand;

            // Validate item matches server state
            int serverIndex = handSlot == Hand.Left ? _leftItemIndex.Value : _rightItemIndex.Value;
            if (serverIndex != expectedItemIndex || serverIndex < 0) return;

            var config = ItemRegistry.GetByIndex(serverIndex);
            if (config == null || config.damage <= 0) return; // non-weapon items don't attack

            // Server-side cooldown
            float nextTime = handSlot == Hand.Left ? _nextAttackTimeLeft : _nextAttackTimeRight;
            if (Time.time < nextTime) return;

            if (handSlot == Hand.Left)
                _nextAttackTimeLeft = Time.time + config.cooldown;
            else
                _nextAttackTimeRight = Time.time + config.cooldown;

            // Execute attack
            Transform origin = _weaponController != null ? _weaponController.AttackOrigin : transform;
            var knock = _weaponController != null
                ? _weaponController.GetKnockSettings()
                : new KnockSettings
                {
                    TeammateKnockForward = 6f,
                    TeammateKnockUp = 2f,
                    TeammateKnockDuration = 3f,
                    EnemyKnockForward = 8f,
                    EnemyKnockUp = 3f,
                };

            if (!secondary)
            {
                ItemAttackExecutor.ExecuteAttack(origin, config, NetworkObject, knock);
            }
            else if (!string.IsNullOrEmpty(config.secondaryActionTrigger))
            {
                // Secondary action: for now, same as primary. Extend per-item later.
                ItemAttackExecutor.ExecuteAttack(origin, config, NetworkObject, knock);
            }

            // Trigger animation on remote clients
            string triggerName = secondary && !string.IsNullOrEmpty(config.secondaryActionTrigger)
                ? config.secondaryActionTrigger
                : config.primaryAttackTrigger;
            if (!string.IsNullOrEmpty(triggerName))
                PlayAnimClientRpc(Animator.StringToHash(triggerName));
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void RequestSpawnWeaponServerRpc(string weaponId)
        {
            // Try ItemRegistry first, fall back to WeaponRegistry for meta weapons
            var config = ItemRegistry.Get(weaponId);
            if (config == null)
                config = WeaponRegistry.Get(weaponId);
            if (config == null) return;

            int registryIndex = ItemRegistry.IndexOf(config);
            if (registryIndex < 0)
            {
                Debug.LogWarning($"[HandSystem] Meta weapon '{weaponId}' not in ItemRegistry. Add it there too.", this);
                return;
            }

            if (config.grip == ItemGrip.TwoHanded)
            {
                _leftItemIndex.Value = registryIndex;
                _rightItemIndex.Value = registryIndex;
                _leftWorldItemNetId.Value = 0; // spawned-in, no world item
                _rightWorldItemNetId.Value = 0;
            }
            else
            {
                // Starting weapon goes in right hand
                _rightItemIndex.Value = registryIndex;
                _rightWorldItemNetId.Value = 0;
            }
        }

        // ────────────────────── Client RPCs ──────────────────────

        [Rpc(SendTo.NotOwner)]
        private void PlayAnimClientRpc(int triggerHash)
        {
            if (_animator == null) return;
            if (WeaponController.HasTriggerParameter(_animator, triggerHash))
                _animator.SetTrigger(triggerHash);
        }

        // ────────────────────── Server-side drop logic ──────────────────────

        private void DropInternal(Hand hand, Vector3 cameraForward, bool isThrow)
        {
            int itemIndex;
            ulong worldItemNetId;

            if (hand == Hand.Left)
            {
                itemIndex = _leftItemIndex.Value;
                worldItemNetId = _leftWorldItemNetId.Value;
            }
            else
            {
                itemIndex = _rightItemIndex.Value;
                worldItemNetId = _rightWorldItemNetId.Value;
            }

            if (itemIndex < 0) return;

            var config = ItemRegistry.GetByIndex(itemIndex);
            bool isTwoHanded = config != null && config.grip == ItemGrip.TwoHanded;

            // Calculate drop position
            Vector3 fwd = cameraForward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.01f) fwd = transform.forward;
            fwd.Normalize();
            Vector3 dropPos = transform.position + Vector3.up * dropUpOffset + fwd * dropForwardOffset;

            // Calculate throw velocity
            Vector3 velocity = Vector3.zero;
            if (isThrow && config != null && config.canBeThrown)
            {
                float rad = config.throwUpAngle * Mathf.Deg2Rad;
                Vector3 throwDir = (cameraForward + Vector3.up * Mathf.Tan(rad)).normalized;
                velocity = throwDir * config.throwForce;
            }

            // Re-enable existing WorldItem or spawn a new one
            if (worldItemNetId != 0 &&
                NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(worldItemNetId, out var existingNetObj))
            {
                var worldItem = existingNetObj.GetComponent<WorldItem>();
                if (worldItem != null)
                    worldItem.ServerDrop(dropPos, velocity);
            }
            else if (config != null && config.worldPrefab != null)
            {
                // Spawned-in weapon (from MetaProgression): create a new WorldItem
                var spawned = Instantiate(config.worldPrefab, dropPos, Quaternion.identity);
                var spawnedNetObj = spawned.GetComponent<NetworkObject>();
                if (spawnedNetObj != null)
                {
                    spawnedNetObj.Spawn(true);
                    var wi = spawned.GetComponent<WorldItem>();
                    if (wi != null && velocity.sqrMagnitude > 0.01f)
                    {
                        var rb = spawned.GetComponent<Rigidbody>();
                        if (rb != null)
                            rb.linearVelocity = velocity;
                    }
                }
            }

            // Clear hand state
            if (isTwoHanded)
            {
                _leftItemIndex.Value = -1;
                _rightItemIndex.Value = -1;
                _leftWorldItemNetId.Value = 0;
                _rightWorldItemNetId.Value = 0;
            }
            else
            {
                if (hand == Hand.Left)
                {
                    _leftItemIndex.Value = -1;
                    _leftWorldItemNetId.Value = 0;
                }
                else
                {
                    _rightItemIndex.Value = -1;
                    _rightWorldItemNetId.Value = 0;
                }
            }
        }

        // ────────────────────── Visual Sync (All Clients) ──────────────────────

        private void OnLeftItemChanged(int prev, int cur)
        {
            UpdateVisual(Hand.Left, cur);
            UpdateTwoHandedMode();
            OnHandsChanged?.Invoke(this);
        }

        private void OnRightItemChanged(int prev, int cur)
        {
            UpdateVisual(Hand.Right, cur);
            UpdateTwoHandedMode();
            OnHandsChanged?.Invoke(this);
        }

        private void UpdateVisual(Hand hand, int itemIndex)
        {
            DestroyVisual(hand);

            if (itemIndex < 0) return;

            var config = ItemRegistry.GetByIndex(itemIndex);
            if (config == null) return;

            // For two-handed items, only show visual on right hand bone
            if (config.grip == ItemGrip.TwoHanded && hand == Hand.Left) return;

            var prefab = config.heldVisualPrefab;
            if (prefab == null) return;

            Transform bone = hand == Hand.Left ? leftHandBone : rightHandBone;
            if (bone == null) return;

            var instance = Instantiate(prefab, bone).transform;
            instance.localPosition = config.heldPositionOffset;
            instance.localRotation = Quaternion.Euler(config.heldRotationOffset);
            instance.localScale = Vector3.one;

            if (hand == Hand.Left)
                _leftVisualInstance = instance;
            else
                _rightVisualInstance = instance;
        }

        private void DestroyVisual(Hand hand)
        {
            if (hand == Hand.Left && _leftVisualInstance != null)
            {
                Destroy(_leftVisualInstance.gameObject);
                _leftVisualInstance = null;
            }
            else if (hand == Hand.Right && _rightVisualInstance != null)
            {
                Destroy(_rightVisualInstance.gameObject);
                _rightVisualInstance = null;
            }
        }

        private void UpdateTwoHandedMode()
        {
            if (_layerBlender != null)
                _layerBlender.SetTwoHandedMode(HasTwoHandedItem);
        }
    }
}
