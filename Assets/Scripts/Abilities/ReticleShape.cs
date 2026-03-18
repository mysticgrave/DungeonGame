namespace DungeonGame.Abilities
{
    /// <summary>
    /// Visual shape of the targeting reticle shown during HoldTarget casting.
    /// </summary>
    public enum ReticleShape
    {
        /// <summary>Auto-select based on ability type: Circle for AreaZone, Ring for MeleeAoE.</summary>
        Auto,

        /// <summary>Filled circle on the ground (best for placed AoE zones).</summary>
        Circle,

        /// <summary>Ring outline around a point (best for self-centered AoE).</summary>
        Ring,

        /// <summary>Fan/arc shape showing a directional sweep (best for forward cone attacks).</summary>
        Arc,
    }
}
