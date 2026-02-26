using UnityEngine;

namespace DungeonGame.Combat
{
    /// <summary>
    /// Optional feedback when a NetworkHealth on this object (or a child) takes damage.
    /// Assign hitSound and/or hitVfx; they play once per damage event.
    /// </summary>
    [RequireComponent(typeof(NetworkHealth))]
    public class HitFeedback : MonoBehaviour
    {
        [SerializeField] private AudioClip hitSound;
        [SerializeField] private ParticleSystem hitVfx;
        [Tooltip("Optional. If set, sound plays at this transform (e.g. camera listener).")]
        [SerializeField] private Transform soundEmitFrom;

        private NetworkHealth _health;

        private void Awake()
        {
            _health = GetComponent<NetworkHealth>();
        }

        private void OnEnable()
        {
            if (_health != null)
                _health.OnDamaged += OnDamaged;
        }

        private void OnDisable()
        {
            if (_health != null)
                _health.OnDamaged -= OnDamaged;
        }

        private void OnDamaged(int amount)
        {
            if (hitSound != null)
            {
                var from = soundEmitFrom != null ? soundEmitFrom.position : transform.position;
                AudioSource.PlayClipAtPoint(hitSound, from);
            }

            if (hitVfx != null && hitVfx.gameObject.activeInHierarchy)
                hitVfx.Play();
        }
    }
}
