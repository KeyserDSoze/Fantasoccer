using System.Security.Claims;
using Fantasoccer.Application.Core.Competitions;
using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Application.Core.Teams;
using Fantasoccer.Infrastructure.PostgreSql;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Fantasoccer.Application.Core.HallOfFame;

public sealed class HallOfFameApplicationService(FantasoccerDbContext database, IAuthorizationService authorization)
{
    public async Task<HallOfFameDto?> GetAsync(ClaimsPrincipal caller, Guid group, Guid league, CancellationToken cancellationToken = default)
    {
        if (!(await authorization.AuthorizeAsync(caller, group, "workspace-member")).Succeeded)
            throw new ApplicationFault(403, "hall_access_denied", "Accesso all'albo della lega non consentito.");
        if (!await database.Leagues.AnyAsync(x => x.GroupId == group && x.LeagueId == league, cancellationToken))
            throw new ApplicationFault(404, "league_not_found", "Lega non trovata nel gruppo richiesto.");
        var archive = await database.HallArchives.AsNoTracking().Include(x => x.PlayerRecords).ThenInclude(x => x.Player).ThenInclude(x => x.Club)
            .Include(x => x.WinningPlayers).ThenInclude(x => x.Player).ThenInclude(x => x.Club)
            .Where(x => x.GroupId == group && x.LeagueId == league).AsSplitQuery().SingleOrDefaultAsync(cancellationToken);
        return archive is null ? null : Project(archive);
    }

    internal static HallOfFameDto Project(HallArchive archive)
    {
        var single = archive.PlayerRecords.SingleOrDefault(x => x.Kind == 0);
        var season = archive.PlayerRecords.SingleOrDefault(x => x.Kind == 1);
        return new(archive.RecordGame is not { } game ? null : new(new(game.Id, game.Number, game.Home, game.HomeOwner, game.Away, game.AwayOwner,
                CompetitionApplicationService.Result(game.Result), null, null), game.Year, game.LinkedMatchId != null),
            single is null ? null : new(Player(single.Player), single.Points),
            season is null ? null : new(Player(season.Player), season.Year!.Value, season.Points),
            archive.WinningPlayers.OrderBy(x => x.Ordinal).Select(x => new HallWinningPlayerDto(Player(x.Player), Wins(x.Wins))).ToArray(),
            archive.WinningTeams.OrderBy(x => x.Ordinal).Select(x => new HallWinningTeamDto(x.OwnerUserId, x.OwnerEmail, x.Name, Wins(x.Wins))).ToArray(),
            archive.Ranks.OrderBy(x => x.Ordinal).Select(x => new HallRankDto(x.OwnerUserId, x.Name, x.OwnerEmail, x.Points, x.Victories, x.Draws, x.Defeats,
                x.Goals, x.SufferedGoals, x.ValuePoints, x.SufferedValuePoints, x.PlusMoney, x.Money)).ToArray(), archive.Version, archive.ContributionsInitialized);
    }
    private static RosterPlayerDto Player(HallPlayerSnapshot row) => new(row.PlayerId, 0, row.Name, new(row.Club.Name, row.Club.Abbreviation),
        row.PlayerRole, row.IsActive, row.IsVisible, row.Price, row.Revenue, row.Status, row.Position);
    private static Dictionary<string, int[]> Wins(IEnumerable<HallWinStage> stages) => stages.OrderBy(x => x.Ordinal).ToDictionary(x => x.Stage,
        x => x.Years.OrderBy(year => year.Ordinal).Select(year => year.Year).ToArray());
}
