using Fantasoccer.Application.Core.Caching;

namespace Fantasoccer.Application.Core.Football;

// Operational football data shared by every group: one snapshot per season/day.
// Not a history: official votes and statistics stay in PostgreSQL.
public sealed record CachedChance(string PlayerId, string Name, string? ClubId, string? ClubName, int Role, bool IsActive, bool IsVisible,
    bool Fantagazzetta, bool Gazzetta, bool Mediaset, bool Sky, int Status, string? Description);
public sealed record FootballChancesPayload(int Season, int Day, string[] Sources, CachedChance[] Players);

public sealed record CachedVote(int Role, float Value, bool IsFinal, int Goals, int Penalties, int Assists, int StoppedPenalties, int SufferedGoals,
    int WrongedPenalties, int OwnGoals, int Status, bool ManOfTheMatch, bool HasVote, bool IsOut, bool IsIn, bool Injured);
public sealed record CachedLiveVote(string PlayerId, string Name, string? ClubId, string? ClubName, int PlayerRole, bool IsActive, bool IsVisible, CachedVote Vote);
public sealed record FootballLiveVotesPayload(int Season, int Day, CachedLiveVote[] Players);

public interface IFootballLiveDataStore
{
    Task<CacheEnvelope<FootballChancesPayload>?> ChancesAsync(int season, int day, CancellationToken cancellationToken = default);
    Task<CachePublishOutcome> PublishChancesAsync(DateTimeOffset fetchedAtUtc, FootballChancesPayload payload, CancellationToken cancellationToken = default);
    Task<CacheEnvelope<FootballLiveVotesPayload>?> LiveVotesAsync(int season, int day, CancellationToken cancellationToken = default);
    // clubScope != null: a recovery refresh replaces only those clubs' entries.
    Task<CachePublishOutcome> PublishLiveVotesAsync(DateTimeOffset fetchedAtUtc, FootballLiveVotesPayload payload, IReadOnlySet<string>? clubScope,
        CancellationToken cancellationToken = default);
}

public sealed class FootballLiveDataStore(SharedSnapshotCache cache) : IFootballLiveDataStore
{
    public const string ChancesKind = "football-chances";
    public const string LiveVotesKind = "football-live-votes";
    // Retention of the physical blob; the purge job and the lifecycle policy remove it afterwards.
    private static readonly TimeSpan Retention = TimeSpan.FromDays(21);

    public Task<CacheEnvelope<FootballChancesPayload>?> ChancesAsync(int season, int day, CancellationToken cancellationToken = default) =>
        cache.ReadAsync<FootballChancesPayload>(CacheKeys.FootballChances(season, day), ChancesKind, CacheKeys.FootballSchemaVersion, cancellationToken);

    public Task<CacheEnvelope<FootballLiveVotesPayload>?> LiveVotesAsync(int season, int day, CancellationToken cancellationToken = default) =>
        cache.ReadAsync<FootballLiveVotesPayload>(CacheKeys.FootballLiveVotes(season, day), LiveVotesKind, CacheKeys.FootballSchemaVersion, cancellationToken);

    public Task<CachePublishOutcome> PublishChancesAsync(DateTimeOffset fetchedAtUtc, FootballChancesPayload payload, CancellationToken cancellationToken = default)
    {
        // Never replace a valid snapshot with an empty one.
        if (payload.Players.Length == 0) throw new ArgumentException("Empty probable-lineup snapshot.", nameof(payload));
        return cache.PublishAsync(CacheKeys.FootballChances(payload.Season, payload.Day),
            new CacheEnvelope<FootballChancesPayload>(CacheKeys.FootballSchemaVersion, ChancesKind, fetchedAtUtc, fetchedAtUtc.AddHours(6),
                fetchedAtUtc.UtcTicks, null, payload), Retention, cancellationToken: cancellationToken);
    }

