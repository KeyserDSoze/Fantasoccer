namespace Fantasoccer
{
    public class LeagueSetting
    {
        public Dictionary<Role, VoteLeagueSetting> Votes { get; set; }
        public int DelayedDay { get; set; }
        public int PointForFirstGoal { get; set; }
        public int PointForNextGoal { get; set; }
        public int PointForOwnGoal { get; set; }
        public int DifferencePointForOwnGoal { get; set; }
        public int PointInHome { get; set; }
        public int PointForVictory { get; set; }
        public int PointForDefeat { get; set; }
        public int PointForDraw { get; set; }
        public int PointForStrongDefense { get; set; }
        public int PointForStrongDefense4 { get; set; }
        public int PointForStrongDefense5 { get; set; }
        public int PointForCleanSheet { get; set; }
        public int MoneyForGoal { get; set; }
        public int MoneyForSufferedGoal { get; set; }
        public static LeagueSetting Default => new()
        {
            Votes = new Dictionary<Role, VoteLeagueSetting>() { { Role.Undefined, new VoteLeagueSetting()
            {
                 Assist = 1,
                 Goal=3,
                 OwnGoal = -3,
                 Penalty = 3,
                 StoppedPenalty = 3,
                 RedCard = -1,
                 YellowCard=-0.5f,
                 SufferedGoal = -1,
                 WrongedPenalty = -3
            } } },
            DelayedDay = 2,
            PointForFirstGoal = 66,
            PointForNextGoal = 6,
            PointForOwnGoal = 6,
            DifferencePointForOwnGoal = 6,
            PointInHome = 0,
            PointForVictory = 3,
            PointForDefeat = 0,
            PointForDraw = 1,
            PointForStrongDefense = 3,
            PointForStrongDefense4 = 3,
            PointForStrongDefense5 = 3,
            MoneyForGoal = 5,
            MoneyForSufferedGoal = 3
        };
    }
}
