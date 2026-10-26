namespace Fantasoccer.Infrastructure.PostgreSql.Entities;

// An archived source row, NOT a playable football identity. Identical rows keep
// distinct source ordinals; no name, key or club/role match is invented.
public sealed class UnresolvedPlayerCatalogEntry
{
    public Guid Id { get; set; }
    public required string SourceId { get; set; }
    public required string DocumentSha256 { get; set; }
    public required string ArchiveReference { get; set; }
    public required string JsonPath { get; set; }
    public int SourceOrdinal { get; set; }
    public int SeasonIndex { get; set; }
    public required string Reason { get; set; }
    public required string OriginalPayloadJson { get; set; }
}
