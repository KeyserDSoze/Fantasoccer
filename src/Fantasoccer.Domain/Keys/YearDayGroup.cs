using System.Text.Json.Serialization;
using RepositoryFramework;

namespace Fantasoccer
{
    public class YearDayGroup : IDefaultKey
    {
        [JsonPropertyName("y")]
        public int Year { get; set; }
        [JsonPropertyName("d")]
        public int Day { get; set; }
        [JsonPropertyName("g")]
        public string Group { get; set; }
    }
}
