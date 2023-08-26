namespace Fantasoccer
{
    public record Rank
    {
        public Dictionary<string, List<RankedTeam>> RankedTeams { get; set; }
    }
}
