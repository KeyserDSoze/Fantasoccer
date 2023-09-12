using System.Text.Json.Serialization;
using RepositoryFramework;

namespace Fantasoccer
{
    public class YearDayKey : IDefaultKey
    {
        [JsonPropertyName("y")]
        public int Year { get; set; }
        [JsonPropertyName("d")]
        public int Day { get; set; }
    }
}
