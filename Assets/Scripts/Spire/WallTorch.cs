using Unity.Netcode;
using UnityEngine;

namespace DungeonGame.Spire
{
    /// <summary>
    /// Networked wall torch.
    /// - Server owns lit state (NetworkVariable)
    /// - Clients render VFX based on lit state
    /// </summary>
    public class WallTorch : NetworkBehaviour
    {
        [Header("State")]
        [SerializeField] private bool startsLit;

        [Header("Render")]
        [SerializeField] private Light torchLight;
        [SerializeField] private ParticleSystem flameVfx;
        [SerializeField] private GameObject litVisualRoot;

        [Header("Tuning")]
        [SerializeField, Range(0f, 20f)] private float litIntensity = 6f;

        private readonly NetworkVariable<bool> Lit = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public bool IsLit => Lit.Value;

        private void Awake()
        {
            if (torchLight == null) torchLight = GetComponentInChildren<Light>(true);
            if (flameVfx == null) flameVfx = GetComponentInChildren<ParticleSystem>(true);
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            Lit.OnValueChanged += (_, current) => ApplyLit(current);

            if (IsServer)
            {
                // If not seeded by TorchSeeder, fall back to inspector default.
                if (!Lit.Value && startsLit)
                {
                    Lit.Value = true;
                }
            }

            ApplyLit(Lit.Value);
        }

        public override void OnNetworkDespawn()
        {
            Lit.OnValueChanged -= (_, _) => { };
            base.OnNetworkDespawn();
        }

        private void ApplyLit(bool lit)
        {
            if (torchLight != null)
            {
                torchLight.enabled = lit;
                torchLight.intensity = lit ? litIntensity : 0f;
            }

            if (litVisualRoot != null)
            {
                litVisualRoot.SetActive(lit);
            }

            if (flameVfx != null)
            {
                if (lit)
                {
                    if (!flameVfx.isPlaying) flameVfx.Play(true);
                }
                else
                {
                    if (flameVfx.isPlaying) flameVfx.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void LightServerRpc(ServerRpcParams rpcParams = default)
        {
            if (!Lit.Value)
            {
                Lit.Value = true;
            }
        }

        /// <summary>
        /// Server-only setter used by seeding.
        /// </summary>
        public void SetLitServer(bool lit)
        {
            if (!IsServer) return;
            Lit.Value = lit;
        }
    }
}
