using System;
using System.Collections.Generic;
using UnityEngine;

namespace DungeonGame.SpireGen
{
    /// <summary>
    /// Attach to the root of a room prefab.
    /// Holds sockets + basic metadata for generation.
    /// </summary>
    public class RoomPrefab : MonoBehaviour
    {
        [Header("Id")]
        public string roomId = Guid.NewGuid().ToString("N");

        [Header("Generation")]
        [Tooltip("Selection weight. Higher = more common.")]
        public int weight = 10;

        [Tooltip("If true, only allow this room once per generated layout (per slice).")]
        public bool uniquePerSlice;

        [Tooltip("If true, generator will try to place this room periodically as a landmark.")]
        public bool landmark;

        [Tooltip("If true, this room can be used as a loop connector (must have exactly 2 compatible sockets).")]
        public bool connector;

        [Tooltip("Prevents this roomId from appearing again within the last N placements. 0 disables.")]
        public int repeatCooldown = 6;

        [Header("Sockets")]
        public List<RoomSocket> sockets = new();

        private void OnValidate()
        {
            // Auto-populate sockets for convenience.
            sockets.Clear();
            sockets.AddRange(GetComponentsInChildren<RoomSocket>(true));
        }

        public IEnumerable<RoomSocket> GetSockets(SocketType type, int size)
        {
            foreach (var s in sockets)
            {
                if (s == null) continue;
                if (s.socketType != type) continue;
                if (s.size != size) continue;
                yield return s;
            }
        }
    }
}
