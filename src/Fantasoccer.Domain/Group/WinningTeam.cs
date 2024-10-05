namespace Fantasoccer
{
    public sealed class WinningTeam
    {
        public string Owner { get; set; }
        public Dictionary<string, Dictionary<string, int>> Wins { get; set; }
    }
}
