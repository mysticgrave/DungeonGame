using DungeonGame.Combat;
using UnityEngine;

namespace DungeonGame.Abilities
{
    /// <summary>
    /// Creates runtime visual effects for abilities.
    /// Supports shape-specific VFX: arc sweeps, rising spikes, zone impacts, bursts, etc.
    /// Falls back to procedural particles when no vfxPrefab is assigned.
    /// </summary>
    public static class AbilityVfxFactory
    {
        /// <summary>
        /// Spawn a temporary VFX at the given position for the ability config.
        /// Returns the spawned GameObject (auto-destroys after lifetime).
        /// </summary>
        public static GameObject SpawnVfx(AbilityConfig config, Vector3 position, Vector3 forward)
        {
            if (config == null) return null;

            // If the ability has a custom VFX prefab, use that
            if (config.vfxPrefab != null)
            {
                var custom = Object.Instantiate(config.vfxPrefab, position, Quaternion.LookRotation(forward));
                var ps = custom.GetComponent<ParticleSystem>();
                float lifetime = ps != null ? ps.main.duration + ps.main.startLifetime.constantMax : 5f;
                Object.Destroy(custom, lifetime);
                return custom;
            }

            // Generate procedural VFX based on ability type + id for unique shapes
            return config.abilityType switch
            {
                AbilityType.MeleeAoE => CreateMeleeAoEVfx(config, position, forward),
                AbilityType.SelfBuff => CreateBuffVfx(position, config.damageType),
                AbilityType.DashStrike => CreateTrailVfx(position, forward, config.dashDistance, config.damageType),
                AbilityType.AreaZone => CreateAreaZoneVfx(config, position),
                _ => null // Projectile handles its own visual via the prefab mesh
            };
        }

        /// <summary>Spawn VFX at a specific target position (for targeted abilities).</summary>
        public static GameObject SpawnVfxAtPosition(AbilityConfig config, Vector3 targetPosition)
        {
            if (config == null) return null;

            return config.abilityType switch
            {
                AbilityType.AreaZone => CreateAreaZoneVfx(config, targetPosition),
                AbilityType.MeleeAoE => CreateMeleeAoEVfx(config, targetPosition, Vector3.forward),
                _ => SpawnVfx(config, targetPosition, Vector3.forward)
            };
        }

        // ──────────────────── MeleeAoE (shape-aware) ────────────────────

        private static GameObject CreateMeleeAoEVfx(AbilityConfig config, Vector3 position, Vector3 forward)
        {
            // Check for specific ability shapes
            string id = config.abilityId?.ToLower() ?? "";

            if (id.Contains("scythe") || id.Contains("fan") || id.Contains("sweep"))
                return CreateArcSweepVfx(position, forward, config.radius, config.damageType);

            if (id.Contains("nova") || id.Contains("slam") || id.Contains("strike"))
                return CreateRingBurstVfx(position, config.radius, config.damageType);

            // Default: standard burst
            return CreateBurstVfx(position, config.radius, config.damageType);
        }

        // ──────────────────── AreaZone (shape-aware) ────────────────────

        private static GameObject CreateAreaZoneVfx(AbilityConfig config, Vector3 position)
        {
            string id = config.abilityId?.ToLower() ?? "";

            if (id.Contains("spike"))
                return CreateIceSpikesVfx(position, config.radius, config.zoneDuration, config.damageType);

            if (id.Contains("wyrm") || id.Contains("fury"))
                return CreateFrostImpactVfx(position, config.radius, config.zoneDuration, config.damageType);

            if (id.Contains("rain") || id.Contains("arrow"))
                return CreateRainVfx(position, config.radius, config.zoneDuration, config.damageType);

            if (id.Contains("blizzard") || id.Contains("snowstorm"))
                return CreateBlizzardVfx(position, config.radius, config.zoneDuration, config.damageType);

            if (id.Contains("consecrat") || id.Contains("holy_fire") || id.Contains("sacred"))
                return CreateGroundFireVfx(position, config.radius, config.zoneDuration, config.damageType);

            // Default: standard zone particles
            return CreateZoneVfx(position, config.radius, config.zoneDuration, config.damageType);
        }

