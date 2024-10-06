namespace Fantasoccer
{
    public enum MarketType
    {
        WithVote,
        WithoutVote,
        Denied
    }
    public static class MarketTypeExtensions
    {
        public static MarketType[] All { get; } = (MarketType[])Enum.GetValues(typeof(MarketType));
    }
}
