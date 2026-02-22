using Unity.Netcode;
using UnityEngine;

namespace DungeonGame.Run
{
    /// <summary>
    /// Creates and spawns the SpireRunState network object when the host/server starts.
    /// 
    /// Why:
    /// Netcode disallows NetworkBehaviours as children of the NetworkManager.
    /// This bootstrap lets you keep NetworkManager clean and still have a persistent run-state singleton.
    /// 
    /// Setup:
    /// - Create a prefab with: NetworkObject + SpireRunState + (optional) RunDebugHotkeys/RunDebugHUD
    /// - Assign it to runStatePrefab.
    /// - Put this component on a non-NetworkManager GameObject in the Town scene (e.g., Bootstrap).
    /// </summary>
    public class RunStateBootstrap : MonoBehaviour
    {
        [SerializeField] private SpireRunState runStatePrefab;

        private void OnEnable()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnServerStarted += HandleServerStarted;
            }
        }

        private void OnDisable()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnServerStarted -= HandleServerStarted;
            }
        }

        private void HandleServerStarted()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;
            if (!nm.IsServer) return;

            // Already exists?
            if (FindFirstObjectByType<SpireRunState>() != null) return;

            if (runStatePrefab == null)
            {
                Debug.LogError("[Run] RunStateBootstrap missing runStatePrefab. Create a RunState prefab and assign it.");
                return;
            }

            var instance = Instantiate(runStatePrefab);
            DontDestroyOnLoad(instance.gameObject);

            var no = instance.GetComponent<NetworkObject>();
            if (no == null)
            {
                Debug.LogError("[Run] RunState prefab missing NetworkObject.");
                Destroy(instance.gameObject);
                return;
            }

            no.Spawn(destroyWithScene: false);
            Debug.Log("[Run] Spawned SpireRunState singleton");
        }
    }
}
