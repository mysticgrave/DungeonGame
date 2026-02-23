using System;
using System.Collections.Generic;
using UnityEngine;

namespace DungeonGame.SpireGen
{
    [Serializable]
    public struct RoomPlacement
    {
        public string roomId;
        public Vector3 position;
        public int rotationY; // degrees (0/90/180/270)
    }

    [Serializable]
    public struct SocketRef
    {
        // Room placement index in SpireLayoutData.rooms
        public int roomIndex;

        // Relative path from that room root to the socket transform.
        public string socketPath;

        public SocketType socketType;
        public int size;
    }

    [Serializable]
    public struct SocketConnection
    {
        // A is the "target" socket (existing layout socket). Doors spawn at A for now.
        public SocketRef a;
        public SocketRef b;
    }

    [Serializable]
    public class SpireLayoutData
    {
        public int seed;
        public List<RoomPlacement> rooms = new();
        public List<SocketConnection> connections = new();
    }
}
