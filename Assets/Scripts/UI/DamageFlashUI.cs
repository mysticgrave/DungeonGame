using DungeonGame.Combat;
using DungeonGame.Player;
using Unity.Netcode;
using UnityEngine;

namespace DungeonGame.UI
{
    /// <summary>
    /// Full-screen red flash when the local player takes damage.
    /// Place on the player prefab — only runs on the local owner client.
    /// Also triggers CombatJuice.OnPlayerTookHit for camera shake.
    /// </summary>
    public class DamageFlashUI : NetworkBehaviour
    {
        [Header("Flash Settings")]
        [SerializeField] private Color flashColor = new(0.8f, 0f, 0f, 0.35f);
        [SerializeField] private float fadeSpeed = 3f;

        private float _flashAlpha;
        private bool _isLocalOwner;
        private Texture2D _flashTex;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _isLocalOwner = IsOwner;

            if (_isLocalOwner)
            {
                PlayerHealth.OnAnyPlayerDamaged += HandlePlayerDamaged;

                _flashTex = new Texture2D(1, 1);
                _flashTex.SetPixel(0, 0, Color.white);
                _flashTex.Apply();
            }
        }

        public override void OnNetworkDespawn()
        {
            if (_isLocalOwner)
            {
                PlayerHealth.OnAnyPlayerDamaged -= HandlePlayerDamaged;

                if (_flashTex != null)
                {
                    Destroy(_flashTex);
                    _flashTex = null;
                }
            }
            base.OnNetworkDespawn();
        }

        private void HandlePlayerDamaged(ulong clientId, DamageInfo info)
        {
            // Only flash for the local player
            if (!_isLocalOwner) return;
            if (clientId != NetworkManager.Singleton.LocalClientId) return;

            // Scale flash intensity by damage ratio
            var playerHealth = GetComponent<PlayerHealth>();
            float ratio = playerHealth != null && playerHealth.MaxHp > 0
                ? (float)info.Amount / playerHealth.MaxHp
                : 0.3f;

            _flashAlpha = Mathf.Clamp01(ratio * 2f + 0.15f); // min visible flash

            // Camera shake for taking damage
            CombatJuice.OnPlayerTookHit(info);
        }

        private void Update()
        {
            if (!_isLocalOwner) return;
            if (_flashAlpha > 0f)
            {
                _flashAlpha -= fadeSpeed * Time.deltaTime;
                if (_flashAlpha < 0f) _flashAlpha = 0f;
            }
        }

        private void OnGUI()
        {
            if (!_isLocalOwner) return;
            if (_flashAlpha <= 0.001f) return;
            if (Event.current.type != EventType.Repaint) return;

            Color col = flashColor;
            col.a = flashColor.a * _flashAlpha;
            GUI.color = col;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _flashTex);
            GUI.color = Color.white;
        }
    }
}
