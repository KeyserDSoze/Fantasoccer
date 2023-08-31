namespace Fantasoccer.Applications.App
{
    public class FantasoccerState
    {
        public string? Email { get; set; }
        public string? GroupId { get; set; }
        public string? LeagueId { get; set; }
        public int Year { get; set; } = FantacalcioTime.ActualYear;
        public Func<ValueTask>? Update { get; set; }
        public LeagueKey LeagueKey => new() { Group = GroupId!, League = LeagueId!, Year = Year };
    }
}
