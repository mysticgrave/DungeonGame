using DungeonGame.Run;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DungeonGame.UI
{
    /// <summary>
    /// Attach to a GameObject in the Town scene (e.g. near your Play/Enter Dungeon button).
    /// Call EnterDungeon() from a Button's On Click — host-only; loads the first dungeon scene.
    /// Clients follow automatically via Netcode scene sync.
    /// </summary>
    public class TownPlayController : MonoBehaviour
    {
        [SerializeField] private string dungeonSceneName = "Spire_Slice";
        [Tooltip("Optional. If set, shows this loading panel when entering the dungeon.")]
        [SerializeField] private LoadingScreenManager loadingScreen;

        /// <summary>
        /// Loads the dungeon scene. Host-only. Wire this to your Play / Enter Dungeon button's On Click.
        /// Alias: LoadDungeonScene for Pick3Controller compatibility.
        /// </summary>
        public void LoadDungeonScene() => EnterDungeon();

        /// <summary>
        /// Loads the dungeon scene. Host-only.
        /// Optionally pass a DungeonConfig to set on SpireRunState before loading.
        /// </summary>
        public void EnterDungeon(DungeonConfig config = null)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null)
            {
                Debug.LogWarning("[TownPlay] No NetworkManager. Enter Dungeon requires hosting.");
                return;
            }

            if (!nm.IsServer)
            {
                Debug.Log("[TownPlay] Only the host can start the dungeon.");
                return;
            }

            if (nm.SceneManager == null)
            {
                Debug.LogWarning("[TownPlay] NetworkManager has no SceneManager.");
                return;
            }

            // Set config on SpireRunState so the generator can read it
            var runState = FindFirstObjectByType<SpireRunState>();
            if (runState != null)
                runState.SetDungeonConfig(config);

            string scene = config != null ? config.sceneName : dungeonSceneName;

            if (SceneManager.GetActiveScene().name == scene)
            {
                Debug.Log("[TownPlay] Already in dungeon.");
                return;
            }

            var loader = loadingScreen != null ? loadingScreen : LoadingScreenManager.Instance;
            if (loader != null)
                loader.Show();

            Debug.Log($"[TownPlay] Loading dungeon: {scene}" + (config != null ? $" (config: {config.displayName})" : ""));
            nm.SceneManager.LoadScene(scene, LoadSceneMode.Single);
        }
    }
}
