namespace Fantasoccer.Infrastructure.PostgreSql.Entities;

// An original legacy substitute, not an actual official vote. Effective
// political grades are calculated from the calendar and each league's rules.
public sealed class LegacyPoliticalVoteEntry
{
    public Guid Id { get; set; }
    public required string SourceId { get; set; }
    public required string DocumentSha256 { get; set; }
    public required string ArchiveReference { get; set; }
    public required string JsonPath { get; set; }
    public int SourceOrdinal { get; set; }
    public string PlayerId { get; set; } = null!;
    public int SeasonIndex { get; set; }
    public int Day { get; set; }
    public string? ClubId { get; set; }
    public required string OriginalPayloadJson { get; set; }
    public FootballClub? Club { get; set; }
}
