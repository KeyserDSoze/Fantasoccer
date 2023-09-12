using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public class Calendar
    {
        [JsonPropertyName("y")]
        public int Year { get; set; }
        [JsonPropertyName("r")]
        public Dictionary<string, List<Day>> Rounds { get; set; }
    }
}
