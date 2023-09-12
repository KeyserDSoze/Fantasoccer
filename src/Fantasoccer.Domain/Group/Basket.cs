using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public class Basket
    {
        [JsonPropertyName("i")]
        public string Id { get; set; }
        [JsonPropertyName("n")]
        public string Name { get; set; }
        [JsonPropertyName("y")]
        public List<YearlyBasket> Years { get; set; }
    }
}
