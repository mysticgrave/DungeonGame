using UnityEngine;

namespace DungeonGame.Run
{
    /// <summary>
    /// Dungeon mood state — adapts based on player behavior.
    /// Server computes; clients can read for UI/audio.
    /// </summary>
    public enum DungeonMoodType
    {
        Calm,    // High health, slow pace — fewer enemies
        Tense,   // Baseline
        Chaotic, // Low health or high kills — more enemies
        Desperate // Multiple deaths, very low health — maximum pressure
    }

    /// <summary>
    /// Singleton-ish mood state. Server updates via DungeonMoodTracker.
    /// </summary>
    public static class DungeonMood
    {
        public static DungeonMoodType Current { get; set; } = DungeonMoodType.Tense;
        public static float SpawnRateMultiplier { get; private set; } = 1f;
        public static float RespawnSpeedMultiplier { get; private set; } = 1f;

        public static void Set(DungeonMoodType mood)
        {
            Current = mood;
            (SpawnRateMultiplier, RespawnSpeedMultiplier) = mood switch
            {
                DungeonMoodType.Calm => (0.8f, 0.8f),
                DungeonMoodType.Tense => (1f, 1f),
                DungeonMoodType.Chaotic => (1.3f, 1.3f),
                DungeonMoodType.Desperate => (1.5f, 1.5f),
                _ => (1f, 1f)
            };
        }

        public static void Reset()
        {
            Set(DungeonMoodType.Tense);
        }
    }
}
