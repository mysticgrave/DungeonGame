using System.Collections.Generic;
using DungeonGame.SpireGen;
using UnityEngine;

namespace DungeonGame.Run
{
    /// <summary>
    /// Configuration for a single dungeon type / biome.
    /// Create via Assets > Create > DungeonGame > Dungeon Config.
    /// Add to DungeonRegistry so the host can select it before entering.
    /// </summary>
    [CreateAssetMenu(fileName = "NewDungeon", menuName = "DungeonGame/Dungeon Config", order = 2)]
    public class DungeonConfig : ScriptableObject
    {
        [Header("Identity")]
        public string dungeonId = "default";
        public string displayName = "The Spire";
        [TextArea(2, 4)] public string description = "";
        public Sprite icon;
        [Tooltip("Freeform label: Easy, Normal, Hard, Nightmare, etc.")]
        public string difficultyLabel = "Normal";

        [Header("Scene")]
        [Tooltip("Scene to load for this dungeon. Must be in Build Settings.")]
        public string sceneName = "Spire_Slice";

        [Header("Generation — Shape")]
        [Tooltip("Room prefabs available for this dungeon. Each must have RoomPrefab + NetworkObject.")]
        public List<RoomPrefab> roomPrefabs = new();
        [Min(1)] public int mainPathRooms = 20;
        [Range(0, 10)] public int branches = 4;
        [Min(1)] public int branchLengthMin = 2;
        [Min(1)] public int branchLengthMax = 6;

        [Header("Generation — Loops")]
        [Range(0, 10)] public int loopAttempts = 2;

        [Header("Generation — Landmarks")]
        [Range(0, 20)] public int landmarkEvery = 8;

        [Header("Generation — Sockets")]
        public SocketType socketType = SocketType.DoorSmall;
        public int socketSize = 0;

        [Header("Generation — Key Rooms")]
        [Tooltip("Optional first room.")]
        public RoomPrefab startRoom;
        [Tooltip("Boss / end room.")]
        public RoomPrefab bossRoom;
        [Min(1)] public int minRoomsBeforeBoss = 10;

        // Future: enemy pools, reward tables, mood/ambience tuning go here later.

        private void OnValidate()
        {
            if (branchLengthMax < branchLengthMin)
                branchLengthMax = branchLengthMin;
            if (mainPathRooms < 1)
                mainPathRooms = 1;
            if (minRoomsBeforeBoss < 1)
                minRoomsBeforeBoss = 1;
        }
    }
}
