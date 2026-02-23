using Unity.Netcode;
using UnityEngine;

namespace DungeonGame.UI
{
    /// <summary>
    /// Minimal center-screen crosshair for MVP aiming.
    /// Owner-only.
    /// </summary>
    public class CrosshairUI : NetworkBehaviour
    {
        [SerializeField] private float size = 10f;
        [SerializeField] private float thickness = 2f;
        [SerializeField] private Color color = new(1f, 1f, 1f, 0.85f);

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsOwner)
            {
                enabled = false;
            }
        }

        private void OnGUI()
        {
            var w = Screen.width;
            var h = Screen.height;
            float cx = w * 0.5f;
            float cy = h * 0.5f;

            var old = GUI.color;
            GUI.color = color;

            // Horizontal
            GUI.DrawTexture(new Rect(cx - size, cy - thickness * 0.5f, size * 2f, thickness), Texture2D.whiteTexture);
            // Vertical
            GUI.DrawTexture(new Rect(cx - thickness * 0.5f, cy - size, thickness, size * 2f), Texture2D.whiteTexture);

            GUI.color = old;
        }
    }
}
