using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public sealed class HallOfFame
    {
        [JsonPropertyName("rg")]
        public RecordGame? RecordGame { get; set; }
        [JsonPropertyName("rp")]
        public RecordPlayer? RecordPlayer { get; set; }
        [JsonPropertyName("ppmy")]
        public PlayerWithMostPointsInYear? PlayerWithMostPointsInYear { get; set; }
        [JsonPropertyName("wp")]
        public List<WinningPlayer> WinningPlayers { get; set; }
        [JsonPropertyName("wt")]
        public List<WinningTeam> WinningTeams { get; set; }
        [JsonPropertyName("rts")]
        public List<RankedTeam> Ranks { get; set; }
    }
}
