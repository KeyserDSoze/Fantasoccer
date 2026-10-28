using System.Security.Claims;
using Fantasoccer.Application.Core.Football;
using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Infrastructure.PostgreSql;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Fantasoccer.Application.Core.Competitions;

public sealed record LiveLeagueDto(Guid Id, string Name, Dictionary<string, CalendarDayDto> Rounds, FantasyRankDto? Rank, bool PreviewUnavailable);
public sealed record LiveGroupDto(string Name, int Year, LiveLeagueDto[] Leagues);

public sealed class LiveCompetitionApplicationService(FantasoccerDbContext database, CompetitionApplicationService competitions,
    GameApplicationService games, FootballApplicationService football, IAuthorizationService authorization, TimeProvider clock)
{
    public async Task<LiveGroupDto> GetAsync(ClaimsPrincipal caller, Guid group, int year, CancellationToken cancellationToken = default)
    {
        if (!(await authorization.AuthorizeAsync(caller, group, "workspace-member")).Succeeded)
            throw new ApplicationFault(403, "group_access_denied", "Accesso al gruppo non consentito.");
        var name = await database.Groups.Where(x => x.Id == group).Select(x => x.Name).SingleAsync(cancellationToken);
        var calendar = await football.CalendarAsync(caller, year, cancellationToken);
        var now = clock.GetUtcNow();
        var windows = (calendar?.Days ?? []).Select(x => new { x.SerieADay, Times = x.Games.Where(game => !game.Delayed && !game.WasPostponed && game.Date != null).Select(game => game.Date!.Value).ToArray() })
            .Where(x => x.Times.Length > 0).ToArray();
        var selected = windows.Where(x => now >= x.Times.Min() && now <= x.Times.Max().AddHours(10.25)).MaxBy(x => x.SerieADay)?.SerieADay
            ?? windows.Where(x => now < x.Times.Min()).MinBy(x => x.SerieADay)?.SerieADay
            ?? windows.MaxBy(x => x.SerieADay)?.SerieADay;
        var leagues = await database.LeagueSeasons.AsNoTracking().Include(x => x.League).Where(x => x.GroupId == group && x.SeasonIndex == year && !x.IsArchived && !x.League.IsArchived).ToArrayAsync(cancellationToken);
        var result = new List<LiveLeagueDto>();
        foreach (var league in leagues)
        {
            var fantasy = await competitions.CalendarAsync(caller, group, league.LeagueId, year, cancellationToken);
            var rounds = new Dictionary<string, CalendarDayDto>();
            var unavailable = false;
            foreach (var (stage, days) in fantasy?.Rounds ?? [])
                foreach (var day in days.Where(x => x.SerieADay == selected))
                {
                    var previews = new List<CalendarGameDto>();
                    foreach (var game in day.Games)
                    {
                        try { previews.Add((await games.GetAsync(caller, group, league.LeagueId, year, game.Id, cancellationToken: cancellationToken)).Game); }
                        catch (ApplicationFault fault) when (fault.Code == "historical_roster_unavailable")
                        { previews.Add(game); unavailable = true; }
                    }
                    rounds.Add(rounds.ContainsKey(stage) ? $"{stage}/turn/{day.Number}" : stage, day with { Games = previews.ToArray() });
                }
            result.Add(new(league.LeagueId, league.League.Name, rounds, await competitions.RankAsync(caller, group, league.LeagueId, year, cancellationToken: cancellationToken), unavailable));
        }
        return new(name, year, result.ToArray());
    }
}
