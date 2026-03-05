using Unity.Netcode;
using UnityEngine;

namespace DungeonGame.Player
{
    /// <summary>
    /// Receives attack-step callbacks from Animation Events. Applies a scripted forward lunge
    /// when the animation would show a step, so movement matches the anim without root motion.
    /// Add to the same GameObject as the Animator (or it will be added automatically by SwordCombatLayerBlender).
    /// In your attack clip: add Animation Event at the step moment → Function: ApplyAttackStep
    /// </summary>
    public class AttackStepReceiver : MonoBehaviour
    {
        [Tooltip("Forward distance to move when the step event fires.")]
        [SerializeField] private float lungeDistance = 0.4f;

        private CharacterController _cc;
        private NetworkObject _no;

        private void Awake()
        {
            _cc = GetComponentInParent<CharacterController>();
            _no = GetComponentInParent<NetworkObject>();
        }

        /// <summary>Call from Animation Event at the moment the attack anim shows the step/lunge.</summary>
        public void ApplyAttackStep()
        {
            if (_cc == null || !_cc.enabled) return;
            if (_no != null && _no.IsSpawned && !_no.IsOwner) return;

            Vector3 fwd = _cc.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            fwd.Normalize();

            _cc.Move(fwd * lungeDistance);
        }
    }
}
