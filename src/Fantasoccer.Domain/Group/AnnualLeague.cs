using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public class AnnualLeague
    {
        [JsonPropertyName("y")]
        public required int Year { get; set; }
        [JsonPropertyName("t")]
        public LeagueType Type { get; set; } = LeagueType.Null;
        [JsonPropertyName("s")]
        public required LeagueSetting Settings { get; set; }
    }
}
