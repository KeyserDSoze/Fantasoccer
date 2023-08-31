namespace Fantasoccer
{
    public record RankedTeam
    {
        public string Name { get; set; }
        public string Owner { get; set; }
        public int Point { get; set; }
        public int Victories { get; set; }
        public int Draws { get; set; }
        public int Defeats { get; set; }
        public int Goal { get; set; }
        public int SufferedGoal { get; set; }
        public float ValuePoint { get; set; }
        public float SufferedValuePoint { get; set; }
        public int PlusMoney { get; set; }
        public int Money { get; set; }
        public static RankedTeam operator +(RankedTeam a, RankedTeam b)
            => new()
            {
                Name = a.Name,
                Owner = a.Owner,
                Point = a.Point + b.Point,
                Defeats = a.Defeats + b.Defeats,
                Goal = a.Goal + b.Goal,
                Draws = a.Draws + b.Draws,
                Money = a.Money + b.Money,
                PlusMoney = a.PlusMoney + b.PlusMoney,
                SufferedGoal = a.SufferedGoal + b.SufferedGoal,
                SufferedValuePoint = a.SufferedValuePoint + b.SufferedValuePoint,
                ValuePoint = a.ValuePoint + b.ValuePoint,
                Victories = a.Victories + b.Victories,
            };
    }
}
