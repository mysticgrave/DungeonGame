#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace DungeonGame.Editor
{
    /// <summary>
    /// Removes MeshCollider from any Terrain GameObject. Unity does not support MeshCollider on Terrain (use TerrainCollider only).
    /// Run via Tools → DungeonGame → Remove MeshCollider From Terrain, or fix manually in the Inspector.
    /// </summary>
    public static class RemoveMeshColliderFromTerrain
    {
        [MenuItem("Tools/DungeonGame/Remove MeshCollider From Terrain")]
        public static void Run()
        {
            int removed = 0;
            var terrains = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
            foreach (var terrain in terrains)
            {
                var mc = terrain.GetComponent<MeshCollider>();
                if (mc != null)
                {
                    Object.DestroyImmediate(mc);
                    removed++;
                    Debug.Log($"[DungeonGame] Removed MeshCollider from Terrain '{terrain.gameObject.name}' (Terrain uses TerrainCollider only).");
                }
            }
            if (removed == 0)
                Debug.Log("[DungeonGame] No Terrain had a MeshCollider. Nothing to remove.");
        }
    }
}
#endif
