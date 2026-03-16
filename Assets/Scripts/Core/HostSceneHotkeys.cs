using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DungeonGame.Core
{
    /// <summary>
    /// Temporary host-only hotkeys for rapid iteration.
    /// - F5: load Spire_Layer (clients follow via Netcode scene manager)
    /// - F6: load Town
    /// 
    /// Remove/replace with proper UI later.
    /// </summary>
    public class HostSceneHotkeys : MonoBehaviour
    {
        [SerializeField] private string townSceneName = "Town";
        [SerializeField] private string spireSceneName = "Spire_Slice";

        private void Awake()
        {
            // Auto-migrate older serialized scene names.
            if (spireSceneName == "Spire_Layer") spireSceneName = "Spire_Slice";
        }

        private void Update()
        {
            // F5/F6 scene hotkeys removed to avoid conflicting with other F-key bindings.
            // Use UI or a different input scheme for scene switching.
        }

        private void LoadNetworkScene(string sceneName)
        {
            var active = SceneManager.GetActiveScene().name;
            if (active == sceneName) return;

            Debug.Log($"[Net] Loading scene: {sceneName}");
            NetworkManager.Singleton.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }
    }
}
