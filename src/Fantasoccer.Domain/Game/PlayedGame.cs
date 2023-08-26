namespace Fantasoccer
{
    public class PlayedGame
    {
        public string Id { get; set; }
        public string Home { get; set; }
        public string Away { get; set; }
        public GameResult Result { get; set; }
        public List<Player> HomePlayers { get; set; }
        public List<Player> AwayPlayers { get; set; }
    }
}
