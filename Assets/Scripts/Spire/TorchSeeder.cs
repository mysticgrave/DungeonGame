using System;
using System.Collections.Generic;
using System.Linq;
using DungeonGame.Core;
using Unity.Netcode;
using UnityEngine;

namespace DungeonGame.Spire
{
    /// <summary>
    /// Seeds initial lit/unlit state of all WallTorches based on SpireSeed.
    /// Server sets torch NetworkVariables so all clients match.
    /// </summary>
    [RequireComponent(typeof(SpireSeed))]
    public class TorchSeeder : NetworkBehaviour
    {
        [Header("Seed")]
        [Tooltip("Chance a torch starts lit.")]
        [SerializeField, Range(0f, 1f)] private float litChance = 0.35f;

        private SpireSeed spireSeed;

        private void Awake()
        {
            spireSeed = GetComponent<SpireSeed>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // Only server seeds (writes torch state).
            if (!IsServer) return;

            SpireSeed.OnSeedChanged += HandleSeedChanged;

            // Seed immediately if already known.
            if (spireSeed != null && spireSeed.Seed != 0)
            {
                HandleSeedChanged(spireSeed.Seed);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                SpireSeed.OnSeedChanged -= HandleSeedChanged;
            }
            base.OnNetworkDespawn();
        }

        private void HandleSeedChanged(int seed)
        {
            if (!IsServer) return;

            var torches = FindObjectsByType<WallTorch>(FindObjectsSortMode.None);
            var list = new List<(string key, WallTorch torch)>(torches.Length);
            foreach (var t in torches)
            {
                list.Add((GetHierarchyPath(t.transform), t));
            }

            // Stable order across machines
            list.Sort((a, b) => string.CompareOrdinal(a.key, b.key));

            Debug.Log($"[TorchSeeder] Seeding {list.Count} torches with seed={seed}, litChance={litChance}");

            for (int i = 0; i < list.Count; i++)
            {
                var torch = list[i].torch;
                int derived = Hash(seed, list[i].key);
                var rng = new System.Random(derived);
                bool lit = rng.NextDouble() < litChance;
                torch.SetLitServer(lit);
            }
        }

        private static int Hash(int seed, string key)
        {
            unchecked
            {
                int h = seed;
                h = (h * 397) ^ (key != null ? key.GetHashCode() : 0);
                return h;
            }
        }

        private static string GetHierarchyPath(Transform t)
        {
            var stack = new Stack<string>();
            while (t != null)
            {
                stack.Push(t.name);
                t = t.parent;
            }
            return string.Join("/", stack);
        }
    }
}
