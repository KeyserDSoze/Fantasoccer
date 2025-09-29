using System.Globalization;
using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public class Calendar
    {
        [JsonPropertyName("y")]
        public int Year { get; set; }
        [JsonPropertyName("r")]
        public Dictionary<string, List<Day>> Rounds { get; set; }
        public (CalendarGame? Value, int SerieADay) GetGameById(string id)
        {
            if (Rounds != null)
                foreach (var round in Rounds)
                {
                    foreach (var day in round.Value)
                    {
                        foreach (var game in day.Games)
                        {
                            if (game.Id == id)
                            {
                                return (game, day.SerieADay);
                            }
                        }
                    }
                }
            return (null, 0);
        }
    }
}
