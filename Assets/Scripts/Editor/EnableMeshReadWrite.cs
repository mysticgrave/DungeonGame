#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DungeonGame.Editor
{
    /// <summary>
    /// One-click fix: enable Read/Write on all model meshes so runtime NavMesh baking (and other mesh read) works in builds.
    /// Menu: Tools → DungeonGame → Enable Read/Write on All Model Meshes
    /// </summary>
    public static class EnableMeshReadWrite
    {
        private static readonly string[] ModelExtensions = { ".fbx", ".obj", ".blend", ".3ds", ".dae", ".dxf", ".mb", ".ma", ".max", ".c4d" };

        [MenuItem("Tools/DungeonGame/Enable Read/Write on All Model Meshes")]
        public static void EnableReadWriteOnAllModels()
        {
            var paths = new List<string>();
            foreach (string path in AssetDatabase.GetAllAssetPaths())
            {
                if (!path.StartsWith("Assets/")) continue;
                if (path.StartsWith("Assets/Plugins")) continue;
                string ext = Path.GetExtension(path).ToLowerInvariant();
                foreach (string e in ModelExtensions)
                {
                    if (ext == e)
                    {
                        paths.Add(path);
                        break;
                    }
                }
            }

            int total = 0;
            int skipped = 0;
            for (int i = 0; i < paths.Count; i++)
            {
                string path = paths[i];
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) continue;
                if (importer.isReadable) { skipped++; continue; }
                importer.isReadable = true;
                importer.SaveAndReimport();
                total++;
                if (i % 20 == 0 || i == paths.Count - 1)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("Enable Read/Write on Meshes",
                            $"{total} updated, {skipped} skipped — {path}",
                            (float)(i + 1) / paths.Count))
                        break;
                }
            }
            EditorUtility.ClearProgressBar();
            Debug.Log($"[EnableMeshReadWrite] Set Read/Write on {total} model(s), skipped {skipped} (already readable). Total scanned: {paths.Count}");
        }
    }
}
#endif
