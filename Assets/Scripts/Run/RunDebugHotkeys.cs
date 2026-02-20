using DungeonGame.Core;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace DungeonGame.Run
{
    /// <summary>
    /// MVP hotkeys to simulate spire progression.
    /// 
    /// Host-only:
    /// - F10: +1 floor
    /// - F11: +5 floors
    /// - F12: Evac (knock back 1 segment) + return to Town
    /// </summary>
    public class RunDebugHotkeys : MonoBehaviour
    {
        [SerializeField] private string townSceneName = "Town";

        private SpireRunState run;

        private void Awake()
        {
            run = GetComponent<SpireRunState>();
            if (run == null) run = FindFirstObjectByType<SpireRunState>();
        }

        private void Update()
        {
            if (Keyboard.current == null) return;

            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening || !nm.IsServer) return;
            if (run == null) return;

            if (Keyboard.current.f10Key.wasPressedThisFrame)
            {
                run.AddFloorRpc(1);
            }

            if (Keyboard.current.f11Key.wasPressedThisFrame)
            {
                run.AddFloorRpc(5);
            }

            if (Keyboard.current.f12Key.wasPressedThisFrame)
            {
                run.EvacRpc();

                if (nm.SceneManager != null && SceneManager.GetActiveScene().name != townSceneName)
                {
                    Debug.Log("[Run] EVAC: loading Town");
                    nm.SceneManager.LoadScene(townSceneName, LoadSceneMode.Single);
                }
            }
        }
    }
}
