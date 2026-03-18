using UnityEngine;
using UnityEngine.Rendering;

namespace DungeonGame.Enemies.Boss
{
    /// <summary>
    /// Visual ground indicator for boss telegraphed attacks.
    /// Supports Circle (fill), Ring (expanding), and Line (beam) shapes.
    /// Spawned on all clients via ClientRpc. Auto-destroys after duration.
    /// </summary>
    public class BossAttackIndicator : MonoBehaviour
    {
        public enum Shape { Circle, Ring, Line }

        private Shape _shape;
        private float _radius;
        private float _duration;
        private float _startTime;
        private Color _warningColor;
        private Color _dangerColor;
        private float _lineLength;
        private float _lineWidth;

        private MeshFilter _mf;
        private MeshRenderer _mr;
        private Material _material;
        private bool _initialized;

        // ──────────────────── Static Creators ────────────────────

        /// <summary>
        /// Create a circle that fills up over duration, signaling an incoming AoE.
        /// </summary>
        public static BossAttackIndicator CreateCircle(Vector3 position, float radius, float duration, Color color)
        {
            var go = new GameObject("BossIndicator_Circle");
            go.transform.position = position + Vector3.up * 0.06f;
            var ind = go.AddComponent<BossAttackIndicator>();
            ind.InitCircle(radius, duration, color);
            return ind;
        }

        /// <summary>
        /// Create an expanding ring that grows outward, signaling an expanding wave.
        /// </summary>
        public static BossAttackIndicator CreateRing(Vector3 position, float maxRadius, float duration, Color color)
        {
            var go = new GameObject("BossIndicator_Ring");
            go.transform.position = position + Vector3.up * 0.06f;
            var ind = go.AddComponent<BossAttackIndicator>();
            ind.InitRing(maxRadius, duration, color);
            return ind;
        }

        /// <summary>
        /// Create a line indicator (for beam/channel attacks).
        /// </summary>
        public static BossAttackIndicator CreateLine(Vector3 start, Vector3 end, float width, float duration, Color color)
        {
            var go = new GameObject("BossIndicator_Line");
            go.transform.position = start + Vector3.up * 0.06f;
            var dir = end - start;
            if (dir.sqrMagnitude > 0.01f)
                go.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            var ind = go.AddComponent<BossAttackIndicator>();
            ind.InitLine(dir.magnitude, width, duration, color);
            return ind;
        }

        // ──────────────────── Init Methods ────────────────────

        private void InitCircle(float radius, float duration, Color color)
        {
            _shape = Shape.Circle;
            _radius = radius;
            _duration = duration;
            _warningColor = new Color(color.r, color.g, color.b, 0.15f);
            _dangerColor = new Color(color.r, color.g, color.b, 0.55f);
            _startTime = Time.time;

            SetupMesh();

            // Start with full circle outline
            _mf.mesh = BuildDiscMesh(_radius, 48);
            _material.color = _warningColor;
            _initialized = true;
        }

        private void InitRing(float radius, float duration, Color color)
        {
            _shape = Shape.Ring;
            _radius = radius;
            _duration = duration;
            _warningColor = new Color(color.r, color.g, color.b, 0.3f);
            _dangerColor = new Color(color.r, color.g, color.b, 0.6f);
            _startTime = Time.time;

            SetupMesh();
            _mf.mesh = BuildRingMesh(0.1f, 0.3f, 48);
            _material.color = _warningColor;
            _initialized = true;
        }

        private void InitLine(float length, float width, float duration, Color color)
        {
            _shape = Shape.Line;
            _lineLength = length;
            _lineWidth = width;
            _duration = duration;
            _warningColor = new Color(color.r, color.g, color.b, 0.2f);
            _dangerColor = new Color(color.r, color.g, color.b, 0.5f);
            _startTime = Time.time;

            SetupMesh();
            _mf.mesh = BuildLineMesh(length, width);
            _material.color = _warningColor;
            _initialized = true;
        }

