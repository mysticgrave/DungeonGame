using DungeonGame.Meta;
using UnityEngine;

namespace DungeonGame.Core
{
    /// <summary>
    /// Ensures MetaProgression is initialized before the first scene loads,
    /// so player data can be saved/loaded before they host or join.
    /// No scene setup required — runs automatically on game launch.
    /// </summary>
    public static class PersistenceBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void OnBeforeSceneLoad()
        {
            EnsureMetaProgression();
        }

        private static void EnsureMetaProgression()
        {
            if (MetaProgression.Instance != null) return;

            var go = new GameObject("MetaProgression");
            go.AddComponent<MetaProgression>(); // MetaProgression.Awake calls DontDestroyOnLoad
            Debug.Log("[Persistence] MetaProgression initialized at launch — data ready for save/load.");
        }
    }
}
