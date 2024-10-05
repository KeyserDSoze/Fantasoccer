namespace Fantasoccer
{
    public sealed class WinningPlayer
    {
        public Player Player { get; set; }
        public Dictionary<string, List<int>> Wins { get; set; }
    }
}
