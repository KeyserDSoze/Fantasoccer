using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public class RealDay
    {
        [JsonPropertyName("y")]
        public int Year { get; set; }
        [JsonPropertyName("a")]
        public int SerieADay { get; set; }
        [JsonPropertyName("g")]
        public List<RealGame> Games { get; set; }
    }
}
