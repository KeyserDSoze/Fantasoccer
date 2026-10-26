using System.Security.Claims;
using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Infrastructure.PostgreSql;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Fantasoccer.Application.Core.Competitions;

public sealed class CompetitionApplicationService(FantasoccerDbContext database, IAuthorizationService authorization)
{
    public async Task<LeagueSeason> AuthorizeAsync(ClaimsPrincipal caller, Guid group, Guid league, int year, CancellationToken cancellationToken)
    {
        if (!(await authorization.AuthorizeAsync(caller, group, "workspace-member")).Succeeded)
            throw new ApplicationFault(403, "competition_access_denied", "Accesso alla competizione non consentito.");
        return await database.LeagueSeasons.AsNoTracking().Include(x => x.League).SingleOrDefaultAsync(x => x.GroupId == group && x.LeagueId == league && x.SeasonIndex == year, cancellationToken)
            ?? throw new ApplicationFault(404, "competition_not_found", "Competizione non trovata nel gruppo e nella stagione richiesti.");
    }

    public async Task<FantasyCalendarDto?> CalendarAsync(ClaimsPrincipal caller, Guid group, Guid league, int year, CancellationToken cancellationToken = default)
    {
        await AuthorizeAsync(caller, group, league, year, cancellationToken);
        var calendar = await database.FantasyCalendars.AsNoTracking().Include(x => x.Stages).ThenInclude(x => x.Days).ThenInclude(x => x.Games)
            .Where(x => x.GroupId == group && x.LeagueId == league && x.SeasonIndex == year).AsSplitQuery().SingleOrDefaultAsync(cancellationToken);
        return calendar is null ? null : new(year, calendar.Stages.OrderBy(x => x.Ordinal).ToDictionary(x => x.Stage,
            x => x.Days.OrderBy(day => day.Ordinal).Select(day => new CalendarDayDto(day.SerieADay, day.FantasyTurn,
                day.Games.OrderBy(game => game.Ordinal).Select(Game).ToArray())).ToArray()), calendar.Version);
    }

    public async Task<LeagueSeason> AuthorizeManagerAsync(ClaimsPrincipal caller, Guid group, Guid league, int year, CancellationToken cancellationToken)
    {
        var season = await AuthorizeAsync(caller, group, league, year, cancellationToken);
        var email = caller.FindFirstValue(ClaimTypes.Email)!.Trim().ToLowerInvariant();
        if (season.IsArchived || season.League.IsArchived || !await database.GroupMemberships.AnyAsync(x => x.GroupId == group && x.User.Email == email &&
            x.User.IsEnabled && (x.Roles & GroupRole.SuperAdmin) != 0, cancellationToken))
            throw new ApplicationFault(403, "competition_manager_required", "Serve un responsabile del gruppo per questa operazione.");
        return season;
    }

    public async Task<FantasyRankDto?> RankAsync(ClaimsPrincipal caller, Guid group, Guid league, int year, int day = 0, CancellationToken cancellationToken = default)
    {
        await AuthorizeAsync(caller, group, league, year, cancellationToken);
        if (day is < 0 or > 38) throw new ApplicationFault(400, "invalid_rank_day", "Giornata non valida.");
        var snapshot = await database.FantasyStandingSnapshots.AsNoTracking().Where(x => x.GroupId == group && x.LeagueId == league && x.SeasonIndex == year && x.SnapshotDay == day)
            .AsSplitQuery().SingleOrDefaultAsync(cancellationToken);
        return snapshot is null ? null : new(snapshot.SerieADay, snapshot.Stages.OrderBy(x => x.Ordinal).ToDictionary(x => x.Stage,
            x => x.Teams.OrderBy(team => team.Ordinal).Select(team => new RankedTeamDto(team.TeamId, team.BasketId, team.Name, team.Owner, team.Points,
                team.Victories, team.Draws, team.Defeats, team.GoalsFor, team.GoalsAgainst, team.ValuePoints, team.SufferedValuePoints, team.PlusMoney, team.Money)).ToArray()), snapshot.SnapshotDay);
    }

    public static CalendarGameDto Game(FantasyMatch row) => new(row.Id, row.Number, row.HomeName, row.HomeOwner, row.AwayName, row.AwayOwner,
        Result(row.Result), row.HomeTeamId, row.AwayTeamId);
    public static ResultDto? Result(SavedFantasyResult? row) => row is null ? null : new(Point(row.Home), Point(row.Away), row.IsCancelled, row.HomeGoals, row.AwayGoals);
    private static PointDto Point(SavedFantasyPoint? point) => point is null ? new(0, false, false, false) : new(point.Value, point.DefensiveBonus, point.GoodPeople, point.OwnGoal);
}
