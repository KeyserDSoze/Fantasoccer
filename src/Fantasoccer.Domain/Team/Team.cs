using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public class Team
    {
        public string Name { get; set; }
        public string Owner { get; set; }
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
        public int TotalMoney => 1000 - TotalCostForPlayers + RevenueMoney + MoneyFromRank + MoneyFromSoldWithOneHalfReturnedPrice;
    }
}
