namespace Fantasoccer.Infrastructure.PostgreSql.Entities;

public sealed class OpeningStandingSnapshot
{
    public Guid GroupId { get; set; }
    public Guid LeagueId { get; set; }
    public int SeasonIndex { get; set; }
    public int SerieADays { get; set; }
    public int Version { get; set; } = 1;
    public ICollection<OpeningStandingRow> Teams { get; set; } = [];
}
public sealed class OpeningStandingRow
{
    public Guid TeamId { get; set; }
    public Guid BasketId { get; set; }
    public int TeamOriginYear { get; set; }
    public int Ordinal { get; set; }
    public required string Owner { get; set; }
    public string? Name { get; set; }
    public float Score { get; set; }
    public int Prize { get; set; }
    public int Position { get; set; }
}
