namespace DungeonGame.Combat
{
    public interface IDamageable
    {
        /// <summary>Primary damage method with rich metadata.</summary>
        void TakeDamage(DamageInfo info);

        /// <summary>Backward-compatible shorthand. Wraps into DamageInfo(Physical).</summary>
        void TakeDamage(int amount) => TakeDamage(new DamageInfo(amount));
    }
}