    public Task<CachePublishOutcome> PublishLiveVotesAsync(DateTimeOffset fetchedAtUtc, FootballLiveVotesPayload payload, IReadOnlySet<string>? clubScope,
        CancellationToken cancellationToken = default)
    {
        if (payload.Players.Length == 0) throw new ArgumentException("Empty live-vote snapshot.", nameof(payload));
        var envelope = new CacheEnvelope<FootballLiveVotesPayload>(CacheKeys.FootballSchemaVersion, LiveVotesKind, fetchedAtUtc, fetchedAtUtc.AddMinutes(2),
            fetchedAtUtc.UtcTicks, null, payload);
        Func<CacheEnvelope<FootballLiveVotesPayload>, CacheEnvelope<FootballLiveVotesPayload>, CacheEnvelope<FootballLiveVotesPayload>?>? merge = clubScope is null ? null
            : (current, incoming) => incoming with
            {
                Payload = incoming.Payload with
                {
                    Players = current.Payload.Players.Where(x => x.ClubId is null || !clubScope.Contains(x.ClubId))
                        .Concat(incoming.Payload.Players).OrderBy(x => x.PlayerId, StringComparer.Ordinal).ToArray()
                }
            };
        return cache.PublishAsync(CacheKeys.FootballLiveVotes(payload.Season, payload.Day), envelope, Retention, merge, cancellationToken);
    }
}

public static class FootballLiveDataMapping
{
    public static CachedVote Cached(Vote vote) => new((int)vote.Role, vote.Value, vote.IsFinal, vote.Goal, vote.Penalty, vote.Assist, vote.StoppedPenalty,
        vote.SufferedGoal, vote.WrongedPenalty, vote.OwnGoal, (int)vote.Status, vote.ManOfTheMatch, vote.HasVote, vote.IsOut, vote.IsIn, vote.Injured);
    public static Vote Domain(CachedVote vote) => new()
    {
        Role = (Role)vote.Role, Value = vote.Value, IsFinal = vote.IsFinal, Goal = vote.Goals, Penalty = vote.Penalties, Assist = vote.Assists,
        StoppedPenalty = vote.StoppedPenalties, SufferedGoal = vote.SufferedGoals, WrongedPenalty = vote.WrongedPenalties, OwnGoal = vote.OwnGoals,
        Status = (Behaviour)vote.Status, ManOfTheMatch = vote.ManOfTheMatch, HasVote = vote.HasVote, IsOut = vote.IsOut, IsIn = vote.IsIn, Injured = vote.Injured
    };
    public static VoteDto Dto(CachedVote vote) => new(vote.Role, vote.Value, vote.IsFinal, vote.Goals, vote.Penalties, vote.Assists, vote.StoppedPenalties,
        vote.SufferedGoals, vote.WrongedPenalties, vote.OwnGoals, vote.Status, vote.ManOfTheMatch, vote.HasVote, vote.IsOut, vote.IsIn, vote.Injured);
    public static RealTeam? Club(string? id, string? name) => id is null ? null : new() { Name = name ?? id, Abbreviation = id };
    public static ClubDto? ClubDto(string? id, string? name) => id is null ? null : new(id, name ?? id, id);
    public static VotedRealPlayerWrapper Wrapper(FootballLiveVotesPayload payload) => new()
    {
        Players = payload.Players.Select(x => new VotedRealPlayer { Name = x.Name, RealTeam = Club(x.ClubId, x.ClubName)!, Role = (Role)x.PlayerRole,
            IsActive = x.IsActive, Visible = x.IsVisible, Vote = Domain(x.Vote) }).ToList()
    };
    public static ChancedRealPlayerWrapper Wrapper(FootballChancesPayload payload) => new()
    {
        Players = payload.Players.Select(x => new ChancedRealPlayer { Name = x.Name, RealTeam = Club(x.ClubId, x.ClubName)!, Role = (Role)x.Role,
            IsActive = x.IsActive, Visible = x.IsVisible, Chance = new() { Fantagazzetta = x.Fantagazzetta, Gazzetta = x.Gazzetta, Mediaset = x.Mediaset,
                Sky = x.Sky, Status = (ChanceType)x.Status, Description = x.Description } }).ToList()
    };
}
