#if !DISABLESTEAMWORKS
using UnityEngine;

namespace DungeonGame.Core
{
    /// <summary>
    /// Initializes and ticks Steamworks via Facepunch.Steamworks.
    /// Place on a persistent GameObject in the first scene (e.g. NetworkManager).
    /// </summary>
    [DefaultExecutionOrder(-2000)]
    public class SteamManager : MonoBehaviour
    {
        [Tooltip("Your Steam App ID. Use 480 (Spacewar) for testing.")]
        [SerializeField] private uint appId = 480;

        public static bool Initialized { get; private set; }

        private void Awake()
        {
            if (Initialized)
            {
                Destroy(gameObject);
                return;
            }

            DontDestroyOnLoad(gameObject);

            try
            {
                Steamworks.SteamClient.Init(appId, false);
                Initialized = true;
                Debug.Log($"[Steam] Initialized — logged in as: {Steamworks.SteamClient.Name} (ID: {Steamworks.SteamClient.SteamId})");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Steam] Failed to initialize: {e.Message}. Is Steam running?");
                Initialized = false;
            }
        }

        private void Update()
        {
            if (Initialized)
                Steamworks.SteamClient.RunCallbacks();
        }

        private void OnApplicationQuit()
        {
            if (Initialized)
            {
                Steamworks.SteamClient.Shutdown();
                Initialized = false;
            }
        }

        private void OnDestroy()
        {
            if (Initialized)
            {
                Steamworks.SteamClient.Shutdown();
                Initialized = false;
            }
        }
    }
}
#endif
