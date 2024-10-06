namespace Fantasoccer
{
    public sealed class Market
    {
        public string Id { get; set; }
        public string Buyer { get; set; }
        public string Seller { get; set; }
        public List<Player> SellerPlayers { get; set; }
        public List<Player> BuyerPlayers { get; set; }
        public int MoneyFromBuyer { get; set; }
        public int MoneyFromSeller { get; set; }
        public List<string> Approvers { get; set; }
        public List<string> Deniers { get; set; }
        public MarketStatus Status { get; set; }
        public DateTime CreationTime { get; set; }
    }
}
