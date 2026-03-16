using System.Collections;
using DungeonGame.Player;
using UnityEngine;

namespace DungeonGame.Combat
{
    /// <summary>
    /// Static utility for combat feel effects: hitstop and camera shake.
    /// Call from anywhere — effects are client-side only.
    /// Hitstop freezes the local player's Animator (NOT Time.timeScale, which breaks networking).
    /// </summary>
    public static class CombatJuice
    {
        private static JuiceRunner _runner;

        private static JuiceRunner Runner
        {
            get
            {
                if (_runner == null)
                {
                    var go = new GameObject("[CombatJuice]");
                    go.hideFlags = HideFlags.HideAndDontSave;
                    Object.DontDestroyOnLoad(go);
                    _runner = go.AddComponent<JuiceRunner>();
                }
                return _runner;
            }
        }

        /// <summary>
        /// Brief freeze of the local player's animator to sell impact.
        /// Does NOT touch Time.timeScale (safe for multiplayer).
        /// </summary>
        public static void HitStop(float duration = 0.04f)
        {
            Runner.RunHitStop(duration);
        }

        /// <summary>Shake the local player's camera.</summary>
        public static void Shake(float intensity = 0.15f, float duration = 0.2f)
        {
            var cam = LocalPlayerCameraRig.Instance;
            if (cam != null)
                cam.Shake(intensity, duration);
        }

        /// <summary>
        /// Full hit feedback for when the local player deals damage.
        /// Scales shake intensity by damage amount.
        /// </summary>
        public static void OnPlayerDealtHit(DamageInfo info)
        {
            HitStop(0.04f);

            // Scale shake by damage: 1 damage = subtle, 5+ = strong
            float intensity = Mathf.Lerp(0.08f, 0.25f, Mathf.InverseLerp(1f, 8f, info.Amount));
            Shake(intensity, 0.15f);
        }

        /// <summary>Feedback for when the local player takes damage.</summary>
        public static void OnPlayerTookHit(DamageInfo info)
        {
            float intensity = Mathf.Lerp(0.1f, 0.3f, Mathf.InverseLerp(1f, 5f, info.Amount));
            Shake(intensity, 0.2f);
        }

        /// <summary>Hidden MonoBehaviour for running coroutines from static context.</summary>
        private class JuiceRunner : MonoBehaviour
        {
            private Animator _cachedAnimator;
            private Coroutine _hitStopCoroutine;

            public void RunHitStop(float duration)
            {
                // Find local player's animator if not cached
                if (_cachedAnimator == null)
                {
                    var cam = LocalPlayerCameraRig.Instance;
                    if (cam != null)
                        _cachedAnimator = cam.GetComponentInChildren<Animator>(true);
                }

                if (_cachedAnimator == null) return;

                if (_hitStopCoroutine != null)
                    StopCoroutine(_hitStopCoroutine);
                _hitStopCoroutine = StartCoroutine(HitStopRoutine(_cachedAnimator, duration));
            }

            private IEnumerator HitStopRoutine(Animator animator, float duration)
            {
                if (animator == null) yield break;

                float originalSpeed = animator.speed;
                animator.speed = 0f;

                // Wait in real time (unscaled)
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }

                if (animator != null)
                    animator.speed = originalSpeed;

                _hitStopCoroutine = null;
            }
        }
    }
}