        private void SetupMesh()
        {
            _mf = gameObject.AddComponent<MeshFilter>();
            _mr = gameObject.AddComponent<MeshRenderer>();
            _material = new Material(Shader.Find("Sprites/Default"));
            _mr.material = _material;
            _mr.shadowCastingMode = ShadowCastingMode.Off;
            _mr.receiveShadows = false;
        }

        // ──────────────────── Update ────────────────────

        private void Update()
        {
            if (!_initialized) return;

            float elapsed = Time.time - _startTime;
            float t = Mathf.Clamp01(elapsed / _duration);

            switch (_shape)
            {
                case Shape.Circle:
                    UpdateCircle(t);
                    break;
                case Shape.Ring:
                    UpdateRing(t);
                    break;
                case Shape.Line:
                    UpdateLine(t);
                    break;
            }

            if (elapsed >= _duration)
            {
                // Flash bright at the end, then destroy
                _material.color = _dangerColor * 2f;
                Destroy(gameObject, 0.15f);
            }
        }

        private void UpdateCircle(float t)
        {
            // Circle fills from edge inward as t progresses
            // Show an outer ring that gets thicker (filling toward center)
            float innerRadius = Mathf.Lerp(_radius, 0f, t); // starts empty, fills to full
            _mf.mesh = BuildRingMesh(innerRadius, _radius, 48);
            _material.color = Color.Lerp(_warningColor, _dangerColor, t);

            // Add outer edge line for visibility
            if (t < 0.9f)
            {
                // Pulse the edge
                float pulse = 0.5f + 0.5f * Mathf.Sin(elapsed() * 6f);
                Color c = _material.color;
                c.a = Mathf.Lerp(c.a, c.a * 1.5f, pulse);
                _material.color = c;
            }
        }

        private void UpdateRing(float t)
        {
            // Ring expands outward
            float currentRadius = Mathf.Lerp(0.2f, _radius, t);
            float thickness = Mathf.Max(0.15f, _radius * 0.1f);
            _mf.mesh = BuildRingMesh(currentRadius - thickness, currentRadius + thickness, 48);
            _material.color = Color.Lerp(_warningColor, _dangerColor, t);
        }

        private void UpdateLine(float t)
        {
            // Line pulses and intensifies
            float pulse = 0.5f + 0.5f * Mathf.Sin(elapsed() * 8f);
            Color c = Color.Lerp(_warningColor, _dangerColor, t);
            c.a = Mathf.Lerp(c.a * 0.7f, c.a, pulse);
            _material.color = c;

            // Widen slightly as it reaches full
            float currentWidth = _lineWidth * Mathf.Lerp(0.5f, 1f, t);
            _mf.mesh = BuildLineMesh(_lineLength, currentWidth);
        }

        private float elapsed() => Time.time - _startTime;

        // ──────────────────── Mesh Builders ────────────────────

        private static Mesh BuildDiscMesh(float radius, int segments)
        {
            var verts = new Vector3[segments + 2];
            var tris = new int[segments * 3];
            verts[0] = Vector3.zero;

            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                verts[i + 1] = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
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
            mesh.RecalculateNormals();
            return mesh;
        }

        private static Mesh BuildRingMesh(float inner, float outer, int segments)
        {
            inner = Mathf.Max(0f, inner);
            var verts = new Vector3[(segments + 1) * 2];
            var tris = new int[segments * 6];

            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                int vi = i * 2;
                verts[vi] = new Vector3(cos * inner, 0f, sin * inner);
                verts[vi + 1] = new Vector3(cos * outer, 0f, sin * outer);

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
            mesh.RecalculateNormals();
            return mesh;
        }

        private static Mesh BuildLineMesh(float length, float width)
        {
            float hw = width * 0.5f;
            var verts = new Vector3[]
            {
                new(-hw, 0f, 0f),
                new(hw, 0f, 0f),
                new(-hw, 0f, length),
                new(hw, 0f, length),
            };
            var tris = new int[] { 0, 2, 1, 1, 2, 3 };

            var mesh = new Mesh { name = "Line" };
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            return mesh;
        }
    }
}
