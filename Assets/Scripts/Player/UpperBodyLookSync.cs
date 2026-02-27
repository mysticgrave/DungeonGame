using Unity.Netcode;
using UnityEngine;

namespace DungeonGame.Player
{
    /// <summary>
    /// Syncs camera pitch to the Spine (or Chest) bone so other players see the upper body tilting when you look up/down.
    /// Owner reads pitch from FirstPersonCameraRig; all clients apply it to the spine bone.
    /// Skip when ragdolling/knocked (spine is driven by physics).
    /// Adds pitch ON TOP of the Animator's pose to avoid twisting the base skeleton.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class UpperBodyLookSync : NetworkBehaviour
    {
        public enum PitchAxis
        {
            [Tooltip("Rotate around local X (through shoulders). Try first for most humanoid rigs.")]
            LocalX,
            [Tooltip("Rotate around local Y (up spine). For rigs where X causes twist.")]
            LocalY,
            [Tooltip("Rotate around local Z (forward). For rigs with non-standard orientation.")]
            LocalZ,
        }

        [Tooltip("Spine or Chest bone to rotate by pitch. Assign from your humanoid rig (e.g. Armature/Hips/Spine/Chest).")]
        [SerializeField] private Transform spineBone;
        [Tooltip("Multiplier for pitch application (e.g. 0.5 for subtle tilt).")]
        [SerializeField] private float pitchScale = 1f;
        [Tooltip("If the upper body tilts the wrong way when you look up/down, enable this.")]
        [SerializeField] private bool invertPitch;
        [Tooltip("Which local axis to rotate around. If the body twists, try a different axis (Y or Z).")]
        [SerializeField] private PitchAxis pitchAxis = PitchAxis.LocalX;

        private readonly NetworkVariable<float> _pitchNet = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        private FirstPersonCameraRig _cameraRig;
        private RagdollColliderSwitch _ragdollSwitch;
        private KnockableCapsule _knock;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _cameraRig = GetComponent<FirstPersonCameraRig>();
            _ragdollSwitch = GetComponent<RagdollColliderSwitch>();
            _knock = GetComponent<KnockableCapsule>();
        }

        private void LateUpdate()
        {
            if (spineBone == null) return;

            bool isRagdolling = (_ragdollSwitch != null && _ragdollSwitch.IsRagdoll) ||
                                (_knock != null && _knock.IsKnocked());

            if (isRagdolling) return;

            if (IsOwner && _cameraRig != null)
                _pitchNet.Value = _cameraRig.Pitch * pitchScale;

            float p = _pitchNet.Value;
            float sign = invertPitch ? 1f : -1f;
            float degrees = sign * p;

            Quaternion pitchRotation = pitchAxis switch
            {
                PitchAxis.LocalX => Quaternion.Euler(degrees, 0f, 0f),
                PitchAxis.LocalY => Quaternion.Euler(0f, degrees, 0f),
                PitchAxis.LocalZ => Quaternion.Euler(0f, 0f, degrees),
                _ => Quaternion.Euler(degrees, 0f, 0f),
            };

            // Add pitch on TOP of the Animator's pose so we don't overwrite and twist the skeleton.
            spineBone.localRotation = spineBone.localRotation * pitchRotation;
        }
    }
}
