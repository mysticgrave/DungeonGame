using DungeonGame.UI;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DungeonGame.Interaction
{
    /// <summary>
    /// In-world interactable (quest board, NPC, portal, etc.) that starts a dungeon run.
    /// Place on any GameObject with a collider in the Town scene.
    /// Host-only: only the server/host player can interact.
    ///
    /// If a Pick3Controller reference is assigned, the modifier-pick UI is shown first;
    /// otherwise the dungeon loads immediately via TownPlayController.
    /// </summary>
    public class DungeonBoard : NetworkBehaviour, IInteractable
    {
        [Header("Config")]
        [SerializeField] private string dungeonSceneName = "Spire_Slice";

        [Tooltip("Optional. If set, shows the Pick-3 modifier selection before entering the dungeon.")]
        [SerializeField] private Pick3Controller pick3Controller;

        [Tooltip("Optional. If not set, will be found automatically in the scene.")]
        [SerializeField] private TownPlayController townPlayController;

        // ─── IInteractable ──────────────────────────────────────────

        public string InteractPrompt => "Enter Dungeon";

        public bool CanInteract(ulong clientId)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return false;

            // Host-only
            if (clientId != nm.LocalClientId || !nm.IsServer) return false;

            // Can't enter if already in dungeon
            if (SceneManager.GetActiveScene().name == dungeonSceneName) return false;

            return true;
        }

        public void Interact(ulong clientId)
        {
            if (!IsServer) return;

            // Show Pick3 if assigned
            if (pick3Controller != null)
            {
                // Unlock cursor for the pick UI
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                pick3Controller.Show();
                return;
            }

            // Otherwise go straight into the dungeon
            var play = townPlayController != null
                ? townPlayController
                : FindFirstObjectByType<TownPlayController>();

            if (play != null)
            {
                play.EnterDungeon();
            }
            else
            {
                // Fallback: load directly
                Debug.Log("[DungeonBoard] No TownPlayController found, loading dungeon directly.");
                var nm = NetworkManager.Singleton;
                if (nm != null && nm.IsServer && nm.SceneManager != null)
                {
                    var loader = LoadingScreenManager.Instance;
                    if (loader != null) loader.Show();
                    nm.SceneManager.LoadScene(dungeonSceneName, LoadSceneMode.Single);
                }
            }
        }
    }
}
