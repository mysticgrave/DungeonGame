namespace DungeonGame.Abilities
{
    /// <summary>
    /// Defines how an ability executes its effect.
    /// </summary>
    public enum AbilityType
    {
        /// <summary>OverlapSphere around caster, damages all enemies in radius.</summary>
        MeleeAoE,

        /// <summary>Spawns a networked projectile that deals damage on impact.</summary>
        Projectile,

        /// <summary>Applies a buff to self: heal, speed boost, or shield.</summary>
        SelfBuff,

        /// <summary>Moves caster forward rapidly, damaging enemies in path.</summary>
        DashStrike,

        /// <summary>Places a damage zone on the ground (DoT area).</summary>
        AreaZone,
    }
}
