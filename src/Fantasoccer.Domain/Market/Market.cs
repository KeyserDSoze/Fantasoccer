using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public sealed class Market
    {
        [JsonPropertyName("i")]
        public string Id { get; set; }
        [JsonPropertyName("b")]
        public string Buyer { get; set; }
        [JsonPropertyName("s")]
        public string Seller { get; set; }
        [JsonPropertyName("sp")]
        public List<Player> SellerPlayers { get; set; }
        [JsonPropertyName("bp")]
        public List<Player> BuyerPlayers { get; set; }
        [JsonPropertyName("mb")]
        public int MoneyFromBuyer { get; set; }
        [JsonPropertyName("ms")]
        public int MoneyFromSeller { get; set; }
        [JsonPropertyName("a")]
        public List<string> Approvers { get; set; }
        [JsonPropertyName("d")]
        public List<string> Deniers { get; set; }
        [JsonPropertyName("st")]
        public MarketStatus Status { get; set; }
        [JsonPropertyName("ct")]
        public DateTime CreationTime { get; set; }
    }
}
