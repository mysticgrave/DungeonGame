#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace DungeonGame.Editor
{
    /// <summary>
    /// Assigns the "IgnoreNavMesh" layer to selected objects so they are excluded from NavMesh baking.
    /// Create the layer in Edit → Project Settings → Tags and Layers, then uncheck it in NavMesh Surface → Include Layers.
    /// </summary>
    public static class SetIgnoreNavMeshLayer
    {
        public const string LayerName = "IgnoreNavMesh";

        [MenuItem("Tools/DungeonGame/Set Layer \"IgnoreNavMesh\" On Selected")]
        public static void SetLayerOnSelected()
        {
            int layer = LayerMask.NameToLayer(LayerName);
            if (layer < 0)
            {
                Debug.LogWarning($"[DungeonGame] Layer '{LayerName}' not found. Add it in Edit → Project Settings → Tags and Layers.");
                return;
            }

            int count = 0;
            foreach (var go in Selection.gameObjects)
            {
                if (go == null) continue;
                go.layer = layer;
                count++;
            }

            if (count > 0)
            {
                EditorUtility.SetDirty(Selection.activeGameObject);
                Debug.Log($"[DungeonGame] Set layer '{LayerName}' on {count} object(s). Uncheck this layer in NavMesh Surface → Include Layers to exclude them from the bake.");
            }
        }

        [MenuItem("Tools/DungeonGame/Set Layer \"IgnoreNavMesh\" On Selected", true)]
        public static bool ValidateSetLayerOnSelected()
        {
            return Selection.gameObjects != null && Selection.gameObjects.Length > 0;
        }
    }
}
#endif
