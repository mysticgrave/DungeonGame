using UnityEngine;

namespace DungeonGame.Player
{
    /// <summary>
    /// Blends SwordArm/SwordHand layer weight to 0 during full-body attack animations
    /// so the Base Layer's attack drives the arm. Exposes IsAttacking for movement lock.
    /// Add to the Player with an Animator.
    /// </summary>
    [DefaultExecutionOrder(-150)]
    public class SwordCombatLayerBlender : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [Tooltip("Weight for SwordArm when not attacking. Match controller default (~0.6).")]
        [SerializeField] private float swordArmWeightIdle = 0.6f;
        [Tooltip("Weight for SwordHand when not attacking.")]
        [SerializeField] private float swordHandWeightIdle = 1f;

        private int _swordArmLayer = -1;
        private int _swordHandLayer = -1;
        private bool _resolved;

        /// <summary>True when Base Layer is in an attack state. Ragdoll disables animator so this becomes false.</summary>
        public bool IsAttacking { get; private set; }

        // When true, keep arm/hand layers suppressed (two-handed items use full body)
        private bool _forceTwoHandedMode;

        /// <summary>Call from HandSystem to suppress arm/hand override layers for two-handed items.</summary>
        public void SetTwoHandedMode(bool value) => _forceTwoHandedMode = value;

        private static readonly int[] AttackStateHashes =
        {
            Animator.StringToHash("A_Attack_LightCombo01A_Sword"),
            Animator.StringToHash("A_Attack_LightCombo01A_WindUp_Sword"),
            Animator.StringToHash("A_Attack_LightCombo01A_Hit_Sword"),
            Animator.StringToHash("A_Attack_LightCombo01A_FollowThrough_Sword"),
        };

        private void Update()
        {
            if (!TryResolve()) { IsAttacking = false; return; }
            IsAttacking = IsBaseLayerInAttackState();
        }

        private void LateUpdate()
        {
            if (!TryResolve()) return;

            bool suppress = IsAttacking || _forceTwoHandedMode;
            float armWeight = suppress ? 0f : swordArmWeightIdle;
            float handWeight = suppress ? 0f : swordHandWeightIdle;

            if (_swordArmLayer >= 0)
                animator.SetLayerWeight(_swordArmLayer, armWeight);
            if (_swordHandLayer >= 0)
                animator.SetLayerWeight(_swordHandLayer, handWeight);
        }

        private bool TryResolve()
        {
            if (_resolved) return _swordArmLayer >= 0 || _swordHandLayer >= 0;
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            if (animator == null || animator.runtimeAnimatorController == null) return false;

            if (animator.GetComponent<AttackStepReceiver>() == null)
                animator.gameObject.AddComponent<AttackStepReceiver>();

            for (int i = 0; i < animator.layerCount; i++)
            {
                string name = animator.GetLayerName(i);
                if (name == "SwordArm") _swordArmLayer = i;
                else if (name == "SwordHand") _swordHandLayer = i;
            }
            _resolved = true;
            return _swordArmLayer >= 0 || _swordHandLayer >= 0;
        }

        private bool IsBaseLayerInAttackState()
        {
            var state = animator.GetCurrentAnimatorStateInfo(0);
            var nextState = animator.GetNextAnimatorStateInfo(0);

            if (animator.IsInTransition(0))
            {
                if (IsAttackState(nextState)) return true;
            }
            return IsAttackState(state);
        }

        private static bool IsAttackState(AnimatorStateInfo state)
        {
            int hash = state.shortNameHash;
            foreach (int attackHash in AttackStateHashes)
                if (hash == attackHash) return true;
            return false;
        }
    }
}
