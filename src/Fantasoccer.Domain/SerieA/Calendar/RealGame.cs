using System.Text.Json.Serialization;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Fantasoccer
{
    public sealed class RealGame
    {
        [JsonPropertyName("h")]
        public RealTeam Home { get; set; }
        [JsonPropertyName("a")]
        public RealTeam Away { get; set; }
        [JsonPropertyName("d")]
        public DateTime? Date { get; set; }
        [JsonPropertyName("g")]
        public int? HomeGoal { get; set; }
        [JsonPropertyName("y")]
        public int? AwayGoal { get; set; }
        [JsonPropertyName("e")]
        public bool Delayed { get; set; }
        [JsonIgnore]
        public bool IsPlayed => !Delayed && Date.HasValue && FantacalcioTime.ItalyNow > Date.Value;
    }
    public static class RealGameExtensions
    {
        private const string DelayedLabel = "rimandata";
        private const string ToPlayLabel = "da giocare";
        public static string CalculateResultAsString(this RealGame? realGame)
        {
            if (realGame == null)
            {
                return ToPlayLabel;
            }
            else if (realGame.IsPlayed)
            {
                return $"{realGame.HomeGoal} - {realGame.AwayGoal}";
            }
            else if (realGame.Delayed == true)
            {
                return DelayedLabel;
            }
            else
            {
                return realGame.Date?.ToString("yyyy-MM-dd HH:mm") ?? ToPlayLabel;
            }
        }
    }
}
