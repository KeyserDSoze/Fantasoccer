using Fantasoccer.Application.Core.Caching;
using Fantasoccer.Application.Core.Football;
using Fantasoccer.Infrastructure.PostgreSql.Entities;

namespace Fantasoccer.DataMigration;

// Cutover without losing operational data: the latest probable lineups and
// provisional live votes of a matchday are published to the shared cache from
// the verified dump. They are never imported into PostgreSQL. The revision is the
// export start time, so any later provider fetch always wins (monotonic publish).
public static class OperationalCacheSeed
{
    public static (FootballChancesPayload? Chances, FootballLiveVotesPayload? Live) Build(CorePlan plan, int season, int day)
    {
        var names = plan.Players.ToDictionary(x => x.Id, x => x.Name, StringComparer.Ordinal);
        var clubs = plan.Clubs.ToDictionary(x => x.Id, x => x.Name, StringComparer.Ordinal);
        string Name(string id) => names.GetValueOrDefault(id) ?? id;
        var chances = plan.Chances.Where(x => x.SeasonIndex == season && x.Day == day).OrderBy(x => x.PlayerId, StringComparer.Ordinal)
            .Select(x => new CachedChance(x.PlayerId, Name(x.PlayerId), x.ClubId, clubs.GetValueOrDefault(x.ClubId), x.PlayerRole, x.IsActive, x.IsVisible,
                x.Fantagazzetta, x.Gazzetta, x.Mediaset, x.Sky, x.Status, x.Description)).ToArray();
        var live = plan.LiveVotes.Where(x => x.SeasonIndex == season && x.Day == day).OrderBy(x => x.PlayerId, StringComparer.Ordinal)
            .Select(x => new CachedLiveVote(x.PlayerId, Name(x.PlayerId), x.ClubId, x.ClubId is null ? null : clubs.GetValueOrDefault(x.ClubId), x.PlayerRole, x.IsActive, x.IsVisible,
                new(x.VoteRole, x.Value, x.IsFinal, x.Goals, x.Penalties, x.Assists, x.StoppedPenalties, x.SufferedGoals, x.WrongedPenalties, x.OwnGoals,
                    x.Status, x.ManOfTheMatch, x.HasVote, x.IsOut, x.IsIn, x.Injured))).ToArray();
        return (chances.Length == 0 ? null : new(season, day, ["legacy-dump"], chances), live.Length == 0 ? null : new(season, day, live));
    }

    public static async Task<(CachePublishOutcome? Chances, CachePublishOutcome? Live)> PublishAsync(IFootballLiveDataStore store, CorePlan plan, int season, int day,
        DateTimeOffset exportedAtUtc, CancellationToken cancellationToken = default)
    {
        var (chances, live) = Build(plan, season, day);
        return (chances is null ? null : await store.PublishChancesAsync(exportedAtUtc, chances, cancellationToken),
            live is null ? null : await store.PublishLiveVotesAsync(exportedAtUtc, live, null, cancellationToken));
    }
}
