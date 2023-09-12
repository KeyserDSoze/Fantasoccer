using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public class Chance
    {
        [JsonPropertyName("f")]
        public bool Fantagazzetta { get; set; }
        [JsonPropertyName("g")]
        public bool Gazzetta { get; set; }
        [JsonPropertyName("m")]
        public bool Mediaset { get; set; }
        [JsonPropertyName("s")]
        public bool Sky { get; set; }
        [JsonPropertyName("t")]
        public ChanceType Status { get; set; }
        public static Chance Default() => new()
        {
            Status = ChanceType.Normal,
            Fantagazzetta = false,
            Gazzetta = false,
            Mediaset = false,
            Sky = false
        };
    }
}
