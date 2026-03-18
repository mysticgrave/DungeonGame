using DungeonGame.Classes;
using DungeonGame.Combat;
using DungeonGame.Player;
using DungeonGame.UI;
using DungeonGame.Weapons;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace DungeonGame.Abilities
{
    /// <summary>
    /// Handles ability input (Q/E/R), cooldown tracking, and server execution.
    /// Supports Instant cast and HoldTarget cast (ground reticle).
    /// Add to the player prefab alongside PlayerClass, PlayerClassStats.
    /// </summary>
    public class PlayerAbilityController : NetworkBehaviour
    {
        private PlayerClass _playerClass;
        private PlayerClassStats _classStats;
        private PlayerBodyStateMachine _bodyState;
        private Animator _animator;
        private bool _hasAnimator;

        // Client-side cooldowns (for input responsiveness)
        private float _nextUseTimeQ;
        private float _nextUseTimeE;
        private float _nextUseTimeR;

        // Server-side cooldowns (authoritative)
        private float _serverNextQ;
        private float _serverNextE;
        private float _serverNextR;

        // Hold-to-target state
        private int _targetingSlot = -1; // which slot is currently being targeted (-1 = none)
        private GameObject _reticleObj;
        private float _reticleRadius;
        private bool _selfCenteredTargeting; // MeleeAoE = reticle on player; AreaZone = reticle follows mouse

        // ──────────────────── Public API (for UI) ────────────────────

        public float CooldownRemainingQ => Mathf.Max(0f, _nextUseTimeQ - Time.time);
        public float CooldownRemainingE => Mathf.Max(0f, _nextUseTimeE - Time.time);
        public float CooldownRemainingR => Mathf.Max(0f, _nextUseTimeR - Time.time);

        public AbilityConfig AbilityQ => GetAbilityForSlot(0);
        public AbilityConfig AbilityE => GetAbilityForSlot(1);
        public AbilityConfig AbilityR => GetAbilityForSlot(2);

        public float GetCooldownTotal(int slot) => GetAbilityForSlot(slot)?.cooldown ?? 0f;

        /// <summary>True when the player is holding a targeting reticle.</summary>
        public bool IsTargeting => _targetingSlot >= 0;

        // ──────────────────── Lifecycle ────────────────────

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            _playerClass = GetComponent<PlayerClass>();
            _classStats = GetComponent<PlayerClassStats>();
            _bodyState = GetComponent<PlayerBodyStateMachine>();
            _animator = GetComponentInChildren<Animator>(true);
            _hasAnimator = _animator != null;
        }

        public override void OnNetworkDespawn()
        {
            CancelTargeting();
            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (!IsOwner) return;
            if (Keyboard.current == null) return;
            if (PauseMenuController.IsPaused) return;
            if (_bodyState != null && _bodyState.IsMovementDisabled) return;

            // If currently targeting, update reticle and check for release
            if (_targetingSlot >= 0)
            {
                UpdateTargetingReticle();

                // Get the key for the active targeting slot
                var key = GetKeyForSlot(_targetingSlot);
                if (key != null && key.wasReleasedThisFrame)
                {
                    // Save slot before canceling (CancelTargeting sets it to -1)
                    int slot = _targetingSlot;
                    bool selfCentered = _selfCenteredTargeting;
                    CancelTargeting();
                    SetClientCooldown(slot);

                    if (selfCentered)
                    {
                        // MeleeAoE: cast at player position
                        UseAbilityServerRpc(slot, transform.position);
                    }
                    else
                    {
                        // AreaZone: cast at mouse target position
                        Vector3 targetPos = GetGroundTargetPosition();
                        UseAbilityAtPositionServerRpc(slot, targetPos);
                    }
                }

                // Right-click or Escape to cancel
                if ((Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame) ||
                    Keyboard.current.escapeKey.wasPressedThisFrame)
                {
                    CancelTargeting();
                }
                return; // Don't process other inputs while targeting
            }

            if (Keyboard.current.qKey.wasPressedThisFrame) TryUseAbility(0);
            if (Keyboard.current.eKey.wasPressedThisFrame) TryUseAbility(1);
            if (Keyboard.current.rKey.wasPressedThisFrame) TryUseAbility(2);
        }

        // ──────────────────── Input → RPC ────────────────────

        private AbilityConfig GetAbilityForSlot(int slot)
        {
            var def = _playerClass != null ? _playerClass.Definition : null;
            if (def == null) return null;

            return slot switch
            {
                0 => def.abilityQ,
                1 => def.abilityE,
                2 => def.abilityR,
                _ => null
            };
        }

        private KeyControl GetKeyForSlot(int slot)
        {
            if (Keyboard.current == null) return null;
            return slot switch
            {
                0 => Keyboard.current.qKey,
                1 => Keyboard.current.eKey,
                2 => Keyboard.current.rKey,
                _ => null
            };
        }

        private void TryUseAbility(int slot)
        {
            var config = GetAbilityForSlot(slot);
            if (config == null) return;

            // Client-side cooldown check
            if (IsOnCooldown(slot))
            {
                float remaining = GetCooldownRemaining(slot);
                Debug.Log($"[Ability] {config.displayName} on cooldown ({remaining:F1}s remaining)");
                return;
            }

            // HoldTarget: begin targeting mode
            if (config.castMode == CastMode.HoldTarget)
            {
                BeginTargeting(slot, config);
                return;
            }

            // Instant cast
            SetClientCooldown(slot);

            // Play animation locally for responsiveness
            if (_hasAnimator && !string.IsNullOrEmpty(config.animationTrigger))
            {
                int hash = Animator.StringToHash(config.animationTrigger);
                if (WeaponController.HasTriggerParameter(_animator, hash))
                    _animator.SetTrigger(hash);
            }

            // Send client position so server uses the correct (owner-authoritative) position
            UseAbilityServerRpc(slot, transform.position);
        }

        private bool IsOnCooldown(int slot)
        {
            float nextTime = slot switch
            {
                0 => _nextUseTimeQ,
                1 => _nextUseTimeE,
                2 => _nextUseTimeR,
                _ => 0f
            };
            return Time.time < nextTime;
        }

        private float GetCooldownRemaining(int slot)
        {
            float nextTime = slot switch
            {
                0 => _nextUseTimeQ,
                1 => _nextUseTimeE,
                2 => _nextUseTimeR,
                _ => 0f
            };
            return Mathf.Max(0f, nextTime - Time.time);
        }

        private void SetClientCooldown(int slot)
        {
            var config = GetAbilityForSlot(slot);
            if (config == null) return;
            float cd = config.cooldown;
            switch (slot)
            {
                case 0: _nextUseTimeQ = Time.time + cd; break;
                case 1: _nextUseTimeE = Time.time + cd; break;
                case 2: _nextUseTimeR = Time.time + cd; break;
            }
        }

        // ──────────────────── Hold-to-Target ────────────────────

        private void BeginTargeting(int slot, AbilityConfig config)
        {
            _targetingSlot = slot;
            _reticleRadius = config.radius;
            _selfCenteredTargeting = TargetingReticleFactory.IsSelfCentered(config);

            DestroyReticle();
            _reticleObj = TargetingReticleFactory.Create(config);
        }

        private void CancelTargeting()
        {
            _targetingSlot = -1;
            _selfCenteredTargeting = false;
            DestroyReticle();
        }

        private void DestroyReticle()
        {
            if (_reticleObj != null)
            {
                Object.Destroy(_reticleObj);
                _reticleObj = null;
            }
        }

        private void UpdateTargetingReticle()
        {
            if (_reticleObj == null) return;

            if (_selfCenteredTargeting)
            {
                // MeleeAoE: reticle stays on the player
                _reticleObj.transform.position = transform.position + Vector3.up * 0.05f;

                // For Arc shape: rotate to face mouse direction
                var config = GetAbilityForSlot(_targetingSlot);
                if (config != null && TargetingReticleFactory.ResolveShape(config) == ReticleShape.Arc)
                {
                    Vector3 groundTarget = GetGroundTargetPosition();
                    Vector3 dir = groundTarget - transform.position;
                    dir.y = 0f;
                    if (dir.sqrMagnitude > 0.01f)
                        _reticleObj.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
                }
            }
            else
            {
                // AreaZone: reticle follows mouse on ground, clamped to range
                Vector3 groundTarget = GetGroundTargetPosition();
                var config = GetAbilityForSlot(_targetingSlot);
                if (config != null)
                {
                    Vector3 offset = groundTarget - transform.position;
                    if (offset.magnitude > config.range)
                        groundTarget = transform.position + offset.normalized * config.range;
                }
                _reticleObj.transform.position = groundTarget + Vector3.up * 0.05f;
            }
        }

        private Vector3 GetGroundTargetPosition()
        {
            // Raycast from camera through mouse to find ground position
            var cam = Camera.main;
            if (cam == null || Mouse.current == null)
                return transform.position + transform.forward * 5f;

            var mousePos = Mouse.current.position.ReadValue();
            var ray = cam.ScreenPointToRay(mousePos);

            // Try hitting the ground
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, ~0, QueryTriggerInteraction.Ignore))
                return hit.point;

            // Fallback: project onto a plane at caster height
            var plane = new Plane(Vector3.up, transform.position);
            if (plane.Raycast(ray, out float dist))
                return ray.GetPoint(dist);

            return transform.position + transform.forward * 5f;
        }

        // ──────────────────── Server Execution ────────────────────

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void UseAbilityServerRpc(int slot, Vector3 clientPosition)
        {
            var config = GetAbilityForSlot(slot);
            if (config == null)
            {
                Debug.Log($"[Ability] ServerRpc rejected — no ability in slot {slot}");
                return;
            }

            if (!CheckAndSetServerCooldown(slot, config)) return;

            // Use client-reported position for owner-authoritative movement accuracy
            // Validate it's not absurdly far from server position (anti-cheat sanity check)
            Vector3 usePosition = clientPosition;
            float drift = Vector3.Distance(transform.position, clientPosition);
            if (drift > 15f)
            {
                Debug.LogWarning($"[Ability] Client position drift too large ({drift:F1}m), using server position");
                usePosition = transform.position;
            }

            // Temporarily move to client position for execution, then restore
            Vector3 serverPos = transform.position;
            transform.position = usePosition;

            Debug.Log($"[Ability] Executing '{config.displayName}' (slot {slot}) for client {OwnerClientId}");
            AbilityExecutor.Execute(config, transform, NetworkObject, _classStats);

            // DashStrike: tell the owner to enter dodge (owner-authoritative movement)
            if (config.abilityType == AbilityType.DashStrike)
            {
                float dashSpeed = config.dashDistance / config.dashDuration;
                TriggerDashClientRpc(transform.forward, config.dashDuration, dashSpeed);
            }

            transform.position = serverPos;

            SyncFeedback(config, slot);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void UseAbilityAtPositionServerRpc(int slot, Vector3 targetPosition)
        {
            var config = GetAbilityForSlot(slot);
            if (config == null)
            {
                Debug.Log($"[Ability] ServerRpc rejected — no ability in slot {slot}");
                return;
            }

            if (!CheckAndSetServerCooldown(slot, config)) return;

            // Clamp target distance to range
            Vector3 offset = targetPosition - transform.position;
            if (offset.magnitude > config.range)
                targetPosition = transform.position + offset.normalized * config.range;

            Debug.Log($"[Ability] Executing targeted '{config.displayName}' (slot {slot}) at {targetPosition} for client {OwnerClientId}");
            AbilityExecutor.ExecuteAtPosition(config, transform, NetworkObject, _classStats, targetPosition);

            SyncFeedbackAtPosition(config, slot, targetPosition);
        }

        private bool CheckAndSetServerCooldown(int slot, AbilityConfig config)
        {
            float serverNext = slot switch
            {
                0 => _serverNextQ,
                1 => _serverNextE,
                2 => _serverNextR,
                _ => 0f
            };

            if (Time.time < serverNext)
            {
                float remaining = serverNext - Time.time;
                Debug.Log($"[Ability] ServerRpc rejected — {config.displayName} server cooldown ({remaining:F1}s)");
                return false;
            }

            switch (slot)
            {
                case 0: _serverNextQ = Time.time + config.cooldown; break;
                case 1: _serverNextE = Time.time + config.cooldown; break;
                case 2: _serverNextR = Time.time + config.cooldown; break;
            }
            return true;
        }

        private void SyncFeedback(AbilityConfig config, int slot)
        {
            if (!string.IsNullOrEmpty(config.animationTrigger))
                PlayAbilityAnimClientRpc(Animator.StringToHash(config.animationTrigger));
            SpawnAbilityVfxClientRpc(slot, transform.position);
        }

        private void SyncFeedbackAtPosition(AbilityConfig config, int slot, Vector3 targetPosition)
        {
            if (!string.IsNullOrEmpty(config.animationTrigger))
                PlayAbilityAnimClientRpc(Animator.StringToHash(config.animationTrigger));
            SpawnAbilityVfxAtPositionClientRpc(slot, targetPosition);
        }

        // ──────────────────── Client Feedback RPCs ────────────────────

        [Rpc(SendTo.NotOwner)]
        private void PlayAbilityAnimClientRpc(int triggerHash)
        {
            if (_hasAnimator)
                _animator.SetTrigger(triggerHash);
        }

        [Rpc(SendTo.Everyone)]
        private void SpawnAbilityVfxClientRpc(int slot, Vector3 position)
        {
            var config = GetAbilityForSlot(slot);
            if (config == null) return;

            AbilityVfxFactory.SpawnVfx(config, position, transform.forward);

            if (config.castSound != null)
                AudioSource.PlayClipAtPoint(config.castSound, position);
        }

        [Rpc(SendTo.Owner)]
        private void TriggerDashClientRpc(Vector3 direction, float duration, float speed)
        {
            if (_bodyState != null)
                _bodyState.EnterDodge(direction, duration, speed);
        }

        [Rpc(SendTo.Everyone)]
        private void SpawnAbilityVfxAtPositionClientRpc(int slot, Vector3 targetPosition)
        {
            var config = GetAbilityForSlot(slot);
            if (config == null) return;

            AbilityVfxFactory.SpawnVfxAtPosition(config, targetPosition);

            if (config.castSound != null)
                AudioSource.PlayClipAtPoint(config.castSound, targetPosition);
        }
    }
}
