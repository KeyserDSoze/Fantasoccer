using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public sealed class LiveFormationChange
    {
        [JsonPropertyName("d")]
        public DateTime Date { get; set; }
        [JsonPropertyName("a")]
        public string Author { get; set; } = string.Empty;
        [JsonPropertyName("p")]
        public List<LiveFormationPlayerChange> Players { get; set; } = [];
    }

    public sealed class LiveFormationPlayerChange
    {
        [JsonPropertyName("n")]
        public string Name { get; set; } = string.Empty;
        [JsonPropertyName("f")]
        public FantaSoccerRole FromPosition { get; set; }
        [JsonPropertyName("t")]
        public FantaSoccerRole ToPosition { get; set; }
    }
}
