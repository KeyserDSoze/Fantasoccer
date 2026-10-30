using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public sealed class AppIdentityState
    {
        [JsonPropertyName("g")]
        public string? GroupId { get; set; }
        [JsonPropertyName("l")]
        public string? LeagueId { get; set; }
        [JsonPropertyName("y")]
        public int? Year { get; set; }
        [JsonPropertyName("m")]
        public string? MeOrLeaderEmail { get; set; }
    }
}
