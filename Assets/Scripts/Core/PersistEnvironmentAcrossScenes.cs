using UnityEngine;

namespace DungeonGame.Core
{
    /// <summary>
    /// Keeps this GameObject (and its children) alive when loading other scenes.
    /// Use on a parent of P_Sky, Global Fog, or any "Env Elements" root so they stay visible
    /// when the game loads Spire_Slice or other scenes via LoadSceneMode.Single (which unloads Town).
    /// </summary>
    public class PersistEnvironmentAcrossScenes : MonoBehaviour
    {
        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }
    }
}
