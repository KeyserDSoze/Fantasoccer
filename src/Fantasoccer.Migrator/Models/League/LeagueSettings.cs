using System.Collections.Generic;

namespace FantacalcioV4
{
    public record LeagueSetting(Dictionary<Role, VoteLeagueSetting> Votes, int DelayedDay, int PointForFirstGoal,
        int PointForNextGoal, int PointForOwnGoal, int DifferencePointForOwnGoal, int PointInHome, int PointForVictory,
        int PointForDefeat, int PointForDraw, int PointForStrongDefense, int PointForStrongDefense4, int PointForStrongDefense5,
        int PointForCleanSheet, int MoneyForGoal, int MoneyForSufferedGoal)
    {
        public static LeagueSetting Default => new(
           new Dictionary<Role, VoteLeagueSetting>() { { Role.Undefined, new VoteLeagueSetting(3, 3, -1, 3, -3, -3, 1, 0.5f, 1) } },
            2, 66, 6, 6, 6, 0, 3, 0, 1, 3, 3, 3, 0, 5, 3);
    }
}