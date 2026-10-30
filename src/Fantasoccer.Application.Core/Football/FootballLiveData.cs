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

public sealed class FootballLiveDataStore(SharedSnapshotCache cache, TimeProvider? clock = null) : IFootballLiveDataStore
{
    public const string ChancesKind = "football-chances";
    public const string LiveVotesKind = "football-live-votes";
    // Retention of the physical blob; the purge job and the lifecycle policy remove it afterwards.
    private static readonly TimeSpan Retention = TimeSpan.FromDays(21);
    // Probable lineups can be used for 30 days, so their blob must outlive that.
    private static readonly TimeSpan ChancesRetention = TimeSpan.FromDays(35);

    // Freshness is about the last confirmation of the content (GeneratedAtUtc), not
    // about when it last changed: an identical snapshot is not rewritten, it is only
    // re-confirmed at most once per `ConfirmEvery` (Blob soft delete keeps a copy of
    // every overwrite).
    //
    // Probable lineups: the job runs a handful of times a day and the longest gap is
    // overnight, so a confirmation stays current for 12 h. After 30 days without any
    // confirmation they are treated as missing: no data is better than wrong data.
    // The job only refreshes the current or next matchday, so a past matchday keeps
    // its last known lineups for that long.
    public static readonly TimeSpan ChancesFreshFor = TimeSpan.FromHours(12);
    public static readonly TimeSpan ChancesUsableFor = TimeSpan.FromDays(30);
    private static readonly TimeSpan ChancesConfirmEvery = TimeSpan.FromHours(1);
    // Provisional votes: the job runs every minute during the live window. Even when
    // stale they stay usable (they are the last known provisional votes of that
    // matchday and official votes supersede them); callers flag them as not updated.
    public static readonly TimeSpan LiveVotesFreshFor = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan LiveVotesConfirmEvery = TimeSpan.FromMinutes(5);

    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public async Task<CacheEnvelope<FootballChancesPayload>?> ChancesAsync(int season, int day, CancellationToken cancellationToken = default)
    {
        var snapshot = await cache.ReadAsync<FootballChancesPayload>(CacheKeys.FootballChances(season, day), ChancesKind, CacheKeys.FootballSchemaVersion, cancellationToken);
        return snapshot is not null && _clock.GetUtcNow() - snapshot.GeneratedAtUtc > ChancesUsableFor ? null : snapshot;
    }

    public Task<CacheEnvelope<FootballLiveVotesPayload>?> LiveVotesAsync(int season, int day, CancellationToken cancellationToken = default) =>
        cache.ReadAsync<FootballLiveVotesPayload>(CacheKeys.FootballLiveVotes(season, day), LiveVotesKind, CacheKeys.FootballSchemaVersion, cancellationToken);

    public Task<CachePublishOutcome> PublishChancesAsync(DateTimeOffset fetchedAtUtc, FootballChancesPayload payload, CancellationToken cancellationToken = default)
    {
        // Never replace a valid snapshot with an empty one.
        if (payload.Players.Length == 0) throw new ArgumentException("Empty probable-lineup snapshot.", nameof(payload));
        return cache.PublishAsync(CacheKeys.FootballChances(payload.Season, payload.Day),
            new CacheEnvelope<FootballChancesPayload>(CacheKeys.FootballSchemaVersion, ChancesKind, fetchedAtUtc, fetchedAtUtc + ChancesFreshFor,
                fetchedAtUtc.UtcTicks, CacheContent.Fingerprint(payload), payload), ChancesRetention, confirmEvery: ChancesConfirmEvery, cancellationToken: cancellationToken);
    }

    public Task<CachePublishOutcome> PublishLiveVotesAsync(DateTimeOffset fetchedAtUtc, FootballLiveVotesPayload payload, IReadOnlySet<string>? clubScope,
        CancellationToken cancellationToken = default)
    {
        if (payload.Players.Length == 0) throw new ArgumentException("Empty live-vote snapshot.", nameof(payload));
        var envelope = new CacheEnvelope<FootballLiveVotesPayload>(CacheKeys.FootballSchemaVersion, LiveVotesKind, fetchedAtUtc, fetchedAtUtc + LiveVotesFreshFor,
            fetchedAtUtc.UtcTicks, CacheContent.Fingerprint(payload), payload);
        Func<CacheEnvelope<FootballLiveVotesPayload>, CacheEnvelope<FootballLiveVotesPayload>, CacheEnvelope<FootballLiveVotesPayload>?>? merge = clubScope is null ? null
            : (current, incoming) =>
            {
                var merged = incoming.Payload with
                {
                    Players = current.Payload.Players.Where(x => x.ClubId is null || !clubScope.Contains(x.ClubId))
                        .Concat(incoming.Payload.Players).OrderBy(x => x.PlayerId, StringComparer.Ordinal).ToArray()
                };
                // The fingerprint always describes the stored (merged) content.
                return incoming with { Payload = merged, SourceFingerprint = CacheContent.Fingerprint(merged) };
            };
        return cache.PublishAsync(CacheKeys.FootballLiveVotes(payload.Season, payload.Day), envelope, Retention, merge, LiveVotesConfirmEvery, cancellationToken);
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
