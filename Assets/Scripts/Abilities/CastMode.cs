namespace DungeonGame.Abilities
{
    /// <summary>
    /// How an ability is triggered by the player.
    /// </summary>
    public enum CastMode
    {
        /// <summary>Fires immediately on key press.</summary>
        Instant,

        /// <summary>Hold key to show ground targeting reticle, release to cast at that position.</summary>
        HoldTarget,
    }
}
