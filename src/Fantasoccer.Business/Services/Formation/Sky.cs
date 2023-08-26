using System.Text.RegularExpressions;

namespace Fantasoccer.Engine.IngestionData
{
    internal class Sky : IFormationParser
    {
        private static readonly Regex PlayersRegex = new(@"<span[\s\S]*?<\/span>");
        public async Task<List<ChancedRealPlayer>> GetPlayersAsync(HttpClient httpClient)
        {
            List<ChancedRealPlayer> players = new();
            var returnedValue = await httpClient.GetStringAsync("https://sport.sky.it/calcio/serie-a/probabili-formazioni/").NoContext();
            var values = PlayersRegex.Split(returnedValue);
            foreach (var player in values)
            {
                var name = PlayersRegex.Match(player).Value.Split('>')[1].Split('<')[0];
                var chancedPlayer = new ChancedRealPlayer
                {
                    Name = KeyUtils.GetPlayerKey(name),
                    Year = FantacalcioTime.ActualYear,
                    Chance = new Chance()
                    {
                        Sky = true
                    }
                };
                players.Add(chancedPlayer);
            }
            return players;
        }
    }
}
