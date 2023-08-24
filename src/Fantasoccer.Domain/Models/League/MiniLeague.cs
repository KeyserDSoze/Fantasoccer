using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public record MiniLeague(string Id, LeagueType Type)
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