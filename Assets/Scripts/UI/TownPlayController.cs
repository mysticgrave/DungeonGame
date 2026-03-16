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
        /// Loads the dungeon scene. Host-only. Wire this to your Play / Enter Dungeon button's On Click.
        /// </summary>
        public void EnterDungeon()
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

            if (SceneManager.GetActiveScene().name == dungeonSceneName)
            {
                Debug.Log("[TownPlay] Already in dungeon.");
                return;
            }

            var loader = loadingScreen != null ? loadingScreen : LoadingScreenManager.Instance;
            if (loader != null)
                loader.Show();

            Debug.Log($"[TownPlay] Loading dungeon: {dungeonSceneName}");
            nm.SceneManager.LoadScene(dungeonSceneName, LoadSceneMode.Single);
        }
    }
}
