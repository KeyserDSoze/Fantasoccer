using System.Security.Claims;
using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Business;
using Fantasoccer.Infrastructure.PostgreSql;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fantasoccer.Application.Core.Competitions;

public sealed partial class CompetitionApplicationService(FantasoccerDbContext database, IAuthorizationService authorization,
    IFactory<ILeagueCalculator> calculators)
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
        return await CalendarCoreAsync(group, league, year, cancellationToken);
    }

    // Callers have already authorized the group (group-scoped projections).
    internal async Task<FantasyCalendarDto?> CalendarCoreAsync(Guid group, Guid league, int year, CancellationToken cancellationToken)
    {
        var calendar = await database.FantasyCalendars.AsNoTracking().Include(x => x.Stages).ThenInclude(x => x.Days).ThenInclude(x => x.Games)
            .Where(x => x.GroupId == group && x.LeagueId == league && x.SeasonIndex == year).AsSplitQuery().SingleOrDefaultAsync(cancellationToken);
        return calendar is null ? null : new(year, calendar.Stages.OrderBy(x => x.Ordinal).ToDictionary(x => x.Stage,
            x => x.Days.OrderBy(day => day.Ordinal).Select(day => new CalendarDayDto(day.SerieADay, day.FantasyTurn,
                day.Games.OrderBy(game => game.Ordinal).Select(Game).ToArray()) { AwaitingPostponedMatches = day.AwaitingPostponedMatches }).ToArray()), calendar.Version);
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
        var season = await AuthorizeAsync(caller, group, league, year, cancellationToken);
        if (day is < 0 or > 38) throw new ApplicationFault(400, "invalid_rank_day", "Giornata non valida.");
        if (day > 0) return await HistoricalRankAsync(season, group, league, year, day, cancellationToken);
        return await CurrentRankAsync(group, league, year, cancellationToken);
    }

    internal async Task<FantasyRankDto?> CurrentRankAsync(Guid group, Guid league, int year, CancellationToken cancellationToken)
    {
        const int day = 0;
        var snapshot = await database.FantasyStandingSnapshots.AsNoTracking().Where(x => x.GroupId == group && x.LeagueId == league && x.SeasonIndex == year && x.SnapshotDay == day)
            .AsSplitQuery().SingleOrDefaultAsync(cancellationToken);
        if (snapshot is null) return null;
        var rank = new FantasyRankDto(snapshot.SerieADay, snapshot.Stages.OrderBy(x => x.Ordinal).ToDictionary(x => x.Stage,
            x => x.Teams.OrderBy(team => team.Ordinal).Select(team => new RankedTeamDto(team.TeamId, team.BasketId, team.Name, team.Owner, team.Points,
                team.Victories, team.Draws, team.Defeats, team.GoalsFor, team.GoalsAgainst, team.ValuePoints, team.SufferedValuePoints, team.PlusMoney, team.Money)).ToArray()), snapshot.SnapshotDay);
        return await WithCreditsAsync(rank, group, league, year, cancellationToken);
    }

    // Credits of every team, from its season roster: Money is what it saved from the starting credits (purchases, refunds, sales,
    // market, opening prize) without the goal credits already received, so ValueAssets = Money + PlusMoney is what it takes to the
    // next auction. Derived competitions have no economy of their own (their goal credits are never paid): no value there.
    private async Task<FantasyRankDto> WithCreditsAsync(FantasyRankDto rank, Guid group, Guid league, int year, CancellationToken cancellationToken)
    {
        var mode = await database.Leagues.AsNoTracking().Where(x => x.GroupId == group && x.LeagueId == league).Select(x => (LeagueMode?)x.Mode).SingleOrDefaultAsync(cancellationToken);
        var starting = await database.LeagueRules.AsNoTracking().Where(x => x.GroupId == group && x.LeagueId == league && x.SeasonIndex == year)
            .Select(x => (int?)x.StartingMoney).SingleOrDefaultAsync(cancellationToken);
        var ids = rank.Rounds.Values.SelectMany(x => x).Select(x => x.TeamId).Distinct().ToArray();
        var credits = new Dictionary<Guid, int>();
        if (mode != LeagueMode.Derived && starting is { } money)
        {
            var rosters = await database.RosterSnapshots.AsNoTracking().Include(x => x.Players)
                .Where(x => x.GroupId == group && x.SeasonIndex == year && x.Kind == RosterKind.Season && ids.Contains(x.TeamId)).AsSplitQuery().ToArrayAsync(cancellationToken);
            foreach (var roster in rosters.GroupBy(x => x.TeamId).Where(x => x.Count() == 1).Select(x => x.Single()))
                credits[roster.TeamId] = (int)Math.Clamp(Teams.RosterEconomy.Balance(roster, money) - (roster.RankCreditGranted ?? roster.MoneyFromRank), int.MinValue, int.MaxValue);
        }
        return rank with { Rounds = rank.Rounds.ToDictionary(x => x.Key, x => x.Value.Select(team => team with { Money = credits.TryGetValue(team.TeamId, out var value) ? value : null }).ToArray()) };
    }

    public static CalendarGameDto Game(FantasyMatch row) => new(row.Id, row.Number, row.HomeName, row.HomeOwner, row.AwayName, row.AwayOwner,
        Result(row.Result), row.HomeTeamId, row.AwayTeamId);
    public static ResultDto? Result(SavedFantasyResult? row) => row is null ? null : new(Point(row.Home), Point(row.Away), row.IsCancelled, row.HomeGoals, row.AwayGoals);
    private static PointDto Point(SavedFantasyPoint? point) => point is null ? new(0, false, false, false) : new(point.Value, point.DefensiveBonus, point.GoodPeople, point.OwnGoal);
}
