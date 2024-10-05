namespace Fantasoccer
{
    public sealed class WinningTeam
    {
        public string Owner { get; set; }
        public string Team { get; set; }
        public Dictionary<string, List<int>> Wins { get; set; }
    }
}
