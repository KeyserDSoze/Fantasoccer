using Fantasoccer;
using System.Text.RegularExpressions;

namespace FantacalcioV4.Engine.IngestionData.FormationParser
{
    internal class Sky : IFormationParser
    {
        private static readonly Regex PlayersRegex = new(@"<span[\s\S]*?<\/span>");
        public async Task<List<Player>> GetPlayersAsync(HttpClient httpClient)
        {
            return new();
            List<Player> players = new();
            string returnedValue = await httpClient.GetStringAsync("https://sport.sky.it/calcio/serie-a/probabili-formazioni/").NoContext();

            string[] values = PlayersRegex.Split(returnedValue);
            foreach (var player in values)
            {
                var name = PlayersRegex.Match(player).Value.Split('>')[1].Split('<')[0];
                players.Add(new(FantacalcioId.GetPlayerKey(FantacalcioTime.ActualYear, name), 0, default)
                {
                    Chance = new(false, false, false, true, ChanceType.Normal)
                });
            }
            return players;
        }
    }
}
