using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public record RankedTeam
    {
        [JsonPropertyName("n")]
        public string Name { get; set; }
        [JsonPropertyName("o")]
        public string Owner { get; set; }
        [JsonPropertyName("p")]
        public int Point { get; set; }
        [JsonPropertyName("v")]
        public int Victories { get; set; }
        [JsonPropertyName("d")]
        public int Draws { get; set; }
        [JsonPropertyName("e")]
        public int Defeats { get; set; }
        [JsonPropertyName("g")]
        public int Goal { get; set; }
        [JsonPropertyName("s")]
        public int SufferedGoal { get; set; }
        [JsonPropertyName("x")]
        public float ValuePoint { get; set; }
        [JsonPropertyName("w")]
        public float SufferedValuePoint { get; set; }
        [JsonPropertyName("z")]
        public int PlusMoney { get; set; }
        [JsonPropertyName("m")]
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
