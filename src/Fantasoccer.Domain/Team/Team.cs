using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public class Team
    {
        [JsonPropertyName("n")]
        public string Name { get; set; }
        [JsonPropertyName("o")]
        public string Owner { get; set; }
        [JsonPropertyName("a")]
        public List<string> AdditionalOwners { get; set; }
        [JsonPropertyName("p")]
        public List<Player> Players { get; set; }
        [JsonIgnore]
        public IEnumerable<Player> ActivePlayers => Players.Where(x => x.Status == PlayerInTeamStatus.Active);
        [JsonIgnore]
        public int RevenueMoney { get; set; }
        [JsonIgnore]
        public int MoneyFromRank { get; set; }
        [JsonIgnore]
        public int MoneyFromSoldWithNoReturnedPrice => Players.Where(x => x.Status == PlayerInTeamStatus.SoldWithNoReturnedPrice).Sum(x => x.Price);
        [JsonIgnore]
        public int MoneyFromSoldWithOneHalfReturnedPrice => Players.Where(x => x.Status == PlayerInTeamStatus.SoldForOneHalf).Sum(x => x.Price / 2);
        [JsonIgnore]
        public int TotalCostForPlayers => Players.Sum(x => x.Price);
        [JsonIgnore]
        public int Cost => TotalCostForPlayers + RevenueMoney + MoneyFromRank + MoneyFromSoldWithOneHalfReturnedPrice;
    }
}
