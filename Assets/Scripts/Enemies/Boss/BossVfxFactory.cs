using DungeonGame.Abilities;
using UnityEngine;

namespace DungeonGame.Enemies.Boss
{
    /// <summary>
    /// Creates procedural VFX for boss abilities.
    /// Similar to AbilityVfxFactory but tuned for boss spell visuals.
    /// </summary>
    public static class BossVfxFactory
    {
        // ──────────────────── Shadow Bolt (dark projectile trail) ────────────────────

        public static GameObject CreateShadowBoltTrail(Vector3 position, Vector3 forward)
        {
            var go = new GameObject("VFX_ShadowBolt");
            go.transform.position = position;
            go.transform.rotation = Quaternion.LookRotation(forward);

            // Core dark sphere
            var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            core.name = "Core";
            core.transform.SetParent(go.transform, false);
            core.transform.localScale = Vector3.one * 0.5f;

            var coreCol = core.GetComponent<Collider>();
            if (coreCol != null) Object.Destroy(coreCol);

            var coreRenderer = core.GetComponent<Renderer>();
            if (coreRenderer != null)
            {
                var mat = new Material(Shader.Find("Sprites/Default"));
                mat.color = new Color(0.4f, 0.1f, 0.6f, 0.9f);
                coreRenderer.material = mat;
                coreRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            // Trail particles
            var trailGo = new GameObject("Trail");
            trailGo.transform.SetParent(go.transform, false);
            var ps = trailGo.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 5f;
            main.startLifetime = 0.4f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
            main.startColor = new Color(0.5f, 0.2f, 0.8f, 0.8f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.loop = true;
            main.maxParticles = 30;

            var emission = ps.emission;
            emission.rateOverTime = 30;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(0.6f, 0.2f, 0.9f), 0f), new GradientColorKey(new Color(0.2f, 0f, 0.3f), 1f) },
                new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = gradient;

            SetupRenderer(trailGo, new Color(0.5f, 0.2f, 0.8f));

            ps.Play();
            return go;
        }

        // ──────────────────── Death Circle Explosion ────────────────────