        // ──────────────────── Arc Sweep (Frost Scythe, Fan of Knives) ────────────────────

        private static GameObject CreateArcSweepVfx(Vector3 position, Vector3 forward, float radius, DamageType damageType)
        {
            var go = new GameObject("VFX_ArcSweep");
            go.transform.position = position;
            go.transform.rotation = Quaternion.LookRotation(forward);

            // Main arc particles — fan out in a 120° cone
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.2f;
            main.startLifetime = 0.5f;
            main.startSpeed = radius * 4f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
            main.startColor = GetColor(damageType);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.loop = false;
            main.maxParticles = 50;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 40) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 60f; // 120° total arc
            shape.radius = 0.2f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            var col = GetColor(damageType);
            gradient.SetKeys(
                new[] { new GradientColorKey(col, 0f), new GradientColorKey(col * 1.5f, 0.3f), new GradientColorKey(col * 0.3f, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.3f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = gradient;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.3f), new Keyframe(0.4f, 1f), new Keyframe(1f, 0f)));

            SetupRenderer(go, damageType);

            // Add a "slash" arc — a second particle system with stretched particles
            var slashGo = new GameObject("Slash");
            slashGo.transform.SetParent(go.transform, false);
            var slashPs = slashGo.AddComponent<ParticleSystem>();
            slashPs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var slashMain = slashPs.main;
            slashMain.duration = 0.15f;
            slashMain.startLifetime = 0.3f;
            slashMain.startSpeed = radius * 3f;
            slashMain.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
            slashMain.startColor = Color.white;
            slashMain.simulationSpace = ParticleSystemSimulationSpace.World;
            slashMain.loop = false;
            slashMain.maxParticles = 15;

            var slashEmission = slashPs.emission;
            slashEmission.rateOverTime = 0;
            slashEmission.SetBursts(new[] { new ParticleSystem.Burst(0f, 12) });

            var slashShape = slashPs.shape;
            slashShape.shapeType = ParticleSystemShapeType.Cone;
            slashShape.angle = 55f;
            slashShape.radius = 0.1f;

            var slashRenderer = slashGo.GetComponent<ParticleSystemRenderer>();
            if (slashRenderer != null)
            {
                slashRenderer.renderMode = ParticleSystemRenderMode.Stretch;
                slashRenderer.lengthScale = 3f;
                var mat = new Material(Shader.Find("Particles/Standard Unlit"));
                mat.SetFloat("_Mode", 1);
                mat.color = Color.white * 0.8f;
                slashRenderer.material = mat;
            }

            ps.Play();
            slashPs.Play();
            Object.Destroy(go, 1.5f);
            return go;
        }

        // ──────────────────── Ring Burst (Frost Nova, Ground Slam, Holy Strike) ────────────────────

