using DungeonGame.Abilities;
using DungeonGame.Combat;
using UnityEngine;

namespace DungeonGame.Enemies
{
    /// <summary>
    /// Lightweight, procedural enemy attack VFX (melee swipes, impacts) for enemies that have no animations yet.
    /// Mirrors the style used by AbilityVfxFactory.
    /// </summary>
    public static class EnemyVfxFactory
    {
        public static GameObject SpawnMeleeSwipe(Vector3 position, Vector3 forward, float radius, float lifetime)
        {
            var go = new GameObject("VFX_EnemySwipe");
            go.transform.position = position;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            go.transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);

            // Use "physical" color style (warm white) unless you want to parameterize per-enemy later.
            Color col = AbilityVfxFactory.GetColor(DamageType.Physical);

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 0.2f;
            main.startLifetime = 0.5f;
            main.startSpeed = Mathf.Max(1f, radius * 4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.35f);
            main.startColor = col;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.loop = false;
            main.maxParticles = 50;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 35) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 55f;
            shape.radius = 0.15f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(col, 0.2f),
                    new GradientColorKey(col * 0.35f, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.95f, 0f),
                    new GradientAlphaKey(0.75f, 0.25f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = gradient;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.3f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0f)));

            // Set up material on the main particle system
            var mainRenderer = go.GetComponent<ParticleSystemRenderer>();
            if (mainRenderer != null)
            {
                mainRenderer.renderMode = ParticleSystemRenderMode.Billboard;
                var mainMat = new Material(Shader.Find("Particles/Standard Unlit"));
                if (mainMat != null)
                {
                    mainMat.SetFloat("_Mode", 1); // Additive
                    mainMat.color = col;
                    mainRenderer.material = mainMat;
                }
            }

            var slashGo = new GameObject("Slash");
            slashGo.transform.SetParent(go.transform, false);
            var slashPs = slashGo.AddComponent<ParticleSystem>();
            slashPs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var slashMain = slashPs.main;
            slashMain.duration = 0.15f;
            slashMain.startLifetime = 0.35f;
            slashMain.startSpeed = Mathf.Max(1f, radius * 3f);
            slashMain.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
            slashMain.startColor = Color.white;
            slashMain.simulationSpace = ParticleSystemSimulationSpace.World;
            slashMain.loop = false;
            slashMain.maxParticles = 18;

            var slashEmission = slashPs.emission;
            slashEmission.rateOverTime = 0;
            slashEmission.SetBursts(new[] { new ParticleSystem.Burst(0f, 14) });

            var slashShape = slashPs.shape;
            slashShape.shapeType = ParticleSystemShapeType.Cone;
            slashShape.angle = 50f;
            slashShape.radius = 0.08f;

            var slashRenderer = slashGo.GetComponent<ParticleSystemRenderer>();
            if (slashRenderer != null)
            {
                slashRenderer.renderMode = ParticleSystemRenderMode.Stretch;
                slashRenderer.lengthScale = 3f;
                var mat = new Material(Shader.Find("Particles/Standard Unlit"));
                if (mat != null)
                {
                    mat.SetFloat("_Mode", 1);
                    mat.color = Color.white * 0.85f;
                    slashRenderer.material = mat;
                }
            }

            ps.Play();
            slashPs.Play();
            Object.Destroy(go, Mathf.Clamp(lifetime, 0.2f, 10f));
            return go;
        }
    }
}

