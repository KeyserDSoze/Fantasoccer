namespace Fantasoccer.Infrastructure.PostgreSql.Entities;

// A reconciled per-season checkpoint permits incremental refresh without
// replacing owner career totals with an incomplete set of imported calendars.
public sealed class HallRankContribution
{
    public Guid GroupId { get; set; }
    public Guid LeagueId { get; set; }
    public int SeasonIndex { get; set; }
    public Guid OwnerUserId { get; set; }
    public float Points { get; set; }
    public int Victories { get; set; }
    public int Draws { get; set; }
    public int Defeats { get; set; }
    public int Goals { get; set; }
    public int SufferedGoals { get; set; }
    public float ValuePoints { get; set; }
    public float SufferedValuePoints { get; set; }
    public int PlusMoney { get; set; }
    public int Money { get; set; }
}
public sealed class HallBaselineConfirmation
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public Guid LeagueId { get; set; }
    public Guid ActorUserId { get; set; }
    public int ArchiveVersion { get; set; }
    public DateTimeOffset RecordedAtUtc { get; set; }
}
