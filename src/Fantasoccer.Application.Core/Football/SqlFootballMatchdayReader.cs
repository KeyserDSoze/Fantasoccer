using Fantasoccer.Business;
using Fantasoccer.Infrastructure.PostgreSql;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fantasoccer.Application.Core.Football;

// The single calculation input for scoring, previews, the optimal formation and
// push producers: official votes from PostgreSQL, provisional live votes and
// probable lineups from the shared operational cache.
public sealed class SqlFootballMatchdayReader(FantasoccerDbContext database, IFootballLiveDataStore liveData, TimeProvider clock) : IFootballMatchdayReader
{
    public async Task<FootballMatchdayInput> ReadAsync(int year, int day, bool withVotes, bool withChances, CancellationToken cancellationToken = default)
    {
        var matches = await database.FootballMatches.AsNoTracking().Include(x => x.HomeClub).Include(x => x.AwayClub)
            .Where(x => x.SeasonIndex == year).OrderBy(x => x.SerieADay).ThenBy(x => x.Ordinal).ToArrayAsync(cancellationToken);
        var now = clock.GetUtcNow();
        var schedules = matches.Where(x => !x.Delayed && !x.WasPostponed && x.KickoffUtc != null).GroupBy(x => x.SerieADay)
            .Select(x => new { Day = x.Key, Start = x.Min(game => game.KickoffUtc!.Value), End = x.Max(game => game.KickoffUtc!.Value) }).ToArray();
        var liveDay = schedules.Where(x => now >= x.Start && now <= x.End.AddHours(10.25)).MaxBy(x => x.Day)?.Day ?? 0;
        var lastDay = schedules.Where(x => now >= x.End.AddHours(2.25)).MaxBy(x => x.Day)?.Day ?? 0;
        var nextDay = (liveDay > 0 ? liveDay : lastDay) + 1;
        if (day == 0) day = schedules.Any(x => x.Day == nextDay) ? nextDay : liveDay > 0 ? liveDay : 1;
        var selected = schedules.SingleOrDefault(x => x.Day == day);
        var games = matches.Where(x => x.SerieADay == day).Select(x => new RealGame
        {
            Home = FootballMapping.DomainClub(x.HomeClub), Away = FootballMapping.DomainClub(x.AwayClub),
            // The characterized scoring helpers compare Italian wall clocks.
            // Keep that convention only inside the engine; DTOs always carry UTC.
            Date = x.KickoffUtc is { } kickoff ? TimeZoneInfo.ConvertTime(kickoff, TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome")).DateTime : null,
            HomeGoal = x.HomeGoals, AwayGoal = x.AwayGoals, Delayed = x.Delayed, WasPostponed = x.WasPostponed,
            IsFinished = x.IsFinished, OriginalDate = x.OriginalKickoffUtc is { } original
                ? TimeZoneInfo.ConvertTime(original, TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome")).DateTime : null
        }).ToList();
        VotedRealPlayerWrapper? live = null, official = null;
        var recoveryLive = matches.Any(x => x.SerieADay == day && FootballLiveTiming.IsRecoveryLive(x.WasPostponed, x.Delayed, x.IsFinished, x.KickoffUtc, now));
        if (withVotes)
        {
            var votes = await database.FootballVotes.AsNoTracking().Include(x => x.Player).Include(x => x.Club)
                .Where(x => x.SeasonIndex == year && x.Day == day && x.Source == FootballVoteSource.Official).ToArrayAsync(cancellationToken);
            if (votes.Length > 0) official = new() { Players = votes.Select(x => new VotedRealPlayer { Name = x.Player.Name,
                RealTeam = x.Club is null ? null! : FootballMapping.DomainClub(x.Club), Role = (Role)x.PlayerRole, IsActive = x.IsActive, Visible = x.IsVisible,
                Vote = FootballMapping.DomainVote(x) }).ToList() };
            // Final scoring never fills historical gaps with live votes. A
            // recovery match keeps its own live window on the original day.
            if (day == liveDay || day == lastDay || recoveryLive)
            {
                var snapshot = await liveData.LiveVotesAsync(year, day, cancellationToken);
                if (snapshot is { Payload.Players.Length: > 0 }) live = FootballLiveDataMapping.Wrapper(snapshot.Payload);
            }
        }
        ChancedRealPlayerWrapper? chances = null;
        var chancesAreStale = false;
        if (withChances)
        {
            var snapshot = await liveData.ChancesAsync(year, day, cancellationToken);
            if (snapshot is { Payload.Players.Length: > 0 })
            {
                chances = FootballLiveDataMapping.Wrapper(snapshot.Payload);
                chancesAreStale = !snapshot.IsFreshAt(now);
            }
        }
        return new(day, day == liveDay || recoveryLive, day == nextDay && selected != null && now < selected.Start,
            games.Count == 0 ? null : new() { Year = year, SerieADay = day, Games = games }, live, official, chances) { ChancesAreStale = chancesAreStale };
    }
}
