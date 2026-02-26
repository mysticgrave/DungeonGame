using System;
using System.Collections.Generic;
using UnityEngine;

namespace DungeonGame.Player
{
    /// <summary>
    /// Switches between "standing" (root capsule only) and "ragdoll" (bone colliders only) so they don't fight.
    /// When standing: root CapsuleCollider enabled, all ragdoll bone colliders disabled and Rigidbodies kinematic.
    /// When ragdoll: root CapsuleCollider disabled, all ragdoll bone colliders enabled and Rigidbodies non-kinematic.
    /// Assign the Hips (or parent of all ragdoll bones) to ragdollRoot. Call SetStanding() on spawn; call SetRagdoll(impulse) when knocked.
    /// </summary>
    public class RagdollColliderSwitch : MonoBehaviour
    {
        [Header("Root (player body)")]
        [Tooltip("CapsuleCollider on this GameObject for ground collision when standing. Auto-finds if null.")]
        [SerializeField] private CapsuleCollider rootCapsule;

        [Header("Ragdoll")]
        [Tooltip("Parent of all ragdoll bones (e.g. Hips). We toggle Rigidbodies and Colliders on this and its descendants.")]
        [SerializeField] private Transform ragdollRoot;

        [Header("Optional")]
        [Tooltip("Disable when ragdolling so physics drives the skeleton. Auto-found on this object or any child if left empty.")]
        [SerializeField] private Animator animator;
        [Tooltip("Bone names containing any of these (e.g. Hand) are not restored after ragdoll; the Animator drives them to avoid hand snap. Leave empty to restore all bones.")]
        [SerializeField] private string[] skipRestoreBoneNames = { "Hand" };

        private readonly List<Rigidbody> _ragdollBodies = new();
        private readonly List<Collider> _ragdollColliders = new();
        private readonly List<Vector3> _standingPositions = new();
        private readonly List<Quaternion> _standingRotations = new();
        private Vector3 _capturedRootPosition;
        private Quaternion _capturedRootRotation;
        private Vector3 _rootToHipsOffset;
        private Rigidbody _hipsRb;
        private bool _initialized;
        private bool _isRagdoll;
        private bool _standingPoseCaptured;
        private int _deferExitRagdollFrames;

        public Transform RagdollRoot => ragdollRoot;

        /// <summary>
        /// The Hips Rigidbody. Use <c>HipsRigidbody.position</c> for the true physics world position
        /// during ragdoll (immune to parent-transform drift from NetworkTransform).
        /// </summary>
        public Rigidbody HipsRigidbody => _hipsRb;

        public bool IsRagdoll => _isRagdoll;

        public event Action OnRagdollEntered;
        public event Action OnRagdollExited;

        private void Awake()
        {
            if (rootCapsule == null) rootCapsule = GetComponent<CapsuleCollider>();
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            CacheRagdollParts();
            PreventBodyMeshCulling();
        }

        /// <summary>
        /// Stops SkinnedMeshRenderers (e.g. body) from being culled when bones jump during ragdoll recovery.
        /// The shield often stays visible because it uses a different renderer; the body can vanish for a frame otherwise.
        /// </summary>
        private void PreventBodyMeshCulling()
        {
            foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
                smr.updateWhenOffscreen = true;
        }

        private void Start()
        {
            if (!_isRagdoll)
                SetStanding();
        }

        private void CacheRagdollParts()
        {
            _ragdollBodies.Clear();
            _ragdollColliders.Clear();
            if (ragdollRoot == null) return;

            _ragdollBodies.AddRange(ragdollRoot.GetComponentsInChildren<Rigidbody>(true));
            _ragdollColliders.AddRange(ragdollRoot.GetComponentsInChildren<Collider>(true));

            // Find the true physics root: the Rigidbody WITHOUT a CharacterJoint (top of the ragdoll chain).
            // ragdollRoot itself may be an intermediate "Armature" transform with no Rigidbody.
            _hipsRb = null;
            foreach (var rb in _ragdollBodies)
            {
                if (rb != null && rb.GetComponent<CharacterJoint>() == null)
                {
                    _hipsRb = rb;
                    break;
                }
            }
            if (_hipsRb == null && _ragdollBodies.Count > 0)
                _hipsRb = _ragdollBodies[0];

            _initialized = true;
        }

