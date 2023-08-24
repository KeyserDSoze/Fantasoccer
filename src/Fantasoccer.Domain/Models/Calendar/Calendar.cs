using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public record Calendar(string Id, List<Daily> Days)
    {
        private string[] splittedId;
        [JsonIgnore]
        private string[] SplittedId => splittedId ??= Id.Split('/');
        [JsonIgnore]
        public string Name => SplittedId[5];
        [JsonIgnore]
        public string GroupName => SplittedId[1];
        [JsonIgnore]
        public int Year => int.Parse(SplittedId[3]);
    }
}