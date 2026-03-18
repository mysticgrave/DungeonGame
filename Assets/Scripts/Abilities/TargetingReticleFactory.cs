using DungeonGame.Combat;
using UnityEngine;
using UnityEngine.Rendering;

namespace DungeonGame.Abilities
{
    /// <summary>
    /// Builds procedural mesh reticles for ability targeting.
    /// Supports Circle (filled disc), Ring (hollow outline), and Arc (directional fan).
    /// </summary>
    public static class TargetingReticleFactory
    {
        private const int CircleSegments = 48;
        private const int ArcSegments = 32;
        private const float DefaultArcDegrees = 120f;

        /// <summary>
        /// Create the appropriate reticle for an ability config.
        /// </summary>
        public static GameObject Create(AbilityConfig config)
        {
            ReticleShape shape = ResolveShape(config);
            Color color = GetReticleColor(config.damageType);
            float radius = config.radius;

            return shape switch
            {
                ReticleShape.Circle => CreateCircle(radius, color),
                ReticleShape.Ring => CreateRing(radius, color),
                ReticleShape.Arc => CreateArc(radius, DefaultArcDegrees, color),
                _ => CreateCircle(radius, color)
            };
        }

        /// <summary>
        /// Resolve Auto shape to a concrete shape based on ability type.
        /// </summary>
        public static ReticleShape ResolveShape(AbilityConfig config)
        {
            if (config.reticleShape != ReticleShape.Auto)
                return config.reticleShape;

            return config.abilityType switch
            {
                AbilityType.MeleeAoE => ReticleShape.Ring,
                AbilityType.AreaZone => ReticleShape.Circle,
                _ => ReticleShape.Circle
            };
        }

        /// <summary>
        /// Whether this ability's targeting reticle is centered on the player (true)
        /// or follows the mouse on the ground (false).
        /// </summary>
        public static bool IsSelfCentered(AbilityConfig config)
        {
            // MeleeAoE is always around the caster
            return config.abilityType == AbilityType.MeleeAoE;
        }

        // ──────────────────── Circle (filled disc) ────────────────────

        public static GameObject CreateCircle(float radius, Color color)
        {
            var go = new GameObject("Reticle_Circle");
            var mesh = BuildDiscMesh(radius, CircleSegments);
            SetupReticle(go, mesh, color);

            // Add an edge ring for visual clarity
            AddEdgeRing(go, radius, color);

            return go;
        }

        // ──────────────────── Ring (hollow outline) ────────────────────

        public static GameObject CreateRing(float radius, Color color)
        {
            var go = new GameObject("Reticle_Ring");
            float thickness = Mathf.Max(0.12f, radius * 0.08f);
            var mesh = BuildRingMesh(radius - thickness, radius, CircleSegments);
            SetupReticle(go, mesh, color * 1.2f);

            // Add a faint inner fill so you can see the full area
            var innerGo = new GameObject("InnerFill");
            innerGo.transform.SetParent(go.transform, false);
            innerGo.transform.localPosition = Vector3.up * -0.001f;
            var innerMesh = BuildDiscMesh(radius - thickness, CircleSegments);
            Color faintColor = color;
            faintColor.a = 0.08f;
            SetupReticle(innerGo, innerMesh, faintColor);

            return go;
        }

        // ──────────────────── Arc (directional fan) ────────────────────

        public static GameObject CreateArc(float radius, float arcDegrees, Color color)
        {
            var go = new GameObject("Reticle_Arc");
            var mesh = BuildArcMesh(radius, arcDegrees, ArcSegments);
            SetupReticle(go, mesh, color);

            // Add edge lines for the arc boundary
            AddArcEdge(go, radius, arcDegrees, color);

            return go;
        }

        // ──────────────────── Mesh Builders ────────────────────

        private static Mesh BuildDiscMesh(float radius, int segments)
        {
            var verts = new Vector3[segments + 2];
            var tris = new int[segments * 3];
            var uvs = new Vector2[segments + 2];

            verts[0] = Vector3.zero;
            uvs[0] = new Vector2(0.5f, 0.5f);

            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                verts[i + 1] = new Vector3(x, 0f, z);
                uvs[i + 1] = new Vector2(0.5f + Mathf.Cos(angle) * 0.5f, 0.5f + Mathf.Sin(angle) * 0.5f);

                if (i < segments)
                {
                    tris[i * 3] = 0;
                    tris[i * 3 + 1] = i + 1;
                    tris[i * 3 + 2] = i + 2;
                }
            }

            var mesh = new Mesh { name = "Disc" };
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.uv = uvs;
            mesh.RecalculateNormals();
            return mesh;
        }

        private static Mesh BuildRingMesh(float innerRadius, float outerRadius, int segments)
        {
            var verts = new Vector3[(segments + 1) * 2];
            var tris = new int[segments * 6];
            var uvs = new Vector2[(segments + 1) * 2];

            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                int vi = i * 2;
                verts[vi] = new Vector3(cos * innerRadius, 0f, sin * innerRadius);
                verts[vi + 1] = new Vector3(cos * outerRadius, 0f, sin * outerRadius);
                uvs[vi] = new Vector2((float)i / segments, 0f);
                uvs[vi + 1] = new Vector2((float)i / segments, 1f);

                if (i < segments)
                {
                    int ti = i * 6;
                    tris[ti] = vi;
                    tris[ti + 1] = vi + 1;
                    tris[ti + 2] = vi + 2;
                    tris[ti + 3] = vi + 1;
                    tris[ti + 4] = vi + 3;
                    tris[ti + 5] = vi + 2;
                }
            }

