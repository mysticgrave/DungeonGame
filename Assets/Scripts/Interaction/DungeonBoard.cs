using DungeonGame.Run;
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
    /// Flow: Interact → DungeonSelectUI (pick dungeon) → optional Pick3 → TownPlayController.EnterDungeon(config).
    /// Falls back to direct entry if DungeonSelectUI is not in the scene.
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

            // Don't show prompt if selection UI is already open
            if (DungeonSelectUI.Instance != null && DungeonSelectUI.Instance.IsVisible) return false;

            return true;
        }

        public void Interact(ulong clientId)
        {
            if (!IsServer) return;

            // Show dungeon selection UI if available
            var selectUI = DungeonSelectUI.Instance;
            if (selectUI != null)
            {
                selectUI.OnDungeonSelected -= OnDungeonChosen;
                selectUI.OnDungeonSelected += OnDungeonChosen;
                selectUI.Show();
                return;
            }

            // Fallback: no selection UI, go directly
            EnterWithConfig(null);
        }

        private void OnDungeonChosen(DungeonConfig config)
        {
            if (DungeonSelectUI.Instance != null)
                DungeonSelectUI.Instance.OnDungeonSelected -= OnDungeonChosen;

            EnterWithConfig(config);
        }

        private void EnterWithConfig(DungeonConfig config)
        {
            // Show Pick3 if assigned
            if (pick3Controller != null)
            {
                // Store the chosen config on SpireRunState before Pick3
                var runState = FindFirstObjectByType<SpireRunState>();
                if (runState != null)
                    runState.SetDungeonConfig(config);

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
                play.EnterDungeon(config);
            }
            else
            {
                // Fallback: load directly
                Debug.Log("[DungeonBoard] No TownPlayController found, loading dungeon directly.");
                var nm = NetworkManager.Singleton;
                if (nm != null && nm.IsServer && nm.SceneManager != null)
                {
                    var runState = FindFirstObjectByType<SpireRunState>();
                    if (runState != null)
                        runState.SetDungeonConfig(config);

                    string scene = config != null ? config.sceneName : dungeonSceneName;
                    var loader = LoadingScreenManager.Instance;
                    if (loader != null) loader.Show();
                    nm.SceneManager.LoadScene(scene, LoadSceneMode.Single);
                }
            }
        }
    }
}
