namespace Fantasoccer
{
    public enum PlayerInTeamStatus
    {
        Active,
        SoldForOneHalf,
        SoldWithNoReturnedPrice,
        Sold,
        Removed,
        // Left Serie A (or long-term out): Revenue holds the refund fixed by the league rules.
        LeftSerieA
    }
}