        private static GameObject CreateRingBurstVfx(Vector3 position, float radius, DamageType damageType)
        {
            var go = new GameObject("VFX_RingBurst");
            go.transform.position = position;

            // Expanding ring of particles
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.15f;
            main.startLifetime = 0.6f;
            main.startSpeed = radius * 4f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
            main.startColor = GetColor(damageType);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.loop = false;
            main.maxParticles = 60;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 50) });

            // Circle shape (ring emission)
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.3f;
            shape.radiusThickness = 0f; // emit from edge only = ring pattern

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            var col = GetColor(damageType);
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(col, 0.2f), new GradientColorKey(col * 0.5f, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.3f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = gradient;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 1f), new Keyframe(0.5f, 0.6f), new Keyframe(1f, 0f)));

            SetupRenderer(go, damageType);

            // Ground shockwave ring — flat expanding mesh
            var ringGo = new GameObject("ShockwaveRing");
            ringGo.transform.SetParent(go.transform, false);
            ringGo.transform.localPosition = Vector3.up * 0.02f;
            var ringAnim = ringGo.AddComponent<ExpandingRing>();
            ringAnim.Initialize(radius, 0.4f, GetColor(damageType));

            ps.Play();
            Object.Destroy(go, 1.5f);
            return go;
        }

        // ──────────────────── Ice Spikes (cubes rising from ground) ────────────────────

        private static GameObject CreateIceSpikesVfx(Vector3 position, float radius, float duration, DamageType damageType)
        {
            var go = new GameObject("VFX_IceSpikes");
            go.transform.position = position;

            Color col = GetColor(damageType);

            // Spawn several spike columns
            int spikeCount = Mathf.RoundToInt(radius * 3);
            for (int i = 0; i < spikeCount; i++)
            {
                float angle = (float)i / spikeCount * Mathf.PI * 2f;
                float dist = Random.Range(0.3f, radius * 0.9f);
                Vector3 spikePos = new Vector3(
                    Mathf.Cos(angle) * dist,
                    0f,
                    Mathf.Sin(angle) * dist
                );

                var spike = GameObject.CreatePrimitive(PrimitiveType.Cube);
                spike.name = "IceSpike";
                spike.transform.SetParent(go.transform, false);

                // Remove collider
                var spikeCol = spike.GetComponent<Collider>();
                if (spikeCol != null) Object.Destroy(spikeCol);

                float height = Random.Range(0.8f, 2.2f);
                float width = Random.Range(0.15f, 0.35f);
                spike.transform.localPosition = spikePos + Vector3.down * height; // start below ground
                spike.transform.localScale = new Vector3(width, height, width);
                spike.transform.localRotation = Quaternion.Euler(
                    Random.Range(-8f, 8f), Random.Range(0f, 360f), Random.Range(-8f, 8f));

                // Semi-transparent ice material
                var renderer = spike.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var mat = new Material(Shader.Find("Sprites/Default"));
                    Color spikeColor = col;
                    spikeColor.a = Random.Range(0.5f, 0.8f);
                    mat.color = spikeColor;
                    renderer.material = mat;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }

                // Animate spike rising
                var anim = spike.AddComponent<SpikeRise>();
                float delay = Random.Range(0f, 0.15f);
                anim.Initialize(spikePos + Vector3.up * (height * 0.5f), delay, 0.25f, duration - 0.5f);
            }

            // Ground particles
            var particleGo = new GameObject("GroundParticles");
            particleGo.transform.SetParent(go.transform, false);
            var ps = particleGo.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = duration;
            main.startLifetime = 1f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.2f);
            main.startColor = col;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.loop = true;
            main.maxParticles = 40;

            var emission = ps.emission;
            emission.rateOverTime = 15;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius;

            SetupRenderer(particleGo, damageType);
            ps.Play();

            Object.Destroy(go, duration + 1f);
            return go;
        }

        // ──────────────────── Frost Impact (Frost Wyrm's Fury) ────────────────────

        private static GameObject CreateFrostImpactVfx(Vector3 position, float radius, float duration, DamageType damageType)
        {
            var go = new GameObject("VFX_FrostImpact");
            go.transform.position = position;
            Color col = GetColor(damageType);

            // Impact sphere that slams down and expands
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "ImpactSphere";
            sphere.transform.SetParent(go.transform, false);
            sphere.transform.localPosition = Vector3.up * 6f; // start high

            var sphereCol = sphere.GetComponent<Collider>();
            if (sphereCol != null) Object.Destroy(sphereCol);

            var sphereRenderer = sphere.GetComponent<Renderer>();
            if (sphereRenderer != null)
            {
                var mat = new Material(Shader.Find("Sprites/Default"));
                mat.color = new Color(col.r, col.g, col.b, 0.7f);
                sphereRenderer.material = mat;
                sphereRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            var slamAnim = sphere.AddComponent<SphereSlam>();
            slamAnim.Initialize(1.2f, 0.3f, 2f);

            // Ground frost spreading outward after impact
            var frostGo = new GameObject("GroundFrost");
            frostGo.transform.SetParent(go.transform, false);
            frostGo.transform.localPosition = Vector3.up * 0.05f;
            var frostRing = frostGo.AddComponent<ExpandingRing>();
            frostRing.Initialize(radius, 0.5f, col, 0.35f); // delayed start

            // Persistent zone particles
            var particleGo = new GameObject("ZoneParticles");
            particleGo.transform.SetParent(go.transform, false);
            var ps = particleGo.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = duration;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
            main.startColor = col;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.loop = true;
            main.maxParticles = 50;
            main.startDelay = 0.35f;

            var pEmission = ps.emission;
            pEmission.rateOverTime = 18;

            var pShape = ps.shape;
            pShape.shapeType = ParticleSystemShapeType.Circle;
            pShape.radius = radius;

            var pColor = ps.colorOverLifetime;
            pColor.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(col, 0f), new GradientColorKey(col * 0.6f, 1f) },
                new[] { new GradientAlphaKey(0.7f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            pColor.color = gradient;

            SetupRenderer(particleGo, damageType);
            ps.Play();

            Object.Destroy(go, duration + 2f);
            return go;
        }

        // ──────────────────── Rain (Arrow Rain) ────────────────────

        private static GameObject CreateRainVfx(Vector3 position, float radius, float duration, DamageType damageType)
        {
            var go = new GameObject("VFX_Rain");
            go.transform.position = position + Vector3.up * 8f;

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = duration;
            main.startLifetime = 0.6f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(12f, 18f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.15f);
            main.startColor = GetColor(damageType);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.loop = true;
            main.maxParticles = 80;
            main.gravityModifier = 0.5f;

            var emission = ps.emission;
            emission.rateOverTime = 30;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius;
            shape.rotation = new Vector3(180f, 0f, 0f); // rain downward

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.lengthScale = 4f;
                var mat = new Material(Shader.Find("Particles/Standard Unlit"));
                mat.SetFloat("_Mode", 1);
                mat.color = GetColor(damageType);
                renderer.material = mat;
            }

            // Impact splashes on ground
            var splashGo = new GameObject("Splashes");
            splashGo.transform.SetParent(go.transform, false);
            splashGo.transform.localPosition = Vector3.down * 8f + Vector3.up * 0.1f;
            var splashPs = splashGo.AddComponent<ParticleSystem>();
            splashPs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var splashMain = splashPs.main;
            splashMain.duration = duration;
            splashMain.startLifetime = 0.3f;
            splashMain.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
            splashMain.startSize = 0.2f;
            splashMain.startColor = GetColor(damageType);
            splashMain.simulationSpace = ParticleSystemSimulationSpace.World;
            splashMain.loop = true;
            splashMain.maxParticles = 30;

            var splashEmission = splashPs.emission;
            splashEmission.rateOverTime = 15;

            var splashShape = splashPs.shape;
            splashShape.shapeType = ParticleSystemShapeType.Circle;
            splashShape.radius = radius;

            SetupRenderer(splashGo, damageType);

            ps.Play();
            splashPs.Play();
            Object.Destroy(go, duration + 2f);
            return go;
        }

        // ──────────────────── Standard Burst (default MeleeAoE) ────────────────────

        private static GameObject CreateBurstVfx(Vector3 position, float radius, DamageType damageType)
        {
            var go = new GameObject("VFX_Burst");
            go.transform.position = position;

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.3f;
            main.startLifetime = 0.6f;
            main.startSpeed = radius * 3f;
            main.startSize = 0.3f;
            main.startColor = GetColor(damageType);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.loop = false;
            main.maxParticles = 40;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 30) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.3f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            var col = GetColor(damageType);
            gradient.SetKeys(
                new[] { new GradientColorKey(col, 0f), new GradientColorKey(col, 0.5f), new GradientColorKey(col * 0.5f, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = gradient;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 1f), new Keyframe(1f, 0f)));

            SetupRenderer(go, damageType);

            ps.Play();
            Object.Destroy(go, 1.5f);
            return go;
        }

        // ──────────────────── Buff (SelfBuff) ────────────────────

        private static GameObject CreateBuffVfx(Vector3 position, DamageType damageType)
        {
            var go = new GameObject("VFX_Buff");
            go.transform.position = position;

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 1f;
            main.startLifetime = 0.8f;
            main.startSpeed = 2f;
            main.startSize = 0.15f;
            main.startColor = GetColor(damageType);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.loop = false;
            main.maxParticles = 30;

            var emission = ps.emission;
            emission.rateOverTime = 30;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 5f;
            shape.radius = 0.5f;
            shape.rotation = new Vector3(-90f, 0f, 0f); // Point upward

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            var col = GetColor(damageType);
            gradient.SetKeys(
                new[] { new GradientColorKey(col, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = gradient;

            SetupRenderer(go, damageType);

            ps.Play();
            Object.Destroy(go, 2f);
            return go;
        }

        // ──────────────────── Trail (DashStrike) ────────────────────

        private static GameObject CreateTrailVfx(Vector3 position, Vector3 forward, float distance, DamageType damageType)
        {
            var go = new GameObject("VFX_Trail");
            go.transform.position = position;

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.1f;
            main.startLifetime = 0.5f;
            main.startSpeed = distance * 2f;
            main.startSize = 0.25f;
            main.startColor = GetColor(damageType);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.loop = false;
            main.maxParticles = 20;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 15) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 10f;
            shape.radius = 0.3f;
            shape.rotation = Quaternion.LookRotation(forward).eulerAngles;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.5f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0f)));

            SetupRenderer(go, damageType);

            ps.Play();
            Object.Destroy(go, 1.5f);
            return go;
        }

        // ──────────────────── Blizzard (swirling snow + ice chunks falling) ────────────────────

        private static GameObject CreateBlizzardVfx(Vector3 position, float radius, float duration, DamageType damageType)
        {
            var go = new GameObject("VFX_Blizzard");
            go.transform.position = position;
            Color col = GetColor(damageType);

            // Swirling snow particles (horizontal rotation)
            var snowGo = new GameObject("Snow");
            snowGo.transform.SetParent(go.transform, false);
            snowGo.transform.localPosition = Vector3.up * 3f;
            var snowPs = snowGo.AddComponent<ParticleSystem>();
            snowPs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var snowMain = snowPs.main;
            snowMain.duration = duration;
            snowMain.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 3f);
            snowMain.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
            snowMain.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
            snowMain.startColor = new Color(col.r, col.g, col.b, 0.9f);
            snowMain.simulationSpace = ParticleSystemSimulationSpace.World;
            snowMain.loop = true;
            snowMain.maxParticles = 100;
            snowMain.gravityModifier = 0.3f;

            var snowEmission = snowPs.emission;
            snowEmission.rateOverTime = 40;

            var snowShape = snowPs.shape;
            snowShape.shapeType = ParticleSystemShapeType.Circle;
            snowShape.radius = radius;
            snowShape.rotation = new Vector3(180f, 0f, 0f);

            var snowColor = snowPs.colorOverLifetime;
            snowColor.enabled = true;
            var snowGrad = new Gradient();
            snowGrad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(col, 0.5f), new GradientColorKey(col * 0.5f, 1f) },
                new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0.6f, 0.5f), new GradientAlphaKey(0f, 1f) }
            );
            snowColor.color = snowGrad;

            SetupRenderer(snowGo, damageType);

            // Ground frost ring
            var frostGo = new GameObject("GroundFrost");
            frostGo.transform.SetParent(go.transform, false);
            frostGo.transform.localPosition = Vector3.up * 0.03f;
            var frostRing = frostGo.AddComponent<ExpandingRing>();
            frostRing.Initialize(radius, 0.6f, col);

            // Rising ice mist at ground level
            var mistGo = new GameObject("Mist");
            mistGo.transform.SetParent(go.transform, false);
            var mistPs = mistGo.AddComponent<ParticleSystem>();
            mistPs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var mistMain = mistPs.main;
            mistMain.duration = duration;
            mistMain.startLifetime = new ParticleSystem.MinMaxCurve(2f, 4f);
            mistMain.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
            mistMain.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
            mistMain.startColor = new Color(col.r, col.g, col.b, 0.3f);
            mistMain.simulationSpace = ParticleSystemSimulationSpace.World;
            mistMain.loop = true;
            mistMain.maxParticles = 25;

            var mistEmission = mistPs.emission;
            mistEmission.rateOverTime = 8;

            var mistShape = mistPs.shape;
            mistShape.shapeType = ParticleSystemShapeType.Circle;
            mistShape.radius = radius * 0.8f;

            var mistSize = mistPs.sizeOverLifetime;
            mistSize.enabled = true;
            mistSize.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.5f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0.3f)));

            SetupRenderer(mistGo, damageType);

            snowPs.Play();
            mistPs.Play();
            Object.Destroy(go, duration + 2f);
            return go;
        }

        // ──────────────────── Ground Fire (Consecration, Holy Fire) ────────────────────

        private static GameObject CreateGroundFireVfx(Vector3 position, float radius, float duration, DamageType damageType)
        {
            var go = new GameObject("VFX_GroundFire");
            go.transform.position = position;
            Color col = GetColor(damageType);

            // Rising flame particles
            var flameGo = new GameObject("Flames");
            flameGo.transform.SetParent(go.transform, false);
            var flamePs = flameGo.AddComponent<ParticleSystem>();
            flamePs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var flameMain = flamePs.main;
            flameMain.duration = duration;
            flameMain.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
            flameMain.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
            flameMain.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
            flameMain.startColor = col;
            flameMain.simulationSpace = ParticleSystemSimulationSpace.World;
            flameMain.loop = true;
            flameMain.maxParticles = 60;

            var flameEmission = flamePs.emission;
            flameEmission.rateOverTime = 25;

            var flameShape = flamePs.shape;
            flameShape.shapeType = ParticleSystemShapeType.Circle;
            flameShape.radius = radius;

            var flameColor = flamePs.colorOverLifetime;
            flameColor.enabled = true;
            var flameGrad = new Gradient();
            flameGrad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(col, 0.2f), new GradientColorKey(col * 0.4f, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.3f), new GradientAlphaKey(0f, 1f) }
            );
            flameColor.color = flameGrad;

            var flameSize = flamePs.sizeOverLifetime;
            flameSize.enabled = true;
            flameSize.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.3f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0f)));

            SetupRenderer(flameGo, damageType);

            // Ground glow ring
            var glowGo = new GameObject("GroundGlow");
            glowGo.transform.SetParent(go.transform, false);
            glowGo.transform.localPosition = Vector3.up * 0.03f;
            var glowRing = glowGo.AddComponent<ExpandingRing>();
            glowRing.Initialize(radius, 0.4f, col);

            // Ember particles rising
            var emberGo = new GameObject("Embers");
            emberGo.transform.SetParent(go.transform, false);
            var emberPs = emberGo.AddComponent<ParticleSystem>();
            emberPs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var emberMain = emberPs.main;
            emberMain.duration = duration;
            emberMain.startLifetime = new ParticleSystem.MinMaxCurve(1f, 2f);
            emberMain.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
            emberMain.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            emberMain.startColor = new Color(1f, 0.8f, 0.2f);
            emberMain.simulationSpace = ParticleSystemSimulationSpace.World;
            emberMain.loop = true;
            emberMain.maxParticles = 30;

            var emberEmission = emberPs.emission;
            emberEmission.rateOverTime = 12;

            var emberShape = emberPs.shape;
            emberShape.shapeType = ParticleSystemShapeType.Circle;
            emberShape.radius = radius;
            emberShape.rotation = new Vector3(-90f, 0f, 0f); // upward

            SetupRenderer(emberGo, damageType);

            flamePs.Play();
            emberPs.Play();
            Object.Destroy(go, duration + 2f);
            return go;
        }

        // ──────────────────── Standard Zone (AreaZone default) ────────────────────

        private static GameObject CreateZoneVfx(Vector3 position, float radius, float duration, DamageType damageType)
        {
            var go = new GameObject("VFX_Zone");
            go.transform.position = position + Vector3.up * 0.1f;

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = duration;
            main.startLifetime = 1.5f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
            main.startColor = GetColor(damageType);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.loop = true;
            main.maxParticles = 60;

            var emission = ps.emission;
            emission.rateOverTime = 20;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            var col = GetColor(damageType);
            gradient.SetKeys(
                new[] { new GradientColorKey(col, 0f), new GradientColorKey(col * 0.7f, 1f) },
                new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = gradient;

            SetupRenderer(go, damageType);

            ps.Play();
            Object.Destroy(go, duration + 2f);
            return go;
        }

        // ──────────────────── Helpers ────────────────────

        public static Color GetColor(DamageType damageType)
        {
            return damageType switch
            {
                DamageType.Fire => new Color(1f, 0.4f, 0.1f),
                DamageType.Ice => new Color(0.4f, 0.7f, 1f),
                DamageType.Lightning => new Color(0.8f, 0.8f, 1f),
                DamageType.Poison => new Color(0.3f, 0.9f, 0.2f),
                _ => new Color(1f, 0.9f, 0.7f) // Physical = warm white
            };
        }

        private static void SetupRenderer(GameObject go, DamageType damageType)
        {
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            if (renderer == null) return;

            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            // Use the default particle material
            var mat = new Material(Shader.Find("Particles/Standard Unlit"));
            if (mat != null)
            {
                mat.SetFloat("_Mode", 1); // Additive
                mat.color = GetColor(damageType);
                renderer.material = mat;
            }
        }
    }

    // ──────────────────── Animation Components ────────────────────

    /// <summary>
    /// Animates a ring expanding outward on the ground. Attached to VFX objects.
    /// </summary>
    public class ExpandingRing : MonoBehaviour
    {
        private float _targetRadius;
        private float _duration;
        private float _startTime;
        private float _delay;
        private MeshFilter _mf;
        private MeshRenderer _mr;
        private Color _color;
        private bool _started;

        public void Initialize(float targetRadius, float duration, Color color, float delay = 0f)
        {
            _targetRadius = targetRadius;
            _duration = duration;
            _color = color;
            _delay = delay;
            _startTime = Time.time;

            _mf = gameObject.AddComponent<MeshFilter>();
            _mr = gameObject.AddComponent<MeshRenderer>();

            var mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = color;
            _mr.material = mat;
            _mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _mr.receiveShadows = false;

            if (delay > 0f)
                _mr.enabled = false;
        }

        private void Update()
        {
            float elapsed = Time.time - _startTime;
            if (elapsed < _delay)
                return;

            if (!_started)
            {
                _started = true;
                _mr.enabled = true;
            }

            float t = Mathf.Clamp01((elapsed - _delay) / _duration);
            float currentRadius = Mathf.Lerp(0.1f, _targetRadius, t);
            float thickness = Mathf.Max(0.1f, _targetRadius * 0.08f);

            // Rebuild ring mesh at current size
            _mf.mesh = BuildSimpleRing(currentRadius - thickness, currentRadius, 32);

            // Fade out
            Color c = _color;
            c.a = _color.a * (1f - t * 0.7f);
            _mr.material.color = c;
        }

        private static Mesh BuildSimpleRing(float inner, float outer, int segments)
        {
            inner = Mathf.Max(0.01f, inner);
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

            var mesh = new Mesh { name = "ExpandRing" };
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            return mesh;
        }
    }

    /// <summary>
    /// Animates an ice spike rising from below ground to its target position.
    /// </summary>
    public class SpikeRise : MonoBehaviour
    {
        private Vector3 _targetLocalPos;
        private Vector3 _startLocalPos;
        private float _delay;
        private float _riseDuration;
        private float _holdDuration;
        private float _startTime;
        private enum Phase { Waiting, Rising, Holding, Sinking }
        private Phase _phase = Phase.Waiting;

        public void Initialize(Vector3 targetLocalPos, float delay, float riseDuration, float holdDuration)
        {
            _targetLocalPos = targetLocalPos;
            _startLocalPos = transform.localPosition;
            _delay = delay;
            _riseDuration = riseDuration;
            _holdDuration = holdDuration;
            _startTime = Time.time;
        }

        private void Update()
        {
            float elapsed = Time.time - _startTime;

            switch (_phase)
            {
                case Phase.Waiting:
                    if (elapsed >= _delay)
                    {
                        _phase = Phase.Rising;
                        _startTime = Time.time;
                    }
                    break;

                case Phase.Rising:
                    elapsed = Time.time - _startTime;
                    float riseT = Mathf.Clamp01(elapsed / _riseDuration);
                    // Ease out for punchy feel
                    riseT = 1f - (1f - riseT) * (1f - riseT);
                    transform.localPosition = Vector3.Lerp(_startLocalPos, _targetLocalPos, riseT);
                    if (riseT >= 1f)
                    {
                        _phase = Phase.Holding;
                        _startTime = Time.time;
                    }
                    break;

                case Phase.Holding:
                    elapsed = Time.time - _startTime;
                    if (elapsed >= _holdDuration)
                    {
                        _phase = Phase.Sinking;
                        _startTime = Time.time;
                    }
                    break;

                case Phase.Sinking:
                    elapsed = Time.time - _startTime;
                    float sinkT = Mathf.Clamp01(elapsed / 0.4f);
                    transform.localPosition = Vector3.Lerp(_targetLocalPos, _startLocalPos, sinkT);

                    // Fade out
                    var renderer = GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        Color c = renderer.material.color;
                        c.a = Mathf.Lerp(0.7f, 0f, sinkT);
                        renderer.material.color = c;
                    }
                    break;
            }
        }
    }

    /// <summary>
    /// Animates a sphere slamming down from above, scaling on impact.
    /// </summary>
    public class SphereSlam : MonoBehaviour
    {
        private float _startScale;
        private float _slamDuration;
        private float _impactScale;
        private float _startTime;
        private Vector3 _startPos;
        private bool _impacted;

        public void Initialize(float startScale, float slamDuration, float impactScale)
        {
            _startScale = startScale;
            _slamDuration = slamDuration;
            _impactScale = impactScale;
            _startTime = Time.time;
            _startPos = transform.localPosition;
            transform.localScale = Vector3.one * startScale;
        }

        private void Update()
        {
            float elapsed = Time.time - _startTime;

            if (!_impacted)
            {
                float t = Mathf.Clamp01(elapsed / _slamDuration);
                // Accelerating fall
                float fallT = t * t;
                transform.localPosition = Vector3.Lerp(_startPos, Vector3.up * 0.5f, fallT);
                transform.localScale = Vector3.one * Mathf.Lerp(_startScale, _startScale * 0.8f, t);

                if (t >= 1f)
                {
                    _impacted = true;
                    _startTime = Time.time;
                }
            }
            else
            {
                elapsed = Time.time - _startTime;
                float t = Mathf.Clamp01(elapsed / 0.3f);
                // Expand and fade on impact
                float scale = Mathf.Lerp(_startScale * 0.8f, _impactScale, t);
                transform.localScale = Vector3.one * scale;

                var renderer = GetComponent<Renderer>();
                if (renderer != null)
                {
                    Color c = renderer.material.color;
                    c.a = Mathf.Lerp(0.7f, 0f, t);
                    renderer.material.color = c;
                }

                if (t >= 1f)
                    Destroy(gameObject);
            }
        }
    }
}
