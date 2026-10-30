using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public class League
    {
        [JsonPropertyName("i")]
        public string Id { get; set; }
        [JsonPropertyName("n")]
        public string Name { get; set; }
        [JsonPropertyName("m")]
        public bool IsMain { get; set; }
        [JsonPropertyName("t")]
        public LeagueType Type { get; set; }
        public LeagueType GetAnnualType(int year)
        {
            var annualLeague = Years?.FirstOrDefault(x => x.Year == year);
            if (annualLeague != null && annualLeague.Type != LeagueType.Null)
            {
                return annualLeague.Type;
            }
            return Type;
        }
        [JsonPropertyName("y")]
        public List<AnnualLeague> Years { get; set; }
        [JsonPropertyName("b")]
        public required List<string> BasketsId { get; set; }
    }
}
