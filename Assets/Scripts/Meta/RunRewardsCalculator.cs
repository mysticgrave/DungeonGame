using UnityEngine;

namespace DungeonGame.Meta
{
    /// <summary>
    /// Computes gold and EXP for a run based on floors reached and outcome.
    /// Tune via inspector on a component or use static defaults.
    /// </summary>
    public static class RunRewardsCalculator
    {
        public const int BaseGold = 20;
        public const int GoldPerFloor = 5;
        public const int VictoryGoldBonus = 100;
        public const int BaseExp = 15;
        public const int ExpPerFloor = 3;
        public const int VictoryExpBonus = 50;
        public const int EvacGoldPenalty = 0; // optional: reduce gold if evac
        public const int WipeGoldPenalty = 10; // less gold on wipe

        public static RunResult Compute(int floorsReached, RunOutcome outcome)
        {
            int gold = BaseGold + floorsReached * GoldPerFloor;
            int exp = BaseExp + floorsReached * ExpPerFloor;

            switch (outcome)
            {
                case RunOutcome.Victory:
                    gold += VictoryGoldBonus;
                    exp += VictoryExpBonus;
                    break;
                case RunOutcome.Evac:
                    gold = Mathf.Max(0, gold - EvacGoldPenalty);
                    break;
                case RunOutcome.Wipe:
                    gold = Mathf.Max(0, gold - WipeGoldPenalty);
                    break;
            }

            return new RunResult(gold, exp, outcome);
        }
    }
}
