namespace DungeonGame.Meta
{
    /// <summary>
    /// Outcome of a run: how it ended and rewards to apply per player.
    /// Server computes; clients apply to their own MetaProgression (using their class for EXP).
    /// </summary>
    public struct RunResult
    {
        public int Gold;
        public int Exp;
        public RunOutcome Outcome;

        public RunResult(int gold, int exp, RunOutcome outcome)
        {
            Gold = gold;
            Exp = exp;
            Outcome = outcome;
        }
    }

    public enum RunOutcome
    {
        None = 0,
        Evac = 1,
        Victory = 2,
        Wipe = 3
    }
}
