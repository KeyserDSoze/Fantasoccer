using Fantasoccer.Business;
using Fantasoccer.Infrastructure.PostgreSql;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fantasoccer.Application.Core.Football;

public sealed class SqlFootballMatchdayReader(FantasoccerDbContext database, TimeProvider clock) : IFootballMatchdayReader
{
    public async Task<FootballMatchdayInput> ReadAsync(int year, int day, bool withVotes, bool withChances, CancellationToken cancellationToken = default)
    {
        var matches = await database.FootballMatches.AsNoTracking().Include(x => x.HomeClub).Include(x => x.AwayClub)
            .Where(x => x.SeasonIndex == year).OrderBy(x => x.SerieADay).ThenBy(x => x.Ordinal).ToArrayAsync(cancellationToken);
        var now = clock.GetUtcNow();
        var schedules = matches.Where(x => !x.Delayed && x.KickoffUtc != null).GroupBy(x => x.SerieADay)
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
            HomeGoal = x.HomeGoals, AwayGoal = x.AwayGoals, Delayed = x.Delayed
        }).ToList();
        VotedRealPlayerWrapper? live = null, official = null;
        if (withVotes)
        {
            var votes = await database.FootballVotes.AsNoTracking().Include(x => x.Player).Include(x => x.Club)
                .Where(x => x.SeasonIndex == year && x.Day == day && x.Source != FootballVoteSource.Notification).ToArrayAsync(cancellationToken);
            VotedRealPlayerWrapper? Wrap(FootballVoteSource source)
            {
                var rows = votes.Where(x => x.Source == source).ToArray();
                return rows.Length == 0 ? null : new() { Players = rows.Select(x => new VotedRealPlayer { Name = x.Player.Name,
                    RealTeam = x.Club is null ? null! : FootballMapping.DomainClub(x.Club), Role = (Role)x.PlayerRole, IsActive = x.IsActive, Visible = x.IsVisible,
                    Vote = FootballMapping.DomainVote(x) }).ToList() };
            }
            if (day == liveDay || day == lastDay) live = Wrap(FootballVoteSource.Live);
            official = Wrap(FootballVoteSource.Official);
        }
        ChancedRealPlayerWrapper? chances = null;
        if (withChances)
        {
            var rows = await database.FootballChances.AsNoTracking().Include(x => x.Player).Include(x => x.Club)
                .Where(x => x.SeasonIndex == year && x.Day == day).ToArrayAsync(cancellationToken);
            if (rows.Length > 0) chances = new() { Players = rows.Select(x => new ChancedRealPlayer { Name = x.Player.Name,
                RealTeam = FootballMapping.DomainClub(x.Club), Role = (Role)x.PlayerRole, IsActive = x.IsActive, Visible = x.IsVisible,
                Chance = new() { Fantagazzetta = x.Fantagazzetta, Gazzetta = x.Gazzetta, Mediaset = x.Mediaset, Sky = x.Sky,
                    Status = (ChanceType)x.Status, Description = x.Description } }).ToList() };
        }
        return new(day, day == liveDay, day == nextDay && selected != null && now < selected.Start,
            games.Count == 0 ? null : new() { Year = year, SerieADay = day, Games = games }, live, official, chances);
    }
}
