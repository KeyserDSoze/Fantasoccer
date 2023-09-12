using System.Text.Json.Serialization;
using RepositoryFramework;

namespace Fantasoccer
{
    public class YearDayLeague : IDefaultKey
    {
        [JsonPropertyName("y")]
        public int Year { get; set; }
        [JsonPropertyName("d")]
        public int Day { get; set; }
        [JsonPropertyName("g")]
        public string Group { get; set; }
        [JsonPropertyName("l")]
        public string League { get; set; }
    }
}
