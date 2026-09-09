using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public sealed class OpeningCompetitionStanding
    {
        [JsonPropertyName("o")] public required string Owner { get; set; }
        [JsonPropertyName("n")] public string? Name { get; set; }
        [JsonPropertyName("s")] public float Score { get; set; }
        [JsonPropertyName("p")] public int Prize { get; set; }
        [JsonPropertyName("r")] public int Position { get; set; }
    }

    public sealed class OpeningCompetitionStandings
    {
        [JsonPropertyName("g")] public required string Group { get; set; }
        [JsonPropertyName("l")] public required string League { get; set; }
        [JsonPropertyName("y")] public int Year { get; set; }
        [JsonPropertyName("d")] public int SerieADays { get; set; }
        [JsonPropertyName("t")] public List<OpeningCompetitionStanding> Teams { get; set; } = [];
    }
}
