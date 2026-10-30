using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public sealed class MarketWrapper
    {
        [JsonPropertyName("m")]
        public List<Market> Markets { get; set; }
    }
}
