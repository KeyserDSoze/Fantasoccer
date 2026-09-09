using System.Text.Json.Serialization;
using RepositoryFramework;

namespace Fantasoccer
{
    public sealed class OpeningCompetitionKey : IDefaultKey
    {
        [JsonPropertyName("g")] public required string Group { get; set; }
        [JsonPropertyName("l")] public required string League { get; set; }
        [JsonPropertyName("y")] public required int Year { get; set; }
    }
}
