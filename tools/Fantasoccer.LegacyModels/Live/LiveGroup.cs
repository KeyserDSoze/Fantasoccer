using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public sealed class LiveGroup
    {
        [JsonPropertyName("n")]
        public string Name { get; set; }
        [JsonPropertyName("l")]
        public List<LiveLeague> Leagues { get; set; }
    }
}
