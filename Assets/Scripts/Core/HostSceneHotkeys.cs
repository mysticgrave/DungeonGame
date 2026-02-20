using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
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
            if (Keyboard.current == null) return;

            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening) return;

            // Only host/server can initiate network scene loads.
            if (!nm.IsServer) return;
            if (nm.SceneManager == null) return;

            if (Keyboard.current.f5Key.wasPressedThisFrame)
            {
                LoadNetworkScene(spireSceneName);
            }

            if (Keyboard.current.f6Key.wasPressedThisFrame)
            {
                LoadNetworkScene(townSceneName);
            }
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
