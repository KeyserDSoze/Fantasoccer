namespace Fantasoccer
{
    public sealed class WinningPlayer
    {
        public Player Player { get; set; }
        public Dictionary<string, Dictionary<string, int>> Wins { get; set; }
    }
}
