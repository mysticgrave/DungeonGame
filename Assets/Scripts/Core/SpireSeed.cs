using System;
using Unity.Netcode;
using UnityEngine;

namespace DungeonGame.Core
{
    /// <summary>
    /// Per-spire seed shared to clients.
    /// MVP: just a single int seed. Later you can derive:
    /// - torch lit/unlit distribution
    /// - layer count selection
    /// - boss kit drafting
    /// - floor layout selection
    /// </summary>
    public class SpireSeed : NetworkBehaviour
    {
        [SerializeField] private int seed;

        public int Seed => seed;

        public static event Action<int> OnSeedChanged;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer)
            {
                if (seed == 0)
                {
                    seed = UnityEngine.Random.Range(int.MinValue / 2, int.MaxValue / 2);
                }

                SeedNetVar.Value = seed;
            }

            SeedNetVar.OnValueChanged += HandleSeedChanged;

            // Fire initial for client
            HandleSeedChanged(0, SeedNetVar.Value);
        }

        public override void OnNetworkDespawn()
        {
            SeedNetVar.OnValueChanged -= HandleSeedChanged;
            base.OnNetworkDespawn();
        }

        private readonly NetworkVariable<int> SeedNetVar = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private void HandleSeedChanged(int previous, int current)
        {
            seed = current;
            OnSeedChanged?.Invoke(seed);
            Debug.Log($"[SpireSeed] Seed = {seed}");
        }

        /// <summary>
        /// Utility for deterministic random from seed.
        /// </summary>
        public System.Random CreateRandom(string stream = "default")
        {
            unchecked
            {
                int h = seed;
                h = (h * 397) ^ (stream != null ? stream.GetHashCode() : 0);
                return new System.Random(h);
            }
        }
    }
}
