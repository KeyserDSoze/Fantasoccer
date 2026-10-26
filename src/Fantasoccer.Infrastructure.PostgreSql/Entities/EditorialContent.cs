namespace Fantasoccer.Infrastructure.PostgreSql.Entities;

public sealed class NewsEdition
{
    public Guid GroupId { get; set; }
    public int SeasonIndex { get; set; }
    public int Day { get; set; }
    public int Version { get; set; } = 1;
    public ICollection<NewsArticle> Articles { get; set; } = [];
}
public sealed class NewsArticle
{
    public int Ordinal { get; set; }
    public Guid? LeagueId { get; set; }
    public required string LeagueName { get; set; }
    public required string Title { get; set; }
    public required string Content { get; set; }
    public string? Image { get; set; }
    public DateTimeOffset PublishedAtUtc { get; set; }
    public DateTime? LegacyDate { get; set; }
    public bool IsGenerated { get; set; }
    public Guid? GeneratedByUserId { get; set; }
    public int? SourceVersion { get; set; }
}
public sealed class TeamAdvice
{
    public Guid GroupId { get; set; }
    public Guid BasketId { get; set; }
    public int SeasonIndex { get; set; }
    public Guid TeamId { get; set; }
    public Guid OwnerUserId { get; set; }
    public int Day { get; set; }
    public required string Message { get; set; }
    public bool IsGenerated { get; set; }
    public DateTimeOffset? GeneratedAtUtc { get; set; }
    public int Version { get; set; } = 1;
    public Guid? GeneratedByUserId { get; set; }
    public int? SourceRosterVersion { get; set; }
}