            var mesh = new Mesh { name = "Ring" };
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.uv = uvs;
            mesh.RecalculateNormals();
            return mesh;
        }

        private static Mesh BuildArcMesh(float radius, float arcDegrees, int segments)
        {
            float halfArc = arcDegrees * 0.5f * Mathf.Deg2Rad;
            var verts = new Vector3[segments + 2];
            var tris = new int[segments * 3];
            var uvs = new Vector2[segments + 2];

            // Center vertex at origin (the player)
            verts[0] = Vector3.zero;
            uvs[0] = new Vector2(0.5f, 0f);

            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float angle = Mathf.Lerp(-halfArc, halfArc, t);
                // Forward is +Z in local space
                float x = Mathf.Sin(angle) * radius;
                float z = Mathf.Cos(angle) * radius;
                verts[i + 1] = new Vector3(x, 0f, z);
                uvs[i + 1] = new Vector2(t, 1f);

                if (i < segments)
                {
                    tris[i * 3] = 0;
                    tris[i * 3 + 1] = i + 1;
                    tris[i * 3 + 2] = i + 2;
                }
            }

            var mesh = new Mesh { name = "Arc" };
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.uv = uvs;
            mesh.RecalculateNormals();
            return mesh;
        }

        // ──────────────────── Visual Helpers ────────────────────

        private static void SetupReticle(GameObject go, Mesh mesh, Color color)
        {
            var mf = go.GetComponent<MeshFilter>();
            if (mf == null) mf = go.AddComponent<MeshFilter>();
            mf.mesh = mesh;

            var mr = go.GetComponent<MeshRenderer>();
            if (mr == null) mr = go.AddComponent<MeshRenderer>();

            var mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = color;
            mr.material = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        private static void AddEdgeRing(GameObject parent, float radius, Color color)
        {
            var edgeGo = new GameObject("Edge");
            edgeGo.transform.SetParent(parent.transform, false);
            edgeGo.transform.localPosition = Vector3.up * 0.001f;

            float thickness = Mathf.Max(0.06f, radius * 0.04f);
            var mesh = BuildRingMesh(radius - thickness, radius + thickness, CircleSegments);

            Color edgeColor = color;
            edgeColor.a = Mathf.Min(1f, color.a * 2.5f);
            SetupReticle(edgeGo, mesh, edgeColor);
        }

        private static void AddArcEdge(GameObject parent, float radius, float arcDegrees, Color color)
        {
            // Build an arc outline (just the perimeter, not filled)
            var edgeGo = new GameObject("ArcEdge");
            edgeGo.transform.SetParent(parent.transform, false);
            edgeGo.transform.localPosition = Vector3.up * 0.001f;

            float halfArc = arcDegrees * 0.5f * Mathf.Deg2Rad;
            float thickness = Mathf.Max(0.06f, radius * 0.04f);

            // Build edge as a thin arc ring + two side lines
            var mesh = BuildArcRingMesh(radius - thickness, radius + thickness, arcDegrees, ArcSegments);

            Color edgeColor = color;
            edgeColor.a = Mathf.Min(1f, color.a * 2.5f);
            SetupReticle(edgeGo, mesh, edgeColor);

            // Side lines (two thin quads from center to arc edge)
            AddSideLine(parent, radius, halfArc, thickness, edgeColor);
            AddSideLine(parent, radius, -halfArc, thickness, edgeColor);
        }

        private static Mesh BuildArcRingMesh(float innerRadius, float outerRadius, float arcDegrees, int segments)
        {
            float halfArc = arcDegrees * 0.5f * Mathf.Deg2Rad;
            var verts = new Vector3[(segments + 1) * 2];
            var tris = new int[segments * 6];

            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float angle = Mathf.Lerp(-halfArc, halfArc, t);
                float sin = Mathf.Sin(angle);
                float cos = Mathf.Cos(angle);

                int vi = i * 2;
                verts[vi] = new Vector3(sin * innerRadius, 0f, cos * innerRadius);
                verts[vi + 1] = new Vector3(sin * outerRadius, 0f, cos * outerRadius);

                if (i < segments)
                {
                    int ti = i * 6;
                    tris[ti] = vi;
                    tris[ti + 1] = vi + 1;
                    tris[ti + 2] = vi + 2;
                    tris[ti + 3] = vi + 1;
                    tris[ti + 4] = vi + 3;
                    tris[ti + 5] = vi + 2;
                }
            }

            var mesh = new Mesh { name = "ArcRing" };
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            return mesh;
        }

        private static void AddSideLine(GameObject parent, float length, float angle, float thickness, Color color)
        {
            var go = new GameObject("SideLine");
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = Vector3.up * 0.001f;

            float sin = Mathf.Sin(angle);
            float cos = Mathf.Cos(angle);
            Vector3 dir = new Vector3(sin, 0f, cos);
            Vector3 perp = new Vector3(cos, 0f, -sin);

            var verts = new Vector3[4];
            verts[0] = -perp * thickness;
            verts[1] = perp * thickness;
            verts[2] = dir * length - perp * thickness;
            verts[3] = dir * length + perp * thickness;

            var tris = new int[] { 0, 2, 1, 1, 2, 3 };
            var mesh = new Mesh { name = "Line" };
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();

            SetupReticle(go, mesh, color);
        }

        // ──────────────────── Color ────────────────────

        private static Color GetReticleColor(DamageType damageType)
        {
            Color baseColor = damageType switch
            {
                DamageType.Fire => new Color(1f, 0.4f, 0.1f),
                DamageType.Ice => new Color(0.3f, 0.6f, 1f),
                DamageType.Lightning => new Color(0.7f, 0.7f, 1f),
                DamageType.Poison => new Color(0.3f, 0.9f, 0.2f),
                _ => new Color(1f, 0.9f, 0.6f) // Physical = warm
            };
            baseColor.a = 0.25f;
            return baseColor;
        }
    }
}