        public static GameObject CreateDeathCircleExplosion(Vector3 position, float radius)
        {
            var go = new GameObject("VFX_DeathCircle");
            go.transform.position = position;
            Color col = new Color(0.5f, 0.1f, 0.6f);

            // Upward burst of dark particles
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
            main.startColor = col;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.loop = false;
            main.maxParticles = 60;
            main.gravityModifier = 0.5f;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 50) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(0.7f, 0.2f, 1f), 0f), new GradientColorKey(new Color(0.2f, 0f, 0.3f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = gradient;

            SetupRenderer(go, col);

            // Ground flash ring
            var ringGo = new GameObject("FlashRing");
            ringGo.transform.SetParent(go.transform, false);
            ringGo.transform.localPosition = Vector3.up * 0.03f;
            var ring = ringGo.AddComponent<ExpandingRing>();
            ring.Initialize(radius * 1.2f, 0.4f, col);

            ps.Play();
            Object.Destroy(go, 2f);
            return go;
        }

        // ──────────────────── Soul Drain Beam ────────────────────

        public static GameObject CreateSoulDrainBeam(Transform source, Transform target, float duration)
        {
            var go = new GameObject("VFX_SoulDrain");
            go.transform.position = source.position;

            // Use a LineRenderer for the beam
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 20; // Segmented for wavy effect
            lr.startWidth = 0.3f;
            lr.endWidth = 0.15f;
            lr.useWorldSpace = true;

            var mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = new Color(0.3f, 0.8f, 0.2f, 0.7f);
            lr.material = mat;

            lr.startColor = new Color(0.3f, 0.9f, 0.2f, 0.9f);
            lr.endColor = new Color(0.6f, 1f, 0.3f, 0.5f);

            // Particle wisps along the beam
            var wispGo = new GameObject("Wisps");
            wispGo.transform.SetParent(go.transform, false);
            var wispPs = wispGo.AddComponent<ParticleSystem>();
            wispPs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var wispMain = wispPs.main;
            wispMain.duration = duration;
            wispMain.startLifetime = 0.6f;
            wispMain.startSpeed = 2f;
            wispMain.startSize = 0.15f;
            wispMain.startColor = new Color(0.4f, 1f, 0.3f, 0.6f);
            wispMain.simulationSpace = ParticleSystemSimulationSpace.World;
            wispMain.loop = true;
            wispMain.maxParticles = 20;

            var wispEmission = wispPs.emission;
            wispEmission.rateOverTime = 15;

            SetupRenderer(wispGo, new Color(0.3f, 0.9f, 0.2f));
            wispPs.Play();

            // Attach beam updater
            var updater = go.AddComponent<BeamUpdater>();
            updater.Initialize(lr, source, target, duration);

            Object.Destroy(go, duration + 0.5f);
            return go;
        }

        // ──────────────────── Frost Wave ────────────────────

        public static GameObject CreateFrostWave(Vector3 position, float maxRadius, float duration)
        {
            var go = new GameObject("VFX_FrostWave");
            go.transform.position = position;
            Color col = new Color(0.4f, 0.7f, 1f);

            // Expanding ring of ice particles
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = duration;
            main.startLifetime = 0.8f;
            main.startSpeed = maxRadius / duration;
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
            main.startColor = col;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.loop = false;
            main.maxParticles = 80;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 60) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.5f;
            shape.radiusThickness = 0f; // edge only

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(col, 0.3f), new GradientColorKey(col * 0.4f, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.7f, 0.4f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = gradient;

            SetupRenderer(go, col);

            // Ground frost expanding ring
            var ringGo = new GameObject("FrostRing");
            ringGo.transform.SetParent(go.transform, false);
            ringGo.transform.localPosition = Vector3.up * 0.03f;
            var frostRing = ringGo.AddComponent<ExpandingRing>();
            frostRing.Initialize(maxRadius, duration, col);

            ps.Play();
            Object.Destroy(go, duration + 1f);
            return go;
        }

        // ──────────────────── Summon Circle (skeletons rising) ────────────────────

        public static GameObject CreateSummonCircle(Vector3 position, float radius, float duration)
        {
            var go = new GameObject("VFX_Summon");
            go.transform.position = position;
            Color col = new Color(0.4f, 0.1f, 0.5f);

            // Dark rising particles
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = duration;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
            main.startColor = col;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.loop = true;
            main.maxParticles = 30;

            var emission = ps.emission;
            emission.rateOverTime = 15;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius;
            shape.rotation = new Vector3(-90f, 0f, 0f); // upward

            SetupRenderer(go, col);

            // Ground glow
            var glowGo = new GameObject("Glow");
            glowGo.transform.SetParent(go.transform, false);
            glowGo.transform.localPosition = Vector3.up * 0.03f;
            var ring = glowGo.AddComponent<ExpandingRing>();
            ring.Initialize(radius, 0.5f, col);

            ps.Play();
            Object.Destroy(go, duration + 1f);
            return go;
        }

        // ──────────────────── Helpers ────────────────────

        private static void SetupRenderer(GameObject go, Color color)
        {
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            if (renderer == null) return;

            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            var mat = new Material(Shader.Find("Particles/Standard Unlit"));
            if (mat != null)
            {
                mat.SetFloat("_Mode", 1);
                mat.color = color;
                renderer.material = mat;
            }
        }
    }

    /// <summary>
    /// Updates a LineRenderer beam between two transforms each frame.
    /// Adds a subtle wave to the beam for visual interest.
    /// </summary>
    public class BeamUpdater : MonoBehaviour
    {
        private LineRenderer _lr;
        private Transform _source;
        private Transform _target;
        private float _duration;
        private float _startTime;

        public void Initialize(LineRenderer lr, Transform source, Transform target, float duration)
        {
            _lr = lr;
            _source = source;
            _target = target;
            _duration = duration;
            _startTime = Time.time;
        }

        private void Update()
        {
            if (_lr == null || _source == null || _target == null) return;

            Vector3 start = _source.position + Vector3.up * 1.5f;
            Vector3 end = _target.position + Vector3.up * 1f;
            int count = _lr.positionCount;
            float time = Time.time;

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / (count - 1);
                Vector3 basePos = Vector3.Lerp(start, end, t);

                // Add wave offset perpendicular to the beam
                float wave = Mathf.Sin(t * Mathf.PI * 3f + time * 6f) * 0.15f;
                Vector3 right = Vector3.Cross((end - start).normalized, Vector3.up);
                basePos += right * wave;

                _lr.SetPosition(i, basePos);
            }

            // Fade out near end
            float elapsed = Time.time - _startTime;
            if (elapsed > _duration - 0.3f)
            {
                float fade = Mathf.Clamp01((_duration - elapsed) / 0.3f);
                _lr.startColor = new Color(_lr.startColor.r, _lr.startColor.g, _lr.startColor.b, fade);
                _lr.endColor = new Color(_lr.endColor.r, _lr.endColor.g, _lr.endColor.b, fade * 0.5f);
            }
        }
    }
}
