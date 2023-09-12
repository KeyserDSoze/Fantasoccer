using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public class AnnualLeague
    {
        [JsonPropertyName("y")]
        public required int Year { get; set; }
        [JsonPropertyName("s")]
        public required LeagueSetting Settings { get; set; }
    }
}
