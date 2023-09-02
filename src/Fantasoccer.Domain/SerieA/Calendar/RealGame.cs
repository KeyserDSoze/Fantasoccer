using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public sealed class RealTeam
    {
        public string Name { get; set; }
        [JsonIgnore]
        public string Abbreviation => Name.ToLower()[..3];
    }
    public sealed class RealGame
    {
        public RealTeam Home { get; set; }
        public RealTeam Away { get; set; }
        public DateTime? Date { get; set; }
        public int? HomeGoal { get; set; }
        public int? AwayGoal { get; set; }
        public bool Delayed { get; set; }
        [JsonIgnore]
        public bool IsPlayed => !Date.HasValue || FantacalcioTime.ItalyNow > Date.Value;
    }
}
