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
        [Tooltip("How often this room is allowed to repeat. 0 = no restriction. Higher means rarer.")]
        public int repeatWeight = 1;

        [Tooltip("If true, only allow this room once per generated layout (per slice).")]
        public bool uniquePerSlice;

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
