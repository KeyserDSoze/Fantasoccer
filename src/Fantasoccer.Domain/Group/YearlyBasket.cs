using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public class YearlyBasket
    {
        [JsonPropertyName("y")]
        public int Year { get; set; }
        [JsonPropertyName("t")]
        public List<AnnualTeam> Teams { get; set; }
    }
}
