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
    /// Also: Tools → DungeonGame → Enable Read/Write on Synty Models Only (faster, targets dungeon assets)
    /// </summary>
    public static class EnableMeshReadWrite
    {
        private static readonly string[] ModelExtensions = { ".fbx", ".obj", ".blend", ".3ds", ".dae", ".dxf", ".mb", ".ma", ".max", ".c4d" };

        [MenuItem("Tools/DungeonGame/Enable Read/Write on All Model Meshes")]
        public static void EnableReadWriteOnAllModels()
        {
            ProcessPaths(GetAllModelPaths(includePackages: true));
        }

        [MenuItem("Tools/DungeonGame/Enable Read/Write on Synty Models Only")]
        public static void EnableReadWriteOnSyntyOnly()
        {
            var all = GetAllModelPaths(includePackages: true);
            var synty = new List<string>();
            foreach (string p in all)
            {
                if (p.IndexOf("Synty", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    synty.Add(p);
            }
            ProcessPaths(synty);
        }

        private static List<string> GetAllModelPaths(bool includePackages)
        {
            var paths = new List<string>();
            foreach (string path in AssetDatabase.GetAllAssetPaths())
            {
                if (!path.StartsWith("Assets/") && !(includePackages && path.StartsWith("Packages/"))) continue;
                if (path.StartsWith("Assets/Plugins")) continue;
                if (path.StartsWith("Packages/com.unity.")) continue;
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
            return paths;
        }

        private static void ProcessPaths(List<string> paths)
        {
            int total = 0;
            int skipped = 0;
            var updated = new List<string>();
            for (int i = 0; i < paths.Count; i++)
            {
                string path = paths[i];
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) continue;
                if (importer.isReadable) { skipped++; continue; }
                importer.isReadable = true;
                importer.SaveAndReimport();
                total++;
                updated.Add(path);
                if (i % 5 == 0 || i == paths.Count - 1)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("Enable Read/Write on Meshes",
                            $"{total} updated, {skipped} skipped — {Path.GetFileName(path)}",
                            (float)(i + 1) / paths.Count))
                        break;
                }
            }
            EditorUtility.ClearProgressBar();
            Debug.Log($"[EnableMeshReadWrite] Set Read/Write on {total} model(s), skipped {skipped} (already readable). Total scanned: {paths.Count}");
            if (updated.Count > 0 && updated.Count <= 20)
            {
                foreach (var p in updated) Debug.Log($"  - {p}");
            }
        }
    }
}
#endif
