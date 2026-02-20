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
    public class SpireLayoutData
    {
        public int seed;
        public List<RoomPlacement> rooms = new();
    }
}
