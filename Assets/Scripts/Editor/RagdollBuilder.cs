#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DungeonGame.Editor
{
    /// <summary>
    /// Builds a ragdoll on a character with a known bone hierarchy (e.g. Polygon Dungeon Characters).
    /// Menu: Tools → DungeonGame → Build Ragdoll (select character root in Hierarchy first).
    /// The character root should be the GameObject that contains the skeleton (e.g. prefab root with Hips, Spine_01, etc. as descendants).
    /// </summary>
    public static class RagdollBuilder
    {
        private const float CapsuleRadius = 0.08f;
        private const float MassPerBone = 2f;
        private const float JointBreakForce = 5000f;

        /// <summary>
        /// Bone name -> parent bone name (empty = root). Order: parent before child.
        /// Matches Polygon/Synty Characters.fbx hierarchy.
        /// </summary>
        private static readonly (string name, string parent)[] PolygonCharacterBones =
        {
            ("Hips", ""),
            ("Spine_01", "Hips"),
            ("Spine_02", "Spine_01"),
            ("Spine_03", "Spine_02"),
            ("UpperLeg_L", "Hips"),
            ("UpperLeg_R", "Hips"),
            ("LowerLeg_L", "UpperLeg_L"),
            ("LowerLeg_R", "UpperLeg_R"),
            ("Shoulder_L", "Spine_03"),
            ("Shoulder_R", "Spine_03"),
            ("Elbow_L", "Shoulder_L"),
            ("Elbow_R", "Shoulder_R"),
            ("Hand_L", "Elbow_L"),
            ("Hand_R", "Elbow_R"),
            ("Neck", "Spine_03"),
            ("Head", "Neck"),
        };

        [MenuItem("Tools/DungeonGame/Build Ragdoll (Polygon Character)")]
        public static void BuildRagdoll()
        {
            var go = Selection.activeGameObject;
            if (go == null)
            {
                EditorUtility.DisplayDialog("Ragdoll Builder", "Select the character root in the Hierarchy (the object that contains Hips, Spine_01, etc.).", "OK");
                return;
            }

            var bones = FindBones(go.transform, PolygonCharacterBones);
            if (bones == null)
            {
                EditorUtility.DisplayDialog("Ragdoll Builder", "Could not find all bones. Ensure the character uses the Polygon Characters skeleton (Hips, Spine_01, UpperLeg_L, etc.).", "OK");
                return;
            }

            Undo.SetCurrentGroupName("Build Ragdoll");
            int undoGroup = Undo.GetCurrentGroup();

            foreach (var kvp in bones)
            {
                string boneName = kvp.Key;
                Transform t = kvp.Value;
                string parentName = GetParentName(boneName);
                Rigidbody parentRb = parentName != null && bones.TryGetValue(parentName, out var parentT) ? parentT.GetComponent<Rigidbody>() : null;

                AddRigidbody(t, boneName, parentRb != null);
                AddCapsuleCollider(t);
                AddCharacterJoint(t, parentRb);
            }

            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log($"[RagdollBuilder] Added ragdoll to {go.name} ({bones.Count} bones). Set root Rigidbody to Is Kinematic when used as player; enable/disable ragdoll by toggling Rigidbodies.");
        }

        private static string GetParentName(string boneName)
        {
            foreach (var b in PolygonCharacterBones)
            {
                if (b.name == boneName)
                    return string.IsNullOrEmpty(b.parent) ? null : b.parent;
            }
            return null;
        }

        private static Dictionary<string, Transform> FindBones(Transform root, (string name, string parent)[] boneList)
        {
            var all = new Dictionary<string, Transform>();
            CollectTransformsByName(root, all);

            var result = new Dictionary<string, Transform>();
            foreach (var (name, _) in boneList)
            {
                if (!all.TryGetValue(name, out var t))
                {
                    Debug.LogWarning($"[RagdollBuilder] Bone not found: {name}");
                    return null;
                }
                result[name] = t;
            }
            return result;
        }

        private static void CollectTransformsByName(Transform t, Dictionary<string, Transform> outMap)
        {
            if (!outMap.ContainsKey(t.name))
                outMap[t.name] = t;
            for (int i = 0; i < t.childCount; i++)
                CollectTransformsByName(t.GetChild(i), outMap);
        }

        private static void AddRigidbody(Transform t, string boneName, bool isChildBone)
        {
            if (t.GetComponent<Rigidbody>() != null) return;
            var rb = Undo.AddComponent<Rigidbody>(t.gameObject);
            rb.mass = MassPerBone;
            rb.linearDamping = 0.05f;
            rb.angularDamping = 0.05f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.constraints = RigidbodyConstraints.None;
            rb.isKinematic = true;
        }

        private static void AddCapsuleCollider(Transform t)
        {
            if (t.GetComponent<Collider>() != null) return;
            var cap = Undo.AddComponent<CapsuleCollider>(t.gameObject);
            cap.radius = CapsuleRadius;
            cap.direction = 1;
            cap.height = Mathf.Max(CapsuleRadius * 2f, 0.12f);
            cap.center = Vector3.zero;
            var mat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>("Assets/Materials/Physics/RagdollFriction.physicMaterial");
            if (mat != null) cap.material = mat;
        }

        private static void AddCharacterJoint(Transform t, Rigidbody connectedBody)
        {
            if (t.GetComponent<CharacterJoint>() != null) return;
            var joint = Undo.AddComponent<CharacterJoint>(t.gameObject);
            joint.connectedBody = connectedBody;
            joint.breakForce = JointBreakForce;
            joint.breakTorque = JointBreakForce * 0.5f;
            joint.enablePreprocessing = true;
        }
    }
}
#endif
