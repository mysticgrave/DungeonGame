using System.Collections;
using DungeonGame.Combat;
using DungeonGame.Player;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace DungeonGame.Enemies.Boss
{
    /// <summary>
    /// Lich boss AI with multi-phase magic attacks.
    /// Server-authoritative: runs attack logic on server, sends VFX/indicators via ClientRpc.
    ///
    /// Phase 1 (100-60% HP): Shadow Bolt, Death Circle
    /// Phase 2 (60-30% HP):  + Soul Drain, Summon Skeletons
    /// Phase 3 (30-0% HP):   + Frost Wave, faster cooldowns
    ///
    /// Attach to a boss prefab with: NetworkObject, NavMeshAgent, EnemyStateMachine, NetworkHealth,
    /// StatusEffectController, Animator.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(EnemyStateMachine))]
    public class LichBossController : NetworkBehaviour
    {
        [Header("Config")]
        [SerializeField] private string bossName = "Malachar the Undying";
        [SerializeField] private float aggroRange = 30f;
        [SerializeField] private float preferredDistance = 10f;
        [SerializeField] private float moveSpeed = 3.5f;

        [Header("Shadow Bolt")]
        [SerializeField] private float shadowBoltCooldown = 3f;
        [SerializeField] private int shadowBoltDamage = 5;
        [SerializeField] private float shadowBoltSpeed = 12f;
        [SerializeField] private float shadowBoltWindUp = 0.6f;

        [Header("Death Circle")]
        [SerializeField] private float deathCircleCooldown = 8f;
        [SerializeField] private int deathCircleDamage = 12;
        [SerializeField] private float deathCircleRadius = 4f;
        [SerializeField] private float deathCircleDelay = 2f; // telegraph time before explosion

        [Header("Soul Drain")]
        [SerializeField] private float soulDrainCooldown = 15f;
        [SerializeField] private int soulDrainDamagePerTick = 2;
        [SerializeField] private float soulDrainDuration = 4f;
        [SerializeField] private float soulDrainRange = 15f;
        [SerializeField] private int soulDrainHealPerTick = 3;
        [SerializeField] private float soulDrainWindUp = 1.2f;

        [Header("Summon")]
        [SerializeField] private float summonCooldown = 20f;
        [SerializeField] private int summonCount = 3;
        [SerializeField] private float summonRadius = 5f;
        [SerializeField] private float summonDelay = 1.5f;
        [Tooltip("Skeleton enemy config to spawn. Assign in inspector.")]
        [SerializeField] private EnemyConfig skeletonConfig;
        [Tooltip("Skeleton prefab with NetworkObject. Required for summon.")]
        [SerializeField] private GameObject skeletonPrefab;

        [Header("Frost Wave")]
        [SerializeField] private float frostWaveCooldown = 12f;
        [SerializeField] private int frostWaveDamage = 8;
        [SerializeField] private float frostWaveMaxRadius = 12f;
        [SerializeField] private float frostWaveExpandTime = 1.5f;
        [SerializeField] private float frostWaveWindUp = 1f;

        [Header("Phase Thresholds (% of max HP)")]
        [SerializeField, Range(0f, 1f)] private float phase2Threshold = 0.6f;
        [SerializeField, Range(0f, 1f)] private float phase3Threshold = 0.3f;
        [SerializeField] private float phase3CooldownMultiplier = 0.6f;

        // Runtime
        private NavMeshAgent _agent;
        private EnemyStateMachine _sm;
        private NetworkHealth _health;
        private StatusEffectController _statusFx;
        private Animator _animator;
        private Transform _target;
        private int _phase = 1;
        private bool _fightStarted;

        // Cooldown tracking
        private float _nextShadowBolt;
        private float _nextDeathCircle;
        private float _nextSoulDrain;
        private float _nextSummon;
        private float _nextFrostWave;
        private bool _isCasting;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            _agent = GetComponent<NavMeshAgent>();
            _sm = GetComponent<EnemyStateMachine>();
            _health = GetComponent<NetworkHealth>();
            _statusFx = GetComponent<StatusEffectController>();
            _animator = GetComponentInChildren<Animator>(true);

            if (!IsServer)
            {
                enabled = false;
                return;
            }

            _agent.speed = moveSpeed;
            _agent.stoppingDistance = preferredDistance;

            if (_health != null)
            {
                _health.OnDied += OnBossDied;
                _health.OnDamaged += OnBossDamaged;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (_health != null)
            {
                _health.OnDied -= OnBossDied;
                _health.OnDamaged -= OnBossDamaged;
            }
            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (!IsServer) return;
            if (_sm.IsDead) return;
            if (_isCasting) return;
            if (_sm.IsMovementDisabled) return;

            float speedMult = _statusFx != null ? _statusFx.SpeedMultiplier : 1f;
            if (speedMult <= 0f)
            {
                if (_agent.enabled) _agent.isStopped = true;
                return;
            }

            _target = FindNearestPlayer();
            if (_target == null)
            {
                if (_agent.enabled && _agent.hasPath) _agent.ResetPath();
                return;
            }

            if (!_fightStarted)
            {
                _fightStarted = true;
                StartBossBarClientRpc(bossName);
            }

            float dist = Vector3.Distance(transform.position, _target.position);

            // Update phase based on HP
            UpdatePhase();

            // Try attacks in priority order
            if (TryAttack(dist)) return;

            // Movement
            FaceTarget();
            if (dist > preferredDistance)
            {
                if (_sm.Current != EnemyState.Chase)
                    _sm.TransitionTo(EnemyState.Chase);
                if (_agent.enabled && _agent.isOnNavMesh)
                {
                    _agent.speed = moveSpeed * speedMult;
                    _agent.isStopped = false;
                    _agent.SetDestination(_target.position);
                }
            }
            else
            {
                if (_agent.enabled) _agent.isStopped = true;
            }
        }

        // ──────────────────── Phase Management ────────────────────

        private void UpdatePhase()
        {
            if (_health == null) return;
            float hpRatio = (float)_health.Hp / _health.MaxHp;

            int newPhase;
            if (hpRatio <= phase3Threshold) newPhase = 3;
            else if (hpRatio <= phase2Threshold) newPhase = 2;
            else newPhase = 1;

            if (newPhase != _phase)
            {
                _phase = newPhase;
                Debug.Log($"[LichBoss] Entering Phase {_phase} ({hpRatio * 100:F0}% HP)");
                OnPhaseChangeClientRpc(_phase);
            }
        }

        private float CooldownScale => _phase >= 3 ? phase3CooldownMultiplier : 1f;

        // ──────────────────── Attack Selection ────────────────────

        private bool TryAttack(float dist)
        {
            float t = Time.time;

            // Phase 3: Frost Wave (highest priority — forces dodging)
            if (_phase >= 3 && t >= _nextFrostWave && dist < frostWaveMaxRadius + 5f)
            {
                StartCoroutine(CastFrostWave());
                return true;
            }

            // Phase 2+: Soul Drain (if in range and off cooldown)
            if (_phase >= 2 && t >= _nextSoulDrain && dist <= soulDrainRange)
            {
                StartCoroutine(CastSoulDrain());
                return true;
            }

            // Phase 2+: Summon Skeletons
            if (_phase >= 2 && t >= _nextSummon && skeletonPrefab != null)
            {
                StartCoroutine(CastSummon());
                return true;
            }

            // All phases: Death Circle (on a player)
            if (t >= _nextDeathCircle && _target != null)
            {
                StartCoroutine(CastDeathCircle());
                return true;
            }

            // All phases: Shadow Bolt (basic ranged attack)
            if (t >= _nextShadowBolt && dist <= aggroRange)
            {
                StartCoroutine(CastShadowBolt());
                return true;
            }

            return false;
        }

        // ──────────────────── Shadow Bolt ────────────────────

        private IEnumerator CastShadowBolt()
        {
            _isCasting = true;
            _nextShadowBolt = Time.time + shadowBoltCooldown * CooldownScale;
            if (_agent.enabled) _agent.isStopped = true;
            _sm.TransitionTo(EnemyState.WindUp, shadowBoltWindUp);

            FaceTarget();

            yield return new WaitForSeconds(shadowBoltWindUp);
            if (_sm.IsDead) { _isCasting = false; yield break; }

            _sm.TransitionTo(EnemyState.Attack, 0.3f);

            // Fire the bolt
            if (_target != null)
            {
                Vector3 spawnPos = transform.position + Vector3.up * 2f + transform.forward * 1.5f;
                Vector3 dir = (_target.position + Vector3.up * 1f - spawnPos).normalized;

                FireShadowBoltClientRpc(spawnPos, dir);
                ServerFireShadowBolt(spawnPos, dir);
            }

            yield return new WaitForSeconds(0.5f);
            _isCasting = false;
        }

        private void ServerFireShadowBolt(Vector3 spawnPos, Vector3 dir)
        {
            // Simple hitscan with travel time simulation
            // Actually use a physics sphere cast for the bolt
            if (Physics.SphereCast(spawnPos, 0.3f, dir, out RaycastHit hit, aggroRange,
                    ~0, QueryTriggerInteraction.Ignore))
            {
                var playerHealth = hit.collider.GetComponentInParent<PlayerHealth>();
                if (playerHealth != null)
                {
                    var info = new DamageInfo(shadowBoltDamage)
                    {
                        Type = DamageType.Lightning,
                        HitPosition = hit.point,
                        KnockImpulse = dir * 3f + Vector3.up * 1f
                    };
                    playerHealth.TakeDamage(info);
                }
            }
        }

        [Rpc(SendTo.Everyone)]
        private void FireShadowBoltClientRpc(Vector3 spawnPos, Vector3 dir)
        {
            var vfx = BossVfxFactory.CreateShadowBoltTrail(spawnPos, dir);
            if (vfx != null)
            {
                // Move the bolt forward over time
                var mover = vfx.AddComponent<ProjectileMover>();
                mover.Initialize(dir, shadowBoltSpeed, aggroRange / shadowBoltSpeed);
            }
        }

        // ──────────────────── Death Circle ────────────────────

        private IEnumerator CastDeathCircle()
        {
            _isCasting = true;
            _nextDeathCircle = Time.time + deathCircleCooldown * CooldownScale;

            // Target a player's position
            Vector3 targetPos = _target != null ? _target.position : transform.position + transform.forward * 5f;

            // Show indicator on all clients
            ShowDeathCircleClientRpc(targetPos, deathCircleRadius, deathCircleDelay);

            // Wind-up animation
            _sm.TransitionTo(EnemyState.WindUp, deathCircleDelay);
            FaceTarget();

            yield return new WaitForSeconds(deathCircleDelay);
            if (_sm.IsDead) { _isCasting = false; yield break; }

            // Explode — damage everyone in radius
            _sm.TransitionTo(EnemyState.Attack, 0.3f);
            DeathCircleExplodeClientRpc(targetPos, deathCircleRadius);

            var hits = Physics.OverlapSphere(targetPos, deathCircleRadius, ~0, QueryTriggerInteraction.Ignore);
            var processed = new System.Collections.Generic.HashSet<ulong>();
            foreach (var col in hits)
            {
                var no = col.GetComponentInParent<NetworkObject>();
                if (no == null || !no.IsPlayerObject) continue;
                if (!processed.Add(no.NetworkObjectId)) continue;

                var ph = col.GetComponentInParent<PlayerHealth>();
                if (ph != null)
                {
                    Vector3 knockDir = (col.transform.position - targetPos).normalized;
                    var info = new DamageInfo(deathCircleDamage)
                    {
                        Type = DamageType.Poison,
                        HitPosition = col.transform.position,
                        KnockImpulse = knockDir * 6f + Vector3.up * 3f
                    };
                    ph.TakeDamage(info);
                }
            }

            yield return new WaitForSeconds(0.5f);
            _isCasting = false;
        }

        [Rpc(SendTo.Everyone)]
        private void ShowDeathCircleClientRpc(Vector3 position, float radius, float delay)
        {
            BossAttackIndicator.CreateCircle(position, radius, delay, new Color(0.5f, 0.1f, 0.6f));
        }

        [Rpc(SendTo.Everyone)]
        private void DeathCircleExplodeClientRpc(Vector3 position, float radius)
        {
            BossVfxFactory.CreateDeathCircleExplosion(position, radius);
        }

        // ──────────────────── Soul Drain ────────────────────

        private IEnumerator CastSoulDrain()
        {
            _isCasting = true;
            _nextSoulDrain = Time.time + soulDrainCooldown * CooldownScale;

            if (_target == null) { _isCasting = false; yield break; }

            // Wind-up with line indicator
            Vector3 targetPos = _target.position;
            ShowSoulDrainIndicatorClientRpc(transform.position, targetPos, soulDrainWindUp);
            _sm.TransitionTo(EnemyState.WindUp, soulDrainWindUp);

            yield return new WaitForSeconds(soulDrainWindUp);
            if (_sm.IsDead || _target == null) { _isCasting = false; yield break; }

            // Start channel
            _sm.TransitionTo(EnemyState.Attack, soulDrainDuration);
            ulong targetId = _target.GetComponent<NetworkObject>()?.NetworkObjectId ?? 0;
            StartSoulDrainVfxClientRpc(targetId, soulDrainDuration);

            float elapsed = 0f;
            float tickInterval = 0.5f;
            float nextTick = 0f;

            while (elapsed < soulDrainDuration && !_sm.IsDead && _target != null)
            {
                FaceTarget();
                if (_agent.enabled) _agent.isStopped = true;

                float dist = Vector3.Distance(transform.position, _target.position);
                if (dist > soulDrainRange * 1.3f) break; // Target escaped range

                if (elapsed >= nextTick)
                {
                    nextTick += tickInterval;

                    // Damage target
                    var ph = _target.GetComponent<PlayerHealth>();
                    if (ph != null)
                    {
                        var info = new DamageInfo(soulDrainDamagePerTick)
                        {
                            Type = DamageType.Poison,
                            HitPosition = _target.position
                        };
                        ph.TakeDamage(info);
                    }

                    // Heal self
                    if (_health != null && _health.Hp < _health.MaxHp)
                    {
                        // NetworkHealth doesn't have Heal — just reduce damage by healing
                        // We'll directly modify HP via a heal method if available
                        HealSelf(soulDrainHealPerTick);
                    }
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            yield return new WaitForSeconds(0.3f);
            _isCasting = false;
        }

        [Rpc(SendTo.Everyone)]
        private void ShowSoulDrainIndicatorClientRpc(Vector3 start, Vector3 end, float duration)
        {
            BossAttackIndicator.CreateLine(start, end, 1f, duration, new Color(0.3f, 0.9f, 0.2f));
        }

        [Rpc(SendTo.Everyone)]
        private void StartSoulDrainVfxClientRpc(ulong targetNetId, float duration)
        {
            // Find the target NetworkObject on this client
            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetNetId, out var targetNo))
                return;
            BossVfxFactory.CreateSoulDrainBeam(transform, targetNo.transform, duration);
        }

        // ──────────────────── Summon Skeletons ────────────────────

        private IEnumerator CastSummon()
        {
            _isCasting = true;
            _nextSummon = Time.time + summonCooldown * CooldownScale;

            // Show summon circles
            Vector3[] summonPositions = new Vector3[summonCount];
            for (int i = 0; i < summonCount; i++)
            {
                float angle = (float)i / summonCount * Mathf.PI * 2f + Random.Range(0f, 0.5f);
                float dist = Random.Range(summonRadius * 0.5f, summonRadius);
                summonPositions[i] = transform.position + new Vector3(
                    Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);

                ShowSummonCircleClientRpc(summonPositions[i], 1.5f, summonDelay);
            }

            _sm.TransitionTo(EnemyState.WindUp, summonDelay);

            yield return new WaitForSeconds(summonDelay);
            if (_sm.IsDead) { _isCasting = false; yield break; }

            _sm.TransitionTo(EnemyState.Attack, 0.5f);

            // Spawn skeletons
            for (int i = 0; i < summonCount; i++)
            {
                if (skeletonPrefab == null) continue;

                var pos = summonPositions[i];
                // Snap to NavMesh
                if (NavMesh.SamplePosition(pos, out NavMeshHit navHit, 5f, NavMesh.AllAreas))
                    pos = navHit.position;

                var go = Instantiate(skeletonPrefab, pos, Quaternion.identity);
                var no = go.GetComponent<NetworkObject>();
                if (no != null) no.Spawn(true);

                var ai = go.GetComponent<EnemyAI>();
                if (ai != null && skeletonConfig != null) ai.Init(skeletonConfig);
            }

            yield return new WaitForSeconds(0.5f);
            _isCasting = false;
        }

        [Rpc(SendTo.Everyone)]
        private void ShowSummonCircleClientRpc(Vector3 position, float radius, float delay)
        {
            BossAttackIndicator.CreateCircle(position, radius, delay, new Color(0.4f, 0.1f, 0.5f));
            BossVfxFactory.CreateSummonCircle(position, radius, delay);
        }

        // ──────────────────── Frost Wave ────────────────────

        private IEnumerator CastFrostWave()
        {
            _isCasting = true;
            _nextFrostWave = Time.time + frostWaveCooldown * CooldownScale;

            // Show expanding ring indicator
            ShowFrostWaveIndicatorClientRpc(transform.position, frostWaveMaxRadius, frostWaveWindUp);
            _sm.TransitionTo(EnemyState.WindUp, frostWaveWindUp);

            yield return new WaitForSeconds(frostWaveWindUp);
            if (_sm.IsDead) { _isCasting = false; yield break; }

            _sm.TransitionTo(EnemyState.Attack, frostWaveExpandTime);
            SpawnFrostWaveVfxClientRpc(transform.position, frostWaveMaxRadius, frostWaveExpandTime);

            // Damage in expanding ring over time
            float elapsed = 0f;
            float tickInterval = 0.15f;
            float nextTick = 0f;
            var damaged = new System.Collections.Generic.HashSet<ulong>();

            while (elapsed < frostWaveExpandTime && !_sm.IsDead)
            {
                if (elapsed >= nextTick)
                {
                    nextTick += tickInterval;
                    float currentRadius = Mathf.Lerp(0.5f, frostWaveMaxRadius, elapsed / frostWaveExpandTime);
                    float innerRadius = Mathf.Max(0f, currentRadius - 2f);

                    var hits = Physics.OverlapSphere(transform.position, currentRadius, ~0, QueryTriggerInteraction.Ignore);
                    foreach (var col in hits)
                    {
                        var no = col.GetComponentInParent<NetworkObject>();
                        if (no == null || !no.IsPlayerObject) continue;
                        if (!damaged.Add(no.NetworkObjectId)) continue;

                        // Check they're in the ring band (not too close to center)
                        float dist = Vector3.Distance(transform.position, col.transform.position);
                        if (dist < innerRadius) continue; // Inside the ring — safe

                        var ph = col.GetComponentInParent<PlayerHealth>();
                        if (ph != null)
                        {
                            Vector3 knockDir = (col.transform.position - transform.position).normalized;
                            var info = new DamageInfo(frostWaveDamage)
                            {
                                Type = DamageType.Ice,
                                HitPosition = col.transform.position,
                                KnockImpulse = knockDir * 4f + Vector3.up * 2f
                            };
                            ph.TakeDamage(info);

                            // Apply freeze
                            var sec = col.GetComponentInParent<StatusEffectController>();
                            if (sec != null)
                                sec.ApplyEffect(StatusEffectType.Freeze, 1.5f, knockDir * 3f);
                        }
                    }
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            yield return new WaitForSeconds(0.5f);
            _isCasting = false;
        }

        [Rpc(SendTo.Everyone)]
        private void ShowFrostWaveIndicatorClientRpc(Vector3 position, float maxRadius, float windUp)
        {
            BossAttackIndicator.CreateRing(position, maxRadius, windUp, new Color(0.3f, 0.6f, 1f));
        }

        [Rpc(SendTo.Everyone)]
        private void SpawnFrostWaveVfxClientRpc(Vector3 position, float maxRadius, float duration)
        {
            BossVfxFactory.CreateFrostWave(position, maxRadius, duration);
        }

        // ──────────────────── Boss Bar ────────────────────

        [Rpc(SendTo.Everyone)]
        private void StartBossBarClientRpc(string name)
        {
            var bar = FindAnyObjectByType<BossHealthBarUI>();
            if (bar == null)
            {
                var go = new GameObject("BossHealthBarUI");
                bar = go.AddComponent<BossHealthBarUI>();
            }
            bar.ShowBoss(_health, name);
        }

        [Rpc(SendTo.Everyone)]
        private void OnPhaseChangeClientRpc(int phase)
        {
            Debug.Log($"[LichBoss] Phase {phase}!");
            // Could add phase transition VFX here
        }

        // ──────────────────── Helpers ────────────────────

        private void OnBossDamaged(DamageInfo info)
        {
            // Could trigger counter-attacks on damage
        }

        private void OnBossDied()
        {
            _isCasting = false;
            StopAllCoroutines();
            _sm.TransitionTo(EnemyState.Dead);
            HideBossBarClientRpc();
            Debug.Log($"[LichBoss] {bossName} defeated!");
        }

        [Rpc(SendTo.Everyone)]
        private void HideBossBarClientRpc()
        {
            var bar = FindAnyObjectByType<BossHealthBarUI>();
            if (bar != null) bar.HideBoss();
        }

        private void HealSelf(int amount)
        {
            if (!IsServer || _health == null) return;
            _health.Heal(amount);
        }

        private void FaceTarget()
        {
            if (_target == null) return;
            Vector3 lookDir = _target.position - transform.position;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    Quaternion.LookRotation(lookDir),
                    720f * Time.deltaTime);
        }

        private Transform FindNearestPlayer()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return null;

            Transform best = null;
            float bestDist = float.MaxValue;

            foreach (var kvp in nm.ConnectedClients)
            {
                var player = kvp.Value?.PlayerObject;
                if (player == null) continue;

                float d = Vector3.Distance(transform.position, player.transform.position);
                if (d < bestDist && d <= aggroRange)
                {
                    bestDist = d;
                    best = player.transform;
                }
            }

            return best;
        }
    }

    /// <summary>
    /// Simple component to move a VFX object forward at a speed, then destroy.
    /// </summary>
    public class ProjectileMover : MonoBehaviour
    {
        private Vector3 _direction;
        private float _speed;
        private float _destroyTime;

        public void Initialize(Vector3 direction, float speed, float lifetime)
        {
            _direction = direction.normalized;
            _speed = speed;
            _destroyTime = Time.time + lifetime;
        }

        private void Update()
        {
            transform.position += _direction * (_speed * Time.deltaTime);
            if (Time.time >= _destroyTime)
                Destroy(gameObject);
        }
    }
}
