namespace DungeonGame.Enemies
{
    /// <summary>
    /// Runtime data for one active status effect on a target.
    /// </summary>
    public class StatusEffectInstance
    {
        public StatusEffectType Type;
        public float RemainingDuration;
        public float TickInterval;
        public float NextTickAt;
        public int DamagePerTick;
        public float SpeedMultiplier;

        public bool IsExpired => RemainingDuration <= 0f;
    }
}
