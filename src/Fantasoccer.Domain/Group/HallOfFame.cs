namespace Fantasoccer
{
    public sealed class HallOfFame
    {
        public RecordGame RecordGame { get; set; }
        public RecordPlayer? RecordPlayer { get; set; }
        public PlayerWithMostPointsInYear? PlayerWithMostPointsInYear { get; set; }
        public List<WinningPlayer> WinningPlayers { get; set; }
        public List<WinningTeam> WinningTeams { get; set; }
        public List<RankedTeam> Ranks { get; set; }
    }
}