        /// <summary>
        /// Use when standing/walking: bone colliders off and kinematic. When a ragdoll is present we leave the root capsule disabled so CharacterController is the only ground collider (avoids falling through after recovery).
        /// </summary>
        public void SetStanding()
        {
            if (ragdollRoot == null)
            {
                if (rootCapsule != null) rootCapsule.enabled = true;
                return;
            }

            if (!_initialized) CacheRagdollParts();

            // With a ragdoll, leave root capsule disabled when standing so CharacterController is the only ground collider (avoids fall-through after recovery).
            // Root capsule is only used when there is no ragdoll (see early return above).

            foreach (var rb in _ragdollBodies)
            {
                if (rb != null)
                    rb.isKinematic = true;
            }

            foreach (var c in _ragdollColliders)
            {
                if (c != null && c != rootCapsule)
                    c.enabled = false;
            }

            // Return skeleton to standing pose so the character doesn't stay crumpled.
            if (!_standingPoseCaptured)
                CaptureStandingPose();
            else
                RestoreStandingPose();

            if (animator != null)
            {
                animator.enabled = true;
                animator.Update(0f); // One update so skipped bones (e.g. hands) get driven by animation immediately.
            }

            _isRagdoll = false;
            // Defer exit event by one frame so SkinnedMeshRenderer bounds update and the body mesh doesn't vanish before the camera snaps back.
            _deferExitRagdollFrames = 2;
        }

        private void LateUpdate()
        {
            if (_deferExitRagdollFrames <= 0) return;
            _deferExitRagdollFrames--;
            if (_deferExitRagdollFrames == 0)
                OnRagdollExited?.Invoke();
        }

        private void CaptureStandingPose()
        {
            _standingPositions.Clear();
            _standingRotations.Clear();
            _capturedRootPosition = transform.position;
            _capturedRootRotation = transform.rotation;
            foreach (var rb in _ragdollBodies)
            {
                if (rb != null)
                {
                    _standingPositions.Add(rb.transform.position);
                    _standingRotations.Add(rb.transform.rotation);
                }
            }
            _standingPoseCaptured = _standingPositions.Count > 0;
        }

        private void RestoreStandingPose()
        {
            if (_standingPositions.Count != _ragdollBodies.Count) return;
            Vector3 posDelta = transform.position - _capturedRootPosition;
            Quaternion rotDelta = transform.rotation * Quaternion.Inverse(_capturedRootRotation);
            for (int i = 0; i < _ragdollBodies.Count; i++)
            {
                var rb = _ragdollBodies[i];
                if (rb == null || i >= _standingPositions.Count) continue;
                if (ShouldSkipRestore(rb.transform.name))
                    continue;
                rb.transform.SetPositionAndRotation(
                    _standingPositions[i] + posDelta,
                    rotDelta * _standingRotations[i]);
            }
        }

        private bool ShouldSkipRestore(string boneName)
        {
            if (skipRestoreBoneNames == null || skipRestoreBoneNames.Length == 0) return false;
            for (int i = 0; i < skipRestoreBoneNames.Length; i++)
            {
                if (!string.IsNullOrEmpty(skipRestoreBoneNames[i]) &&
                    boneName.IndexOf(skipRestoreBoneNames[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Use when knocked/ragdoll: root capsule off, bone colliders on and non-kinematic. Applies impulse to ragdollRoot's Rigidbody.
        /// </summary>
        public void SetRagdoll(Vector3 impulse)
        {
            if (ragdollRoot == null)
            {
                if (rootCapsule != null) rootCapsule.enabled = false;
                _isRagdoll = true;
                OnRagdollEntered?.Invoke();
                return;
            }

            if (!_initialized) CacheRagdollParts();

            if (rootCapsule != null)
                rootCapsule.enabled = false;

            foreach (var rb in _ragdollBodies)
            {
                if (rb != null)
                    rb.isKinematic = false;
            }

            foreach (var c in _ragdollColliders)
            {
                if (c != null && c != rootCapsule)
                    c.enabled = true;
            }

            // Capture offset from the actual physics root bone (not the ragdollRoot container, which may lack a Rigidbody).
            _rootToHipsOffset = _hipsRb != null
                ? (_hipsRb.position - transform.position)
                : (ragdollRoot.position - transform.position);

            if (animator != null)
                animator.enabled = false;

            if (_hipsRb != null && impulse.sqrMagnitude > 0.001f)
                _hipsRb.AddForce(impulse, ForceMode.Impulse);

            _isRagdoll = true;
            OnRagdollEntered?.Invoke();
        }

        /// <summary>
        /// Current world position of the Hips during ragdoll, using <c>Rigidbody.position</c> for accuracy.
        /// Falls back to <c>ragdollRoot.position</c> if no Rigidbody, or <c>transform.position</c> if ragdollRoot is null.
        /// </summary>
        public Vector3 GetRagdollWorldPosition()
        {
            if (ragdollRoot == null) return transform.position;
            return _hipsRb != null ? _hipsRb.position : ragdollRoot.position;
        }

        /// <summary>
        /// Snap the root transform to the ragdoll's landing position.
        /// Uses <c>Rigidbody.position</c> (true physics position) instead of <c>Transform.position</c>
        /// to avoid stale values caused by parent-transform drift from NetworkTransform.
        /// </summary>
        public void SnapRootToRagdoll()
        {
            if (ragdollRoot == null) return;
            Vector3 hipsPos = _hipsRb != null ? _hipsRb.position : ragdollRoot.position;
            transform.position = hipsPos - _rootToHipsOffset;
        }
    }
}